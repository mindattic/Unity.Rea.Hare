using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Enemies;
using Unity.Hare.Pickups;

namespace Unity.Hare.Helper
{
    /// <summary>
    /// The helper hawks of a room, engine-free (gmlHARE: create_helper and obj_helper). An arrow key calls one at the ship
    /// (one at a time: <c>if (!instance_exists(obj_helper))</c>, here one per ship); it flies at 4 px a step along the
    /// ship's image_angle, the left and right arrows turn it 2 degrees a step, Up and Down step its speed between 1 and 4,
    /// Page Down makes it follow the mouse (solo), Delete sets it off as an obj_rocket_explosion. Out of view it flies
    /// straight back to the ship until within 400 px. It smokes gold every free step, orange on the way back, trails
    /// ef_smokeup every 40 steps and sparks every 5 while turning. obj_carrot and obj_coin collide with it as with the
    /// ship: the presentation finds what its mask touches and sends the pickups' own CollectPickup for the ship that owns it.
    ///
    /// A step runs in GameMaker's order: the alarms, the held arrows (the keyboard events), the Step, then the built-in
    /// motion. The key presses and releases are commands that run when they happen. Every random value (the spark's colour
    /// and jitter) is drawn from the field's own <see cref="IRandomSource"/> in the original's order.
    /// </summary>
    public sealed class HelperField
    {
        const float StepEpsilon = 1e-4f;

        readonly IRandomSource rng;
        readonly List<HelperHawk> helpers = new List<HelperHawk>();
        readonly SortedDictionary<int, HelperOwner> owners = new SortedDictionary<int, HelperOwner>();
        readonly Dictionary<int, Vec2> mice = new Dictionary<int, Vec2>();
        readonly List<HelperEvent> events = new List<HelperEvent>();
        float pendingSteps;
        int nextId;

        /// <param name="rng">The random source (GML's engine random).</param>
        /// <param name="tuning">The numbers; null reads <see cref="GameContent.Current"/>.</param>
        public HelperField(IRandomSource rng, HelperTuning tuning = null)
        {
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Tuning = tuning ?? GameContent.Current.Helper;
        }

        public HelperTuning Tuning { get; }
        public IReadOnlyList<HelperHawk> Helpers => helpers;
        /// <summary>View 0 in the world (in_view's view_xview..view_wview); null counts everything as in view.</summary>
        public RoomBounds? View { get; internal set; }
        /// <summary>GML paused.</summary>
        public bool Paused { get; internal set; }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public HelperResult Execute(HelperCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<HelperEvent>() : events.ToArray();
            events.Clear();
            return new HelperResult(accepted, raised);
        }

        /// <summary>The helper ship <paramref name="owner"/> called, or null.</summary>
        public HelperHawk HelperOf(PlayerId owner)
        {
            foreach (var h in helpers) if (h.Owner.Equals(owner)) return h;
            return null;
        }

        /// <summary>A helper still in the field, or null.</summary>
        public HelperHawk Find(int id)
        {
            foreach (var h in helpers) if (h.Id == id) return h;
            return null;
        }

        // ---- The keys ----

        /// <summary>create_helper, then obj_helper's Create.</summary>
        internal HelperHawk Call(PlayerId owner, Vec2 at, float imageAngle)
        {
            if (HelperOf(owner) != null) return null;
            var t = Tuning;
            var h = new HelperHawk(++nextId, owner, at)
            {
                TurnSpeed = t.TurnSpeed,
                SpeedPx = t.StartSpeedPx,
                SpeedSetting = t.StartSpeedPx,
                LastSpeed = t.StartSpeedPx,
                MaxSpeed = t.MaxSpeedPx,
                FirstPass = true,
                SmokeAlarm = t.SmokeEverySteps,
                SparkAlarm = t.SparkEverySteps,
                State = HelperState.Free
            };
            h.Direction = imageAngle; // direction = player.image_angle;
            h.ImageAngle = h.Direction; // image_angle = direction;
            helpers.Add(h);
            events.Add(new HelperCalled { Helper = h });
            return h;
        }

