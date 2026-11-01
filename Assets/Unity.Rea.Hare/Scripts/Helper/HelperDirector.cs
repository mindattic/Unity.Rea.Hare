using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Audio;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Pickups;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Rea.Hare.Helper
{
    /// <summary>Steps the helper field once per physics step, after the pickups (whose collision events it raises).</summary>
    [DefaultExecutionOrder(1002)]
    sealed class HelperDriver : MonoBehaviour
    {
        void Awake()
        {
            if (HelperDirector.HasOtherDriver(this)) { Destroy(gameObject); return; }
            HelperDirector.SetDriver(this);
        }

        void OnDestroy() => HelperDirector.ClearDriver(this);

        float lastSteppedAt = -1f;

        void FixedUpdate()
        {
            if (Time.fixedTime == lastSteppedAt) return;
            lastSteppedAt = Time.fixedTime;
            HelperDirector.Step(Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// The scene's one authority over Core's <see cref="HelperField"/> (gmlHARE's create_helper and obj_helper, U.HARE-T66):
    /// it runs the commands the ships' keys send (<see cref="PlayerShipController"/>'s Helper map), tells the field what each
    /// helper reads from its ship (<c>player.x</c>, <c>player.speed</c>, its box, the mouse), view 0 and paused, steps it,
    /// shows each helper (<see cref="HelperView"/>), finds the pickups its mask touches (obj_carrot's and obj_coin's
    /// collision with obj_helper, which is their collision with obj_player: the pickups' own <see cref="CollectPickup"/> for
    /// the ship that owns it) and plays what that raises: sfx_hawk, the ef_ring, ef_smoke, ef_smokeup and ef_spark effects
    /// and Delete's obj_rocket_explosion with its black ef_smokeup and orange ef_firework.
    /// </summary>
    public static class HelperDirector
    {
        static HelperField field;
        static int seed = Environment.TickCount;
        static bool seedPinned;
        static HelperDriver driver;
        static readonly Dictionary<int, HelperView> views = new Dictionary<int, HelperView>();
        static readonly List<PlayerShipController> ships = new List<PlayerShipController>();

        /// <summary>Every event the field raised, after it was played (tests).</summary>
        public static event Action<HelperEvent> EventPlayed;

        /// <summary>Every sound a helper played, by GML name.</summary>
        public static event Action<string> SoundPlayed;

        public static HelperField Field => field ?? (field = new HelperField(new SeededRandom(seed), GameContent.Current.Helper));

        /// <summary>A fresh field (and optionally a pinned seed); every view is forgotten.</summary>
        public static void Reset(int? newSeed = null)
        {
            seedPinned = newSeed.HasValue;
            if (newSeed.HasValue) seed = newSeed.Value;
            field = null;
            views.Clear();
            ships.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            if (!seedPinned) seed = Environment.TickCount;
            Reset();
            if (driver != null) { Object.Destroy(driver.gameObject); driver = null; }
        }

        static void EnsureDriver()
        {
            if (driver != null || !Application.isPlaying) return;
            var go = new GameObject("HelperDirector") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<HelperDriver>();
        }

        internal static bool HasOtherDriver(HelperDriver self) => driver != null && driver != self;
        internal static void SetDriver(HelperDriver self) => driver = self;
        internal static void ClearDriver(HelperDriver self)
        {
            if (driver == self) driver = null;
        }

        // ---- What the ships send ----

        /// <summary>create_helper for ship <paramref name="owner"/> at <paramref name="at"/>, heading <paramref name="imageAngle"/>.</summary>
        public static bool Call(PlayerId owner, Vector2 at, float imageAngle) =>
            Execute(new CallHelper(owner, at.ToVec2(), imageAngle)).Accepted;

        /// <summary>The held arrows of ship <paramref name="owner"/> (nothing to do while it has no helper).</summary>
        public static void HoldTurn(PlayerId owner, bool counterclockwise, bool clockwise)
        {
            if (field == null || field.Helpers.Count == 0) return;
            Execute(new HoldHelperTurn(owner, counterclockwise, clockwise));
        }

        /// <summary>Runs a command on the field and plays its events.</summary>
        public static HelperResult Execute(HelperCommand command)
        {
            EnsureDriver();
            var result = Field.Execute(command);
            Play(result);
            return result;
        }

        // ---- The step ----

        /// <summary>One physics step: paused, the view and the ships to the field, its step, the views, then the pickups it touches.</summary>
        public static void Step(float dt)
        {
            if (field == null || field.Helpers.Count == 0) return;
            var f = field;
            if (f.Paused != Gml.Paused) f.Execute(new SetHelpersPaused(Gml.Paused));
            f.Execute(new SetHelperView(CurrentView()));
            FindShips();
            foreach (var ship in ships) f.Execute(new SetHelperOwner(SeatOf(ship), OwnerOf(ship)));

            Execute(new TickHelpers(dt));
            Present();
            Collide();
        }

        /// <summary>Puts every view where its helper is.</summary>
        public static void Present()
        {
            foreach (var v in views.Values) if (v != null) v.Present();
        }

        /// <summary>obj_carrot's and obj_coin's collision events with obj_helper: each helper takes every pickup its mask touches.</summary>
        static void Collide()
        {
            if (PickupDirector.Field.Pickups.Count == 0) return;
            foreach (var view in views.Values)
            {
                if (view == null || view.Helper == null || view.Helper.IsGone) continue;
                foreach (var hit in view.MaskHits())
                {
                    var pickup = hit != null ? hit.GetComponent<PickupView>() : null;
                    if (pickup == null || pickup.Pickup == null || pickup.Pickup.IsGone) continue;
                    PickupDirector.Execute(new CollectPickup(view.Helper.Owner, pickup.Pickup.Id));
                }
            }
        }

        static PlayerId SeatOf(PlayerShipController ship) => PlayerSeat.Of(ship.gameObject).Id;

        static void FindShips()
        {
            ships.Clear();
            ships.AddRange(Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None));
            ships.Sort((a, b) => SeatOf(a).Value.CompareTo(SeatOf(b).Value));
        }

        /// <summary>
        /// What obj_helper reads from <c>player</c>: x, y; speed (px a step; 0 perched); its bounding box, the ship's mask
        /// (spr_rabbit_leap's ellipse flying, spr_rabbit_idle's box perched) turned to image_angle; and mouse_x, mouse_y.
        /// </summary>
        static HelperOwner OwnerOf(PlayerShipController ship)
        {
            Vector2 at = ship.transform.position;
            bool perched = ship.State == PlayerFlightState.Idle;
            float speedPx = perched ? 0f : ship.Speed * GameClock.PixelsPerUnit / GameClock.StepsPerSecond;
            var size = perched ? PerchProbe.PerchMaskSizePx : PerchProbe.FlightMaskSizePx;
            var offset = perched ? PerchProbe.PerchMaskOffsetPx : PerchProbe.FlightMaskOffsetPx;
            var turn = Quaternion.Euler(0f, 0f, ship.Facing);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                var corner = offset + new Vector2((i & 1) == 0 ? -size.x / 2f : size.x / 2f, (i & 2) == 0 ? -size.y / 2f : size.y / 2f);
                Vector2 world = at + (Vector2)(turn * new Vector2(Gml.Px(corner.x), Gml.Px(corner.y)));
                min = Vector2.Min(min, world);
                max = Vector2.Max(max, world);
            }
            Vec2? mouse = ship.AimPoint.HasValue ? ship.AimPoint.Value.ToVec2() : (Vec2?)null;
            return new HelperOwner(at.ToVec2(), speedPx, new RoomBounds(min.x, min.y, max.x, max.y), mouse);
        }

        /// <summary>View 0 of the play room running, in the world; with none, the main camera's; else none (all in view).</summary>
        static RoomBounds? CurrentView()
        {
            var arena = Arena.ArenaRoom.Active;
            if (arena != null && arena.State != null)
            {
                var view = arena.State.View;
                Vector2 topLeft = arena.ToWorld(view.X, view.Y);
                Vector2 bottomRight = arena.ToWorld(view.X + view.Width, view.Y + view.Height);
                return new RoomBounds(topLeft.x, bottomRight.y, bottomRight.x, topLeft.y);
            }
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return null;
            float h = camera.orthographicSize, w = h * camera.aspect;
            Vector2 c = camera.transform.position;
            return new RoomBounds(c.x - w, c.y - h, c.x + w, c.y + h);
        }

        /// <summary>A view went away from outside (its scene unloaded): its helper leaves the field quietly.</summary>
        internal static void Dispose(int id, HelperView view)
        {
            if (!views.TryGetValue(id, out var registered) || !ReferenceEquals(registered, view)) return;
            views.Remove(id);
            if (field != null) field.Execute(new ForgetHelper(id));
        }

        /// <summary>The live view of a helper, or null.</summary>
        public static HelperView ViewOf(int id) => views.TryGetValue(id, out var v) && v != null ? v : null;

        // ---- Playing events ----

        static void Play(HelperResult result)
        {
            foreach (var e in result.Events)
            {
                PlayEvent(e);
                EventPlayed?.Invoke(e);
            }
        }

        static void PlayEvent(HelperEvent e)
        {
            var tuning = Field.Tuning;
            switch (e)
            {
                case HelperCalled called:
                    views[called.Helper.Id] = HelperView.Create(called.Helper, tuning);
                    PlaySound(tuning.CallSound);
                    break;
                case HelperRinged ring:
                    GmlEffects.CreateBelow(GmlEffectKind.Ring, ring.At.ToVector2(), tuning.RingSize, Color.white);
                    break;
                case HelperSmoked smoke:
                    GmlEffects.CreateBelow(GmlEffectKind.Smoke, smoke.At.ToVector2(), tuning.SmokeSize,
                        smoke.Returning ? DeathEffects.Orange : PickupDirector.Gold);
                    break;
                case HelperTrailed trail:
                    GmlEffects.CreateBelow(GmlEffectKind.SmokeUp, trail.At.ToVector2(), tuning.TrailSize, Color.white);
                    break;
                case HelperSparked spark:
                    GmlEffects.CreateBelow(GmlEffectKind.Spark, spark.At.ToVector2(), tuning.SparkSize, PickupDirector.ColorOf(spark.Color));
                    break;
                case HelperExploded blast:
                    var at = blast.At.ToVector2();
                    new GameObject("RocketExplosion").AddComponent<RocketExplosion>().Init(at);
                    GmlEffects.CreateAbove(GmlEffectKind.SmokeUp, at, tuning.ExplosionEffectSize, Color.black);
                    GmlEffects.CreateAbove(GmlEffectKind.Firework, at, tuning.ExplosionEffectSize, DeathEffects.Orange);
                    Destroy(blast.Helper.Id);
                    break;
            }
        }

        /// <summary>audio_play_sound(name, 1, false) through <see cref="HareAudio"/>.</summary>
        static void PlaySound(string name)
        {
            HareAudio.Play(name);
            SoundPlayed?.Invoke(name);
        }

        static void Destroy(int id)
        {
            if (!views.TryGetValue(id, out var view)) return;
            views.Remove(id);
            if (view == null) return;
            if (Application.isPlaying) Object.Destroy(view.gameObject); else Object.DestroyImmediate(view.gameObject);
        }
    }
}
