using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Audio;
using Unity.Hare.Content;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Session;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Pickups
{
    /// <summary>Steps the pickup field once per physics step, after the enemies (whose kills drop pickups).</summary>
    [DefaultExecutionOrder(1001)]
    sealed class PickupDriver : MonoBehaviour
    {
        void Awake()
        {
            if (PickupDirector.HasOtherDriver(this)) { Destroy(gameObject); return; }
            PickupDirector.SetDriver(this);
        }

        void OnDestroy() => PickupDirector.ClearDriver(this);

        float lastSteppedAt = -1f;

        void FixedUpdate()
        {
            if (Time.fixedTime == lastSteppedAt) return;
            lastSteppedAt = Time.fixedTime;
            PickupDirector.Step(Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// The scene's one authority over Core's <see cref="PickupField"/> (gmlHARE's par_pickup, obj_chip and obj_cartridge):
    /// it runs the commands the game sends (an enemy kill's random_pickup, a leap's or rock landing's spawn_chips, a
    /// vulcan round's cartridge and a shotgun blast's shell), steps the field, shows it, finds which pickups each ship's
    /// mask touches and plays what that raises: the carrot's HP through the ship's seat (<see cref="RestoreHealth"/>), the
    /// coin's COINS through the room flow's session (<see cref="CollectCoin"/>), the ef_ring, ef_spark and ef_firework
    /// effects and sfx_powerup / sfx_extralife through <see cref="HareAudio"/>. The field is dealt by one seeded random
    /// source, so a seed reproduces a run.
    /// </summary>
    public static class PickupDirector
    {
        /// <summary>GML c_red, c_silver and c_gray, and globals.gml's c_cornflowerblue, c_amber, c_pink and c_gold.</summary>
        public static readonly Color Red = new Color32(255, 0, 0, 255);
        public static readonly Color Silver = new Color32(192, 192, 192, 255);
        public static readonly Color Gray = new Color32(128, 128, 128, 255);
        public static readonly Color CornflowerBlue = new Color32(154, 206, 235, 255);
        public static readonly Color Amber = new Color32(255, 191, 0, 255);
        public static readonly Color Pink = new Color32(255, 192, 203, 255);
        public static readonly Color Gold = new Color32(255, 223, 0, 255);

        /// <summary>The sounds the pickups play.</summary>
        public const string PowerUpSound = "sfx_powerup";
        public const string ExtraLifeSound = "sfx_extralife";

        static PickupField field;
        static int seed = Environment.TickCount;
        static bool seedPinned;
        static PickupDriver driver;
        static Arena.ArenaRoom arena;
        static readonly Dictionary<int, Object> views = new Dictionary<int, Object>();
        static readonly List<PlayerShipController> ships = new List<PlayerShipController>();
        static readonly List<PlayerId> targeted = new List<PlayerId>();

        /// <summary>Every event the field raised, after it was played (tests, a HUD).</summary>
        public static event Action<PickupEvent> EventPlayed;

        /// <summary>Every sound a pickup played, by GML name.</summary>
        public static event Action<string> SoundPlayed;

        public static int Seed => seed;

        public static PickupField Field => field ?? (field = new PickupField(new SeededRandom(seed), GameContent.Current.Pickups));

        /// <summary>A fresh field (and optionally a pinned seed); every view is forgotten.</summary>
        public static void Reset(int? newSeed = null)
        {
            seedPinned = newSeed.HasValue;
            if (newSeed.HasValue) seed = newSeed.Value;
            field = null;
            arena = null;
            views.Clear();
            ships.Clear();
            targeted.Clear();
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
            var go = new GameObject("PickupDirector") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<PickupDriver>();
        }

        internal static bool HasOtherDriver(PickupDriver self) => driver != null && driver != self;
        internal static void SetDriver(PickupDriver self) => driver = self;
        internal static void ClearDriver(PickupDriver self)
        {
            if (driver == self) driver = null;
        }

        // ---- What the game sends ----

        /// <summary>GML random_pickup(x, y), an enemy's kill branch.</summary>
        public static PickupResult DropRandom(Vector2 at) => Execute(new DropRandomPickup(at.ToVec2()));

        /// <summary>instance_create(x, y, obj_carrot / obj_coin).</summary>
        public static PickupResult Drop(PickupKind kind, Vector2 at) => Execute(new DropPickup(kind, at.ToVec2()));

        /// <summary>
        /// player_leap's and the par_rock landing's kicked-up dirt: <c>effect_create_above(ef_firework, feetX, feetY, 0,
        /// c_gray)</c> then spawn_chips(feetX, feetY), the feet 32 px along <paramref name="imageAngle"/> from the ship.
        /// </summary>
        public static PickupResult KickUpDirt(Vector2 shipAt, float imageAngle)
        {
            var feet = Field.FeetOf(shipAt.ToVec2(), imageAngle).ToVector2();
            GmlEffects.CreateAbove(GmlEffectKind.Firework, feet, 0, Gray);
            return Execute(new KickUpChips(shipAt.ToVec2(), imageAngle));
        }

        /// <summary>shoot_vulcan's obj_cartridge or shoot_shotgun's shell at the gun (gunX, gunY).</summary>
        public static PickupResult EjectCasing(ScrapKind kind, Vector2 gun) => Execute(new Pickups.EjectCasing(kind, gun.ToVec2()));

        /// <summary>setup_level's <c>with(par_pickup) instance_destroy()</c>.</summary>
        public static void ClearPickups()
        {
            if (field != null) Execute(new Pickups.ClearPickups());
        }

        /// <summary>Runs a command on the field and plays its events.</summary>
        public static PickupResult Execute(PickupCommand command)
        {
            EnsureDriver();
            var result = Field.Execute(command);
            Play(result);
            return result;
        }

        // ---- The step ----

        /// <summary>One physics step: the room, the pause and the ships to the field, its step, the views, then the collisions.</summary>
        public static void Step(float dt)
        {
            if (field == null || (field.Pickups.Count == 0 && field.Scraps.Count == 0)) return;
            var f = field;
            if (f.Paused != Gml.Paused) f.Execute(new SetPickupsPaused(Gml.Paused));
            f.Execute(new SetPickupRoom(CurrentRoom()));
            FindShips();
            foreach (var ship in ships) f.Execute(new SetPickupTarget(SeatOf(ship), ((Vector2)ship.transform.position).ToVec2()));

            Execute(new TickPickups(dt));
            Present();
            Collide();
        }

        /// <summary>Puts every view where its Core body is.</summary>
        public static void Present()
        {
            foreach (var v in views.Values)
            {
                if (v is PickupView p && p != null) p.Present();
                else if (v is ScrapView s && s != null) s.Present();
            }
        }

        /// <summary>The pickups' collision events with obj_player: each ship takes every pickup its mask touches.</summary>
        static void Collide()
        {
            if (field.Pickups.Count == 0) return;
            foreach (var ship in ships)
            {
                if (ship == null) continue;
                var touched = PerchProbe.ShipMaskHits(ship.transform.position, ship.Facing, ship.State == PlayerFlightState.Idle);
                foreach (var hit in touched)
                {
                    var view = hit != null ? hit.GetComponent<PickupView>() : null;
                    if (view == null || view.Pickup == null || view.Pickup.IsGone) continue;
                    Execute(new CollectPickup(SeatOf(ship), view.Pickup.Id));
                }
            }
        }

        static PlayerId SeatOf(PlayerShipController ship) => PlayerSeat.Of(ship.gameObject).Id;

        /// <summary>Every ship in play (GML player), in seat order; ships that are gone leave the magnet.</summary>
        static void FindShips()
        {
            foreach (var id in targeted) field.Execute(new ClearPickupTarget(id));
            targeted.Clear();
            ships.Clear();
            ships.AddRange(Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None));
            ships.Sort((a, b) => SeatOf(a).Value.CompareTo(SeatOf(b).Value));
            foreach (var ship in ships) targeted.Add(SeatOf(ship));
        }

        /// <summary>The room the arena plays in (room_width x room_height), else room_gamescreen.</summary>
        static RoomBounds CurrentRoom()
        {
            if (arena == null) arena = Object.FindAnyObjectByType<Arena.ArenaRoom>();
            if (arena == null || arena.State == null) return Gml.Room.ToBounds();
            Vector2 topLeft = arena.ToWorld(0f, 0f);
            Vector2 bottomRight = arena.ToWorld(arena.State.Width, arena.State.Height);
            return new RoomBounds(topLeft.x, bottomRight.y, bottomRight.x, topLeft.y);
        }

        /// <summary>A view went away from outside (its scene unloaded): its body leaves the field quietly.</summary>
        internal static void Dispose(int id, Object view)
        {
            if (!views.TryGetValue(id, out var registered) || !ReferenceEquals(registered, view)) return;
            views.Remove(id);
            if (field != null) field.Execute(new ForgetPickup(id));
        }

        /// <summary>The live view of a pickup or scrap, or null.</summary>
        public static T ViewOf<T>(int id) where T : Object => views.TryGetValue(id, out var v) && v != null ? v as T : null;

        // ---- Playing events ----

        static void Play(PickupResult result)
        {
            foreach (var e in result.Events)
            {
                PlayEvent(e);
                EventPlayed?.Invoke(e);
            }
        }

        static void PlayEvent(PickupEvent e)
        {
            switch (e)
            {
                case PickupDropped dropped:
                    views[dropped.Pickup.Id] = PickupView.Create(dropped.Pickup);
                    break;
                case ScrapThrown thrown:
                    views[thrown.Scrap.Id] = ScrapView.Create(thrown.Scrap);
                    break;
                case CarrotPinged ping:
                    GmlEffects.CreateBelow(GmlEffectKind.Ring, ping.At.ToVector2(), 0, Red);
                    break;
                case CoinSparkled sparkle:
                    GmlEffects.CreateBelow(GmlEffectKind.Spark, sparkle.At.ToVector2(), 0, ColorOf(sparkle.Color));
                    break;
                case PickupCollected taken:
                    Collected(taken);
                    Destroy(taken.Pickup.Id);
                    break;
                case PickupRemoved removed:
                    Destroy(removed.Pickup.Id);
                    break;
                case ScrapRemoved gone:
                    Destroy(gone.Scrap.Id);
                    break;
            }
        }

        /// <summary>The carrot's and coin's collision event with obj_player, after the field let the ship have it.</summary>
        static void Collected(PickupCollected taken)
        {
            var pickup = taken.Pickup;
            var at = pickup.Position.ToVector2();
            if (pickup.Kind == PickupKind.Carrot)
            {
                // if (player.HP < player.MAXHP) player.HP += 1; sfx_powerup; effect_create_below(ef_ring, x, y, 1, c_red).
                var ship = ShipOf(taken.By);
                if (ship != null) PlayerSeat.Of(ship.gameObject).Send(new RestoreHealth(taken.By, pickup.Value));
                PlaySound(PowerUpSound);
                GmlEffects.CreateBelow(GmlEffectKind.Ring, at, 1, Red);
                return;
            }

            // COINS += value; the ring in the coin's colour; at 100 COINS LIVES += 1, COINS = 0, sfx_extralife, else sfx_powerup.
            var tuning = Field.Tuning;
            var ring = pickup.Value == tuning.BlueCoinValue ? CornflowerBlue : pickup.Value == tuning.SilverCoinValue ? Silver : Gold;
            GmlEffects.CreateBelow(GmlEffectKind.Ring, at, 0, ring);
            bool extraLife = false;
            var flow = RoomFlowDirector.Active;
            if (flow != null && flow.Session != null)
            {
                var result = flow.ExecuteSession(new CollectCoin(taken.By, pickup.Value));
                foreach (var se in result.Events) if (se is ExtraLifeEarned) extraLife = true;
            }
            PlaySound(extraLife ? ExtraLifeSound : PowerUpSound);
        }

        /// <summary>audio_play_sound(name, 1, false) through <see cref="HareAudio"/> (it honours the session's mute).</summary>
        static void PlaySound(string name)
        {
            HareAudio.Play(name);
            SoundPlayed?.Invoke(name);
        }

        static PlayerShipController ShipOf(PlayerId id)
        {
            foreach (var ship in ships) if (ship != null && SeatOf(ship).Equals(id)) return ship;
            foreach (var ship in Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None))
                if (SeatOf(ship).Equals(id)) return ship;
            return null;
        }

        static void Destroy(int id)
        {
            if (!views.TryGetValue(id, out var view)) return;
            views.Remove(id);
            if (view == null) return;
            var go = ((Component)view).gameObject;
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
        }

        /// <summary>obj_coin's spark colours.</summary>
        public static Color ColorOf(SparkleColor color) =>
            color == SparkleColor.Amber ? Amber : color == SparkleColor.Pink ? Pink : CornflowerBlue;
    }
}