        internal bool HoldTurn(PlayerId owner, bool counterclockwise, bool clockwise)
        {
            var h = HelperOf(owner);
            if (h == null) return false;
            // Key-release events for vk_left (37) and vk_right (39): turning = false, paused or not.
            if ((h.CounterclockwiseHeld && !counterclockwise) || (h.ClockwiseHeld && !clockwise)) h.Turning = false;
            h.CounterclockwiseHeld = counterclockwise;
            h.ClockwiseHeld = clockwise;
            return true;
        }

        internal bool ChangeSpeed(PlayerId owner, int sign)
        {
            var h = HelperOf(owner);
            if (h == null || Paused || h.State != HelperState.Free) return false;
            var t = Tuning;
            if (sign > 0)
            {
                if (!(h.SpeedPx < h.MaxSpeed)) return false;
                h.SpeedPx += t.SpeedStepPx;
            }
            else
            {
                if (!(h.SpeedPx > t.MinSpeedPx)) return false;
                h.SpeedPx -= t.SpeedStepPx;
            }
            h.SpeedSetting = h.SpeedPx;
            return true;
        }

        internal bool ToggleSolo(PlayerId owner)
        {
            var h = HelperOf(owner);
            if (h == null) return false;
            h.Solo = !h.Solo;
            return true;
        }

        internal bool Detonate(PlayerId owner)
        {
            var h = HelperOf(owner);
            if (h == null) return false;
            h.IsGone = true;
            helpers.Remove(h);
            events.Add(new HelperExploded { Helper = h, At = h.Position });
            return true;
        }

        internal void SetOwner(PlayerId id, HelperOwner owner)
        {
            owners[id.Value] = owner;
            if (owner.Mouse.HasValue) mice[id.Value] = owner.Mouse.Value;
        }

        internal bool Forget(int id)
        {
            for (int i = 0; i < helpers.Count; i++)
                if (helpers[i].Id == id) { helpers[i].IsGone = true; helpers.RemoveAt(i); return true; }
            return false;
        }

        // ---- The step ----

        internal void Tick(float steps)
        {
            if (steps <= 0f) return;
            pendingSteps += steps;
            while (pendingSteps >= 1f - StepEpsilon)
            {
                pendingSteps -= 1f;
                foreach (var h in helpers.ToArray()) StepOnce(h);
            }
            if (pendingSteps < 0f) pendingSteps = 0f;
        }

        void StepOnce(HelperHawk h)
        {
            var t = Tuning;

            // Alarm 0: the smoke trail behind it (gated by paused, re-armed either way).
            if (--h.SmokeAlarm <= 0)
            {
                if (!Paused) events.Add(new HelperTrailed { Helper = h, At = h.Position - h.StepVelocity });
                h.SmokeAlarm = t.SmokeEverySteps;
            }

            // Alarm 1: a spark while turning, amber, pink or cornflower blue (c_yellow is never kept: the roll is 1-3).
            if (--h.SparkAlarm <= 0)
            {
                if (!Paused && h.Turning)
                {
                    int color = rng.IRandom(1, 3);
                    int dx = rng.IRandom(-t.SparkJitterPx, t.SparkJitterPx);
                    int dy = rng.IRandom(-t.SparkJitterPx, t.SparkJitterPx);
                    var at = h.Position - h.StepVelocity + new Vec2(GameClock.Px(dx), -GameClock.Px(dy)); // GML y down
                    events.Add(new HelperSparked
                    {
                        Helper = h,
                        At = at,
                        Color = color == 1 ? SparkleColor.Amber : color == 2 ? SparkleColor.Pink : SparkleColor.CornflowerBlue
                    });
                }
                h.SparkAlarm = t.SparkEverySteps;
            }

            // The keyboard events for the held arrows: vk_left (37) then vk_right (39).
            if (!Paused && h.State == HelperState.Free)
            {
                if (h.CounterclockwiseHeld) Turn(h, +h.TurnSpeed);
                if (h.ClockwiseHeld) Turn(h, -h.TurnSpeed);
            }

            // The Step.
            if (!Paused)
            {
                h.SpeedPx = h.SpeedSetting;
                if (h.State == HelperState.Free) StepFree(h);
                else StepReturn(h);
            }
            else h.SpeedPx = 0f;

            // The built-in motion.
            if (h.SpeedPx != 0f) h.Position += h.StepVelocity;
        }

        static void Turn(HelperHawk h, float degrees)
        {
            h.Turning = true;
            h.Direction += degrees;
            h.ImageAngle += degrees;
        }

