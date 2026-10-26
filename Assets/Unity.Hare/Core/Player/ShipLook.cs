using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// obj_player's presentation numbers (Create and its Draw/Step/Alarm 2/Alarm 3 events), in the original's steps and
    /// pixels at room_gamescreen's 120 steps a second.
    /// </summary>
    [Serializable]
    public class LookTuning
    {
        /// <summary>smoke_delay: Alarm 2's smoke trail puffs every 40 steps.</summary>
        public float SmokeDelaySteps = 40f;
        /// <summary>spark_delay: Alarm 3's spark trail sparks every 8 steps while turning.</summary>
        public float SparkDelaySteps = 8f;
        /// <summary>Alarm 3: <c>rnd_x = irandom_range(-4, 4)</c>, <c>rnd_y</c> likewise.</summary>
        public int SparkJitterPx = 4;
        /// <summary>The justspawned smoke jet's <c>Len = 36</c> behind the ship (image_angle - 180).</summary>
        public float JetOffsetPx = 36f;
        /// <summary>obj_player Create's <c>scale = 1</c>: Draw's draw_sprite_ext(sprite_index, 0, x, y, scale, scale, ...).</summary>
        public float Scale = 1f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".SmokeDelaySteps", SmokeDelaySteps);
            ValidationHelpers.Positive(errors, path + ".SparkDelaySteps", SparkDelaySteps);
            ValidationHelpers.NonNegative(errors, path + ".SparkJitterPx", SparkJitterPx);
            ValidationHelpers.NonNegative(errors, path + ".JetOffsetPx", JetOffsetPx);
            ValidationHelpers.Positive(errors, path + ".Scale", Scale);
        }
    }

    /// <summary>Alarm 2: an ef_smokeup (size 0, c_white) one step behind a flying ship (<c>x - hspeed, y - vspeed</c>).</summary>
    public sealed class SmokeTrailPuffed : PlayerEvent { }

    /// <summary>
    /// Alarm 3 while turning: an ef_spark (size 0) in <see cref="Color"/> (amber, pink or cornflower blue) one step behind
    /// the ship, <see cref="JitterXPx"/>/<see cref="JitterYPx"/> GML px off it (y grows downward).
    /// </summary>
    public sealed class SparkTrailSparked : PlayerEvent
    {
        public ShipColor Color;
        public int JitterXPx;
        public int JitterYPx;
    }

    /// <summary>obj_player's Step while justspawned: an orange ef_smoke (size 0) 36 px behind the ship, every step.</summary>
    public sealed class SpawnJetPuffed : PlayerEvent { }

    /// <summary>
    /// What obj_player draws and the trails it leaves, as numbers and events (presentation reads this and never writes):
    /// <list type="bullet">
    /// <item>the pose (<see cref="PlayerShip.Pose"/>) in <see cref="ShipHealth.Color"/>, at <see cref="Scale"/>, hidden while dead;</item>
    /// <item>the held ground weapon, <see cref="HeldWeapon"/> (ground_weapon_asset: Create's vulcan, then whatever
    /// next_ground_weapon equipped or a shoot_* script last fired, the secondary shotgun included), drawn only while
    /// perched (state_idle), at gunX/gunY and turned to mouse_angle;</item>
    /// <item>Alarm 2's smoke trail: armed by reset_player and player_leap, it re-arms itself every smoke_delay steps for
    /// good and puffs (<see cref="SmokeTrailPuffed"/>) whenever it rings on a flying ship;</item>
    /// <item>Alarm 3's spark trail: armed by reset_player, player_leap and the turn starting (rotate_* when !turning), it
    /// sparks (<see cref="SparkTrailSparked"/>) and re-arms every spark_delay steps for as long as the ship is turning;</item>
    /// <item>the justspawned jet: an orange ef_smoke behind the ship every step while it is spawn-protected
    /// (<see cref="SpawnJetPuffed"/>).</item>
    /// </list>
    /// It steps with the flight (<see cref="StepFlight"/>), counting whole GML steps however long the physics step is.
    /// </summary>
    public sealed class ShipLook
    {
        const float Epsilon = 1e-4f;

        readonly EventSink sink;
        readonly ShipFlight flight;
        readonly ShipHealth health;
        readonly Func<float, float, float> random;
        LookTuning tuning = GameContent.Current.Look;
        float smokeTimer;
        float sparkTimer;
        float jetClock;
        bool wasTurning;

        internal ShipLook(EventSink sink, ShipFlight flight, ShipHealth health, Func<float, float, float> random)
        {
            this.sink = sink;
            this.flight = flight;
            this.health = health;
            this.random = random;
            sink.Emitted += Observe;
        }

        public LookTuning Tuning => tuning;

        /// <summary>GML ground_weapon_asset, as the weapon it shows.</summary>
        public GroundWeapon HeldWeapon { get; private set; } = GroundWeapon.Vulcan;

        /// <summary>The GML sprite of <see cref="HeldWeapon"/> (spr_weapon_vulcan ...).</summary>
        public string HeldWeaponSprite => WeaponSprite(HeldWeapon);

        /// <summary>obj_player's scale: the pose is drawn at this x and y scale.</summary>
        public float Scale => tuning.Scale;

        /// <summary>Draw: <c>if(state != state_dead)</c>.</summary>
        public bool IsDrawn => flight.State != PlayerFlightState.Dead;

        /// <summary>Draw draws ground_weapon_asset only while <c>state == state_idle</c> (the air weapon's line is commented out).</summary>
        public bool ShowsGun => flight.State == PlayerFlightState.Idle;

        /// <summary>Whether Alarm 2 is armed (it is, for good, once reset_player or player_leap has run).</summary>
        public bool SmokeTrailArmed => smokeTimer > 0f;

        /// <summary>Whether Alarm 3 is armed.</summary>
        public bool SparkTrailArmed => sparkTimer > 0f;

        /// <summary>The GML sprite obj_player draws for ground weapon <paramref name="weapon"/> (ground_weapon_asset).</summary>
        public static string WeaponSprite(GroundWeapon weapon)
        {
            switch (weapon)
            {
                case GroundWeapon.Laser: return "spr_weapon_laser";
                case GroundWeapon.Railgun: return "spr_weapon_railgun";
                case GroundWeapon.Shotgun: return "spr_weapon_shotgun";
                case GroundWeapon.Flamer: return "spr_weapon_flamer";
                default: return "spr_weapon_vulcan";
            }
        }

        /// <summary>
        /// obj_player Create (the vulcan in hand, no alarm armed) followed by setup_level's reset_player (both trail
        /// alarms armed).
        /// </summary>
        internal void Reset(LookTuning newTuning)
        {
            tuning = newTuning ?? GameContent.Current.Look;
            HeldWeapon = GroundWeapon.Vulcan;
            jetClock = 0f;
            wasTurning = false;
            ArmTrails();
        }

        /// <summary>reset_player / player_leap: <c>alarm[alarm_smoke_trail] = smoke_delay; alarm[alarm_spark_trail] = spark_delay</c>.</summary>
        void ArmTrails()
        {
            smokeTimer = GameClock.Seconds(tuning.SmokeDelaySteps);
            sparkTimer = GameClock.Seconds(tuning.SparkDelaySteps);
        }

        /// <summary>Follows the events that change what obj_player shows (a shot, a switch, a leap, a respawn).</summary>
        void Observe(PlayerEvent e)
        {
            switch (e)
            {
                case GroundWeaponSwitched switched:
                    HeldWeapon = switched.Weapon; // next_ground_weapon: ground_weapon_asset = spr_weapon_*
                    break;
                case GunFired fired:
                    var kind = WeaponTable.GroundKindOf(fired.Type);
                    if (kind.HasValue) HeldWeapon = kind.Value; // shoot_*: ground_weapon_asset = spr_weapon_*
                    break;
                case ShipLeapt _:
                case PlayerRespawned _:
                    ArmTrails();
                    break;
            }
        }

        /// <summary>One physics step of <paramref name="dt"/> seconds, after the flight's.</summary>
        internal void Step(float dt)
        {
            // GameMaker counts the alarms down before the key events of a step, so they tick first.
            TickSmoke(dt);
            TickSpark(dt);

            // rotate_clockwise / rotate_counterclockwise: if(!turning) { turning = true; alarm[alarm_spark_trail] = spark_delay; }
            bool turning = flight.IsTurning;
            if (turning && !wasTurning) sparkTimer = GameClock.Seconds(tuning.SparkDelaySteps);
            wasTurning = turning;

            TickJet(dt);
        }

        /// <summary>Alarm 2: <c>if(state == state_moving) effect_create_below(ef_smokeup, ...); alarm[2] = smoke_delay</c>.</summary>
        void TickSmoke(float dt)
        {
            if (smokeTimer <= 0f) return;
            smokeTimer -= dt;
            float every = GameClock.Seconds(tuning.SmokeDelaySteps);
            while (smokeTimer <= Epsilon)
            {
                if (flight.State == PlayerFlightState.Moving) sink.Emit(new SmokeTrailPuffed());
                smokeTimer += every;
            }
        }

        /// <summary>Alarm 3: <c>if(turning) { the spark; alarm[3] = spark_delay; }</c>, so it stops once the turn does.</summary>
        void TickSpark(float dt)
        {
            if (sparkTimer <= 0f) return;
            sparkTimer -= dt;
            float every = GameClock.Seconds(tuning.SparkDelaySteps);
            while (sparkTimer <= Epsilon)
            {
                if (!flight.IsTurning)
                {
                    sparkTimer = 0f;
                    return;
                }
                int pick = PickWhole(1, 3);
                sink.Emit(new SparkTrailSparked
                {
                    Color = pick == 1 ? ShipColor.Amber : pick == 2 ? ShipColor.Pink : ShipColor.CornflowerBlue,
                    JitterXPx = PickWhole(-tuning.SparkJitterPx, tuning.SparkJitterPx),
                    JitterYPx = PickWhole(-tuning.SparkJitterPx, tuning.SparkJitterPx),
                });
                sparkTimer += every;
            }
        }

        /// <summary>obj_player's Step: <c>if(justspawned) effect_create_below(ef_smoke, jetX, jetY, 0, c_orange)</c>, every step.</summary>
        void TickJet(float dt)
        {
            if (!health.IsSpawnProtected || flight.State == PlayerFlightState.Dead)
            {
                jetClock = 0f;
                return;
            }
            jetClock += dt * GameClock.StepsPerSecond;
            while (jetClock >= 1f - Epsilon)
            {
                jetClock -= 1f;
                sink.Emit(new SpawnJetPuffed());
            }
        }

        /// <summary>GML irandom_range(min, max): a whole number from min to max inclusive.</summary>
        int PickWhole(int min, int max)
        {
            if (max <= min) return min;
            int value = (int)Math.Floor(random(min, max + 1));
            return Math.Max(min, Math.Min(max, value));
        }
    }
}
