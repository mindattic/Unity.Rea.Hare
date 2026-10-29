using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Enemies;

namespace Unity.Hare.Pickups
{
    /// <summary>
    /// The pickups and the scraps of a room, engine-free (gmlHARE: par_pickup's obj_carrot and obj_coin, random_pickup,
    /// spawn_chips, obj_chip, obj_cartridge). Every random value is drawn from the field's own <see cref="IRandomSource"/>
    /// in the original's order, so a seed repeats a run. State changes only through <see cref="Execute"/>; presentation
    /// reads <see cref="Pickups"/> and <see cref="Scraps"/> and plays the events.
    ///
    /// A step runs in GameMaker's order: each instance's alarms, then its Step, then the built-in motion
    /// (move_towards_point's speed and direction, which GameMaker keeps applying even while <c>paused</c>, since only the
    /// objects' own code checks it). Which ship a pickup touches is the presentation's physics question; it answers with
    /// <see cref="CollectPickup"/>.
    /// </summary>
    public sealed class PickupField
    {
        const float Epsilon = 1e-3f;

        readonly IRandomSource rng;
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<Scrap> scraps = new List<Scrap>();
        readonly SortedDictionary<int, Vec2> targets = new SortedDictionary<int, Vec2>();
        readonly List<PickupEvent> events = new List<PickupEvent>();
        int nextId;

        /// <param name="rng">The random source (GML's engine random).</param>
        /// <param name="tuning">The numbers; null reads <see cref="GameContent.Current"/>.</param>
        public PickupField(IRandomSource rng, PickupTuning tuning = null)
        {
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            Tuning = tuning ?? GameContent.Current.Pickups;
            Room = RoomBounds.Gamescreen;
        }

        public PickupTuning Tuning { get; }
        public IReadOnlyList<Pickup> Pickups => pickups;
        public IReadOnlyList<Scrap> Scraps => scraps;
        /// <summary>The room (world units): what falls past its bottom plus its sprite_height is gone.</summary>
        public RoomBounds Room { get; internal set; }
        /// <summary>GML paused.</summary>
        public bool Paused { get; internal set; }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public PickupResult Execute(PickupCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<PickupEvent>() : events.ToArray();
            events.Clear();
            return new PickupResult(accepted, raised);
        }

        /// <summary>A pickup still in the field, or null.</summary>
        public Pickup FindPickup(int id)
        {
            foreach (var p in pickups) if (p.Id == id) return p;
            return null;
        }

        // ---- Creating ----

        /// <summary>random_pickup: <c>irandom_range(1, 2)</c>; 1 is a 1-in-4 carrot, 2 a coin. False when nothing dropped.</summary>
        internal bool DropRandom(Vec2 at)
        {
            int roll = rng.IRandom(1, Tuning.PickupRollMax);
            if (roll == 1)
            {
                if (rng.IRandom(1, Tuning.CarrotOneIn) != 1) return false;
                Drop(PickupKind.Carrot, at);
                return true;
            }
            if (roll == 2)
            {
                Drop(PickupKind.Coin, at);
                return true;
            }
            return false;
        }

        /// <summary>obj_carrot's or obj_coin's Create.</summary>
        internal Pickup Drop(PickupKind kind, Vec2 at)
        {
            Pickup pickup;
            if (kind == PickupKind.Carrot)
            {
                pickup = new Pickup(++nextId, kind, CoinGrade.Blue, Tuning.CarrotHeal, at)
                {
                    HoverAlarm = Tuning.CarrotHoverEverySteps, // alarm[0] = rate
                    PingAlarm = Tuning.CarrotPingEverySteps    // alarm[1] = ping_rate
                };
            }
            else
            {
                // rndvalue = irandom_range(1, 100): <= 75 blue (5), < 98 silver (10), >= 98 gold (20).
                int roll = rng.IRandom(1, Tuning.CoinRollMax);
                var grade = roll <= Tuning.BlueCoinMaxRoll ? CoinGrade.Blue
                    : roll < Tuning.GoldCoinMinRoll ? CoinGrade.Silver
                    : CoinGrade.Gold;
                int value = grade == CoinGrade.Blue ? Tuning.BlueCoinValue
                    : grade == CoinGrade.Silver ? Tuning.SilverCoinValue
                    : Tuning.GoldCoinValue;
                pickup = new Pickup(++nextId, kind, grade, value, at) { SparkleAlarm = Tuning.CoinSparkleEverySteps };
            }
            pickups.Add(pickup);
            events.Add(new PickupDropped { Pickup = pickup });
            return pickup;
        }