        void StepFree(HelperHawk h)
        {
            var t = Tuning;
            if (h.Solo && mice.TryGetValue(h.Owner.Value, out var mouse))
            {
                float mouseDirection = GmlMath.PointDirection(h.Position, mouse);
                if (!GmlMath.ANearB(h.Direction, mouseDirection, t.SoloNearDegrees))
                {
                    bool clockwise = GmlMath.CheckRotateClockwise(mouseDirection, h.Direction);
                    h.Direction += clockwise ? -t.SoloTurnDegrees : t.SoloTurnDegrees;
                    h.ImageAngle = h.Direction;
                }
            }

            if (h.FirstPass)
            {
                h.FirstPass = false;
                events.Add(new HelperRinged { Helper = h, At = h.Position });
            }

            if (!InView(h.Position))
            {
                h.LastSpeed = h.SpeedPx;
                h.State = HelperState.Return;
                events.Add(new HelperReturning { Helper = h });
            }
            else events.Add(new HelperSmoked { Helper = h, At = h.Position, Returning = false });
        }

        void StepReturn(HelperHawk h)
        {
            h.FirstPass = false;
            if (!owners.TryGetValue(h.Owner.Value, out var owner)) return; // no player to fly home to

            if (DistanceToObjectPx(h, owner.Box) > Tuning.ReturnDistancePx)
            {
                events.Add(new HelperSmoked { Helper = h, At = h.Position, Returning = true });
                h.Direction = GmlMath.PointDirection(h.Position, owner.Position);
                h.ImageAngle = h.Direction;
                h.SpeedPx = h.MaxSpeed;
                if (owner.SpeedPxPerStep > h.MaxSpeed) h.SpeedPx = owner.SpeedPxPerStep;
                h.SpeedSetting = h.SpeedPx;
            }
            else
            {
                h.State = HelperState.Free;
                h.FirstPass = true;
                h.SpeedPx = h.LastSpeed;
                h.SpeedSetting = h.SpeedPx;
                events.Add(new HelperRejoined { Helper = h });
            }
        }

        /// <summary>GML in_view(x, y): strictly inside view 0.</summary>
        bool InView(Vec2 at)
        {
            if (!View.HasValue) return true;
            var v = View.Value;
            return at.X > v.XMin && at.X < v.XMax && at.Y > v.YMin && at.Y < v.YMax;
        }

        /// <summary>
        /// GML distance_to_object(player): the gap, px, between the helper's bounding box (spr_helper's, turned to its
        /// image_angle) and the ship's; 0 when they overlap.
        /// </summary>
        public float DistanceToObjectPx(HelperHawk h, RoomBounds other)
        {
            var self = BoxOf(h);
            float dx = Math.Max(0f, Math.Max(other.XMin - self.XMax, self.XMin - other.XMax));
            float dy = Math.Max(0f, Math.Max(other.YMin - self.YMax, self.YMin - other.YMax));
            return (float)Math.Sqrt(dx * dx + dy * dy) * GameClock.PixelsPerUnit;
        }

        /// <summary>The helper's bounding box in the world: spr_helper's box about its origin, turned to image_angle.</summary>
        public RoomBounds BoxOf(HelperHawk h)
        {
            var box = Tuning.Box;
            // GML px about the origin (y down) as world offsets (y up): left..right+1, top..bottom+1.
            float left = box[0], right = box[2] + 1f, top = -box[1], bottom = -(box[3] + 1f);
            var corners = new[] { new Vec2(left, top), new Vec2(right, top), new Vec2(left, bottom), new Vec2(right, bottom) };
            double r = h.ImageAngle * Math.PI / 180.0;
            float cos = (float)Math.Cos(r), sin = (float)Math.Sin(r);
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            foreach (var c in corners)
            {
                float x = c.X * cos - c.Y * sin, y = c.X * sin + c.Y * cos;
                xMin = Math.Min(xMin, x); xMax = Math.Max(xMax, x);
                yMin = Math.Min(yMin, y); yMax = Math.Max(yMax, y);
            }
            return new RoomBounds(h.Position.X + GameClock.Px(xMin), h.Position.Y + GameClock.Px(yMin),
                h.Position.X + GameClock.Px(xMax), h.Position.Y + GameClock.Px(yMax));
        }
    }
}