        /// <summary>Where player_leap and the par_rock landing kick up dirt: <c>x + lengthdir(32, image_angle)</c>.</summary>
        public Vec2 FeetOf(Vec2 shipAt, float imageAngle) => shipAt + GmlMath.Dir(imageAngle) * GameClock.Px(Tuning.FeetDistancePx);

        /// <summary>spawn_chips(feetX, feetY): returns how many chips flew.</summary>
        internal int SpawnChips(Vec2 feet, float playerImageAngle)
        {
            int count = rng.IRandom(Tuning.ChipCountMin, Tuning.ChipCountMax);
            var heights = Tuning.ChipHeightsPx;
            for (int i = 0; i < count; i++)
            {
                // instance_create(feetX, feetY + irandom_range(-3, 3), obj_chip): GML y grows downward.
                int jitter = rng.IRandom(-Tuning.ChipJitterPx, Tuning.ChipJitterPx);
                var at = new Vec2(feet.X, feet.Y - GameClock.Px(jitter));

                // obj_chip's Create, in its order.
                int variant = rng.IRandom(1, heights.Length);
                var chip = new Scrap(++nextId, ScrapKind.Chip, variant, at, Tuning.ChipScale)
                {
                    HeightPx = heights[variant - 1] * Tuning.ChipScale,
                    MoveEverySteps = Tuning.ChipMoveEverySteps,
                    MoveAlarm = Tuning.ChipMoveEverySteps
                };
                chip.Direction = playerImageAngle + rng.IRandom(-Tuning.ChipDirectionSpread, Tuning.ChipDirectionSpread);
                chip.ImageAngle = rng.IRandom(0, 359);
                chip.Spin = Spin(Tuning.ChipSpinMax);
                chip.VelocityXPx = rng.IRandom(-Tuning.ChipVelocityXMax, Tuning.ChipVelocityXMax);
                chip.VelocityYPx = rng.IRandom(Tuning.ChipVelocityYMin, Tuning.ChipVelocityYMax);
                if (rng.IRandom(1, Tuning.ChipSpurtOneIn) == 1)
                    chip.VelocityYPx = rng.IRandom(Tuning.ChipSpurtVelocityYMin, Tuning.ChipSpurtVelocityYMax);
                Add(chip);
            }
            return count;
        }

        /// <summary>obj_cartridge's Create at the gun, as the vulcan's cartridge or the shotgun's shell.</summary>
        internal Scrap SpawnCasing(ScrapKind kind, Vec2 at)
        {
            if (kind == ScrapKind.Chip) return null;
            var casing = new Scrap(++nextId, kind, 1, at, 1f)
            {
                HeightPx = kind == ScrapKind.Shell ? Tuning.ShellHeightPx : Tuning.CartridgeHeightPx,
                MoveEverySteps = Tuning.CasingMoveEverySteps,
                MoveAlarm = Tuning.CasingMoveEverySteps
            };
            casing.VelocityXPx = rng.IRandom(-Tuning.CasingVelocityXMax, Tuning.CasingVelocityXMax);
            casing.VelocityYPx = rng.IRandom(Tuning.CasingVelocityYMin, Tuning.CasingVelocityYMax);
            casing.Spin = Spin(Tuning.CasingSpinMax);
            Add(casing);
            return casing;
        }

        /// <summary><c>spin = irandom_range(-max, max); if (spin == 0) spin = 3;</c></summary>
        int Spin(int max)
        {
            int spin = rng.IRandom(-max, max);
            return spin == 0 ? Tuning.ScrapZeroSpin : spin;
        }

        void Add(Scrap scrap)
        {
            scraps.Add(scrap);
            events.Add(new ScrapThrown { Scrap = scrap });
        }

        // ---- Ships ----

        internal void SetTarget(PlayerId id, Vec2 position) => targets[id.Value] = position;

        internal bool ClearTarget(PlayerId id) => targets.Remove(id.Value);

        /// <summary>The collision with obj_player: the pickup is taken by <paramref name="by"/>.</summary>
        internal bool Collect(PlayerId by, int pickupId)
        {
            var pickup = FindPickup(pickupId);
            if (pickup == null || pickup.IsGone) return false;
            pickup.IsGone = true;
            pickups.Remove(pickup);
            events.Add(new PickupCollected { Pickup = pickup, By = by });
            return true;
        }

        internal bool ClearAllPickups()
        {
            if (pickups.Count == 0) return false;
            foreach (var p in pickups.ToArray()) Remove(p);
            return true;
        }

        internal bool Forget(int id)
        {
            for (int i = 0; i < pickups.Count; i++)
                if (pickups[i].Id == id) { pickups[i].IsGone = true; pickups.RemoveAt(i); return true; }
            for (int i = 0; i < scraps.Count; i++)
                if (scraps[i].Id == id) { scraps[i].IsGone = true; scraps.RemoveAt(i); return true; }
            return false;
        }

        void Remove(Pickup pickup)
        {
            pickup.IsGone = true;
            pickups.Remove(pickup);
            events.Add(new PickupRemoved { Pickup = pickup });
        }

        void Remove(Scrap scrap)
        {
            scrap.IsGone = true;
            scraps.Remove(scrap);
            events.Add(new ScrapRemoved { Scrap = scrap });
        }

        // ---- The step ----

        internal void Tick(float steps)
        {
            if (steps <= 0f) return;
            foreach (var pickup in pickups.ToArray()) TickPickup(pickup, steps);
            foreach (var scrap in scraps.ToArray()) TickScrap(scrap, steps);
        }

        void TickPickup(Pickup p, float steps)
        {
            // Alarms (they keep counting while paused; their code checks it where the original's does).
            if (p.Kind == PickupKind.Carrot)
            {
                p.HoverAlarm -= steps;
                while (p.HoverAlarm <= Epsilon)
                {
                    if (!Paused) Hover(p);
                    p.HoverAlarm += Tuning.CarrotHoverEverySteps;
                }
                p.PingAlarm -= steps;
                while (p.PingAlarm <= Epsilon)
                {
                    events.Add(new CarrotPinged { Pickup = p, At = p.Position });
                    p.PingAlarm += Tuning.CarrotPingEverySteps;
                }
            }
            else
            {
                p.SparkleAlarm -= steps;
                while (p.SparkleAlarm <= Epsilon)
                {
                    Sparkle(p);
                    p.SparkleAlarm += Tuning.CoinSparkleEverySteps;
                }
            }

            // Step.
            if (!Paused)
            {
                if (p.Kind == PickupKind.Coin && p.Position.Y < Room.YMin - GameClock.Px(Tuning.CoinHeightPx))
                {
                    Remove(p);
                    return;
                }
                if (NearestTarget(p, out var target))
                {
                    p.Direction = GmlMath.PointDirection(p.Position, target);
                    p.SpeedPxPerStep = Tuning.MagnetSpeedPx;
                }
                else if (p.Kind == PickupKind.Coin)
                {
                    p.Position = new Vec2(p.Position.X, p.Position.Y - GameClock.Px(Tuning.CoinFallPxPerStep * steps));
                }
            }

            // The built-in motion.
            if (p.SpeedPxPerStep != 0f)
                p.Position += GmlMath.Dir(p.Direction) * GameClock.Px(p.SpeedPxPerStep * steps);
        }

        /// <summary>obj_carrot's Alarm 0: hoverY swings between -hoverAmount and hoverAmount, and <c>y += hoverY</c>.</summary>
        void Hover(Pickup p)
        {
            float amount = Tuning.CarrotHoverAmountPx, speed = Tuning.CarrotHoverSpeedPx;
            if (p.HoverUp)
            {
                if (p.HoverYPx < amount) p.HoverYPx += speed; else p.HoverUp = false;
            }
            else
            {
                if (p.HoverYPx > -amount) p.HoverYPx -= speed; else p.HoverUp = true;
            }
            p.Position = new Vec2(p.Position.X, p.Position.Y - GameClock.Px(p.HoverYPx)); // GML y grows downward
        }

        /// <summary>obj_coin's Alarm 0: a spark one step behind its motion, jittered 2 px, amber, pink or cornflower blue.</summary>
        void Sparkle(Pickup p)
        {
            int color = rng.IRandom(1, 3);
            int dx = rng.IRandom(-Tuning.CoinSparkleJitterPx, Tuning.CoinSparkleJitterPx);
            int dy = rng.IRandom(-Tuning.CoinSparkleJitterPx, Tuning.CoinSparkleJitterPx);
            Vec2 velocity = GmlMath.Dir(p.Direction) * GameClock.Px(p.SpeedPxPerStep);
            var at = p.Position - velocity + new Vec2(GameClock.Px(dx), -GameClock.Px(dy));
            events.Add(new CoinSparkled
            {
                Pickup = p,
                At = at,
                Color = color == 1 ? SparkleColor.Amber : color == 2 ? SparkleColor.Pink : SparkleColor.CornflowerBlue
            });
        }

        /// <summary><c>distance_to_point(player.x, player.y) &lt; 48</c>, from the pickup's bounding box, for the nearest ship.</summary>
        bool NearestTarget(Pickup p, out Vec2 target)
        {
            target = default;
            float best = float.MaxValue;
            var box = p.Kind == PickupKind.Carrot ? Tuning.CarrotBox : Tuning.CoinBox;
            foreach (var pair in targets)
            {
                float d = DistanceToPointPx(p.Position, box, pair.Value);
                if (d < Tuning.MagnetRangePx && d < best)
                {
                    best = d;
                    target = pair.Value;
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>GML distance_to_point: from the box (left, top, right, bottom about the origin, GML px, y down) to the point.</summary>
        public static float DistanceToPointPx(Vec2 origin, float[] box, Vec2 point)
        {
            float x = (point.X - origin.X) * GameClock.PixelsPerUnit;
            float y = -(point.Y - origin.Y) * GameClock.PixelsPerUnit; // GML y down
            float dx = Math.Max(0f, Math.Max(box[0] - x, x - box[2]));
            float dy = Math.Max(0f, Math.Max(box[1] - y, y - box[3]));
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        void TickScrap(Scrap s, float steps)
        {
            float bottom = Room.YMin - GameClock.Px(s.HeightPx);

            // Alarm 0: x += velocityX; y -= velocityY; velocityY -= 1; y += 1 (gravity); gone past the room's bottom.
            s.MoveAlarm -= steps;
            while (s.MoveAlarm <= Epsilon)
            {
                s.MoveAlarm += s.MoveEverySteps;
                if (Paused) continue;
                s.Position = new Vec2(s.Position.X + GameClock.Px(s.VelocityXPx), s.Position.Y + GameClock.Px(s.VelocityYPx));
                s.VelocityYPx -= Tuning.ScrapVelocityLossPx;
                s.Position = new Vec2(s.Position.X, s.Position.Y - GameClock.Px(Tuning.ScrapGravityPx));
                if (s.Position.Y < bottom)
                {
                    Remove(s);
                    return;
                }
            }

            // Step: image_angle += spin.
            if (Paused) return;
            s.ImageAngle += s.Spin * steps;
            if (s.Position.Y < bottom) Remove(s);
        }
    }
}
