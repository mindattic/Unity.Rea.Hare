using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Rocks
{
    /// <summary>What removed a rock.</summary>
    public enum RockRemoval
    {
        /// <summary>Its colour ran out under the ship's or an enemy's bullets.</summary>
        Bullet,
        /// <summary>Its colour ran out under a rocket.</summary>
        Rocket,
        /// <summary>It drifted more than its sprite width outside the room.</summary>
        LeftRoom,
        /// <summary>setup_level's move_rocks or its with(par_rock) instance_destroy().</summary>
        Cleared
    }

    /// <summary>One rock leaving play, raised by <see cref="Rock.Removed"/>.</summary>
    public readonly struct RockGone
    {
        public readonly RockRemoval Cause;
        public readonly Vector2 Position;
        /// <summary>GML current_score += 10 and show_float_text_ext(x, y, "10") when <see cref="AwardsScore"/>.</summary>
        public readonly int Points;
        public readonly bool AwardsScore;

        public RockGone(RockRemoval cause, Vector2 position, int points, bool awardsScore)
        {
            Cause = cause;
            Position = position;
            Points = points;
            AwardsScore = awardsScore;
        }

        /// <summary>Destroyed by a weapon (GML rock_destroyed runs: flotsum, fling, a new rock).</summary>
        public bool Destroyed => Cause == RockRemoval.Bullet || Cause == RockRemoval.Rocket;
    }

    /// <summary>
    /// Ports par_rock (gmlHARE, depth 3; obj_rock1-5 are empty children that no script creates, the
    /// field is all par_rock instances): a drifting, spinning asteroid drawn with one of spr_rock1-5
    /// or spr_asteroid_experiment (precise masks, here the sprite's physics shape).
    /// <list type="bullet">
    /// <item>Create: image_angle and direction 0-359, speed 0 for stationary_percent of rocks and
    /// irandom_range(-15, 15) * 0.1 px/step otherwise, spin irandom_range(-30, 30) * 0.01 degrees/step
    /// (0 within 0.03), image_blend grey RGB 255.</item>
    /// <item>Alarm 0 every room_speed / 4 (30 steps): RGB heals 1 toward 255. Alarm 1 every 30 steps:
    /// spin above original_spin decays by 0.96.</item>
    /// <item>Step (unpaused): speed = Speed, image_angle += spin; more than its sprite width outside
    /// the room it is destroyed and create_new_rock replaces it. Paused: speed 0.</item>
    /// <item>A bullet (any owner) above RGB 128 darkens it 4, pushes it 0.1 px/step along the bullet
    /// (speed capped at 3), adds spin 0.06 and is spent in an obj_tinyexplosion at the bullet plus
    /// the rock's hspeed/vspeed. A rocket darkens it 20, pushes 0.5, spins 0.25 and goes off there
    /// as an obj_rocket_explosion.</item>
    /// <item>At RGB 128 or below the next hit destroys it: 10 points (a bullet's only when it is the
    /// ship's), ef_smokeup and ef_firework size 2, an obj_grenade_explosion, then rock_destroyed.
    /// The bullet still meets its own par_rock event (a tiny explosion, spent); obj_rocket has no
    /// par_rock event, so a rocket that breaks a rock flies on.</item>
    /// </list>
    /// The laser beam is drawn only up to a rock (draw_beam); nothing else (laser and railgun kills,
    /// flame, rocket explosions, shield wall, enemies) touches one. The ship lands on and rides a rock
    /// as an <see cref="IPerchSurface"/> (U.HARE-T53); its <see cref="PlayerShipController"/> listens to
    /// <see cref="Removed"/> for rock_destroyed's and the room edge's fling_player.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Rock : MonoBehaviour, IProjectileTarget, IRocketTarget, IBeamBlocker, IPerchSurface
    {
        public const int Depth = 3;
        public const int FullRgb = 255;
        /// <summary>RGB &gt; 128: the hit only darkens it; at or below, the next destroys it.</summary>
        public const int BreakRgb = 128;
        public const int BulletDarken = 4;
        public const int RocketDarken = 20;
        public const float BulletPushPxPerStep = 0.1f;
        public const float RocketPushPxPerStep = 0.5f;
        public const float MaxPushedSpeedPxPerStep = 3f;
        public const float BulletSpin = 0.06f;
        public const float RocketSpin = 0.25f;
        /// <summary>spin_max, degrees/step.</summary>
        public const float SpinMax = 4f;
        /// <summary>regen_rate and friction_rate: room_speed / 4.</summary>
        public const float AlarmSteps = Gml.StepsPerSecond / 4f;
        public const float SpinFriction = 0.96f;
        /// <summary>a_near_b(spin, 0, 0.03): a spin this small is set to none.</summary>
        public const float NearZeroSpin = 0.03f;
        public const int Points = 10;

        /// <summary>rnd_rock 1-6.</summary>
        public static readonly string[] SpriteNames =
            { "spr_rock1", "spr_rock2", "spr_rock3", "spr_rock4", "spr_rock5", "spr_asteroid_experiment" };
        /// <summary>sprite_get_width per rnd_rock, the rock's size for the boundary check.</summary>
        public static readonly float[] SpriteWidthsPx = { 242f, 186f, 273f, 232f, 197f, 262f };
        /// <summary>Each sprite's bbox (size, and centre from the origin in Unity axes) for the fallback mask without art.</summary>
        static readonly Vector2[] BBoxSizesPx =
            { new Vector2(234f, 159f), new Vector2(182f, 140f), new Vector2(270f, 201f), new Vector2(229f, 186f), new Vector2(193f, 119f), new Vector2(257f, 210f) };
        static readonly Vector2[] BBoxCentresPx =
            { new Vector2(0f, 1.5f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(-0.5f, 0f), new Vector2(-0.5f, 0.5f), new Vector2(-0.5f, 1f) };

        Rect room;
        Rigidbody2D body;
        SpriteRenderer spriteRenderer;
        float direction;
        float imageAngle;
        float regenAlarm;
        float frictionAlarm;
        bool gone;

        /// <summary>Raised once as any rock leaves play, before it is destroyed.</summary>
        public static event Action<Rock, RockGone> Removed;

        /// <summary>The field this rock belongs to (rock_destroyed and create_new_rock run there); null for a lone rock.</summary>
        public RockField Field { get; set; }

        public Rect Room => room;
        public bool IsGone => gone;
        public Vector2 Position => transform.position;

        /// <summary>rnd_rock - 1: index into <see cref="SpriteNames"/>.</summary>
        public int Kind { get; private set; }
        public string SpriteName => SpriteNames[Kind];
        /// <summary>GML size = sprite_get_width(sprite_index).</summary>
        public float SizePx => SpriteWidthsPx[Kind];

        /// <summary>GML RGB: the image_blend grey, 255 whole, darkened by hits and healed by Alarm 0.</summary>
        public int Rgb { get; private set; } = FullRgb;

        /// <summary>GML direction, degrees in [0, 360).</summary>
        public float Direction
        {
            get => direction;
            private set => direction = Mathf.Repeat(value, 360f);
        }

        /// <summary>GML speed, px/step (negative moves backwards, as GML's does).</summary>
        public float SpeedPxPerStep { get; private set; }
        /// <summary>GML Speed: the speed Step restores, kept through a pause.</summary>
        public float StoredSpeedPxPerStep { get; private set; }

        public float ImageAngle
        {
            get => imageAngle;
            private set
            {
                imageAngle = Mathf.Repeat(value, 360f);
                transform.rotation = Quaternion.Euler(0f, 0f, imageAngle);
            }
        }

        /// <summary>GML spin, degrees/step.</summary>
        public float Spin { get; private set; }
        public float OriginalSpin { get; private set; }

        /// <summary>GML hspeed/vspeed in px/step, Unity axes.</summary>
        public Vector2 VelocityPxPerStep => Gml.Dir(Direction) * SpeedPxPerStep;

        // ---- IPerchSurface: the ship's par_rock collision (U.HARE-T53) ----

        SurfaceKind IPerchSurface.Kind => SurfaceKind.Rock;
        int IPerchSurface.SurfaceId => GetInstanceID();
        float IPerchSurface.SpinPerStep => Spin;
        Collider2D IPerchSurface.Mask => GetComponent<Collider2D>();

        /// <summary>
        /// GML instance_create(x, y, par_rock): its Create event, with <paramref name="kind"/> pinning
        /// rnd_rock (0-5) for tests and art-free callers.
        /// </summary>
        public static Rock Create(Vector2 position, int stationaryPercent, Rect? room = null, int? kind = null)
        {
            var go = new GameObject("Rock");
            var rock = go.AddComponent<Rock>();
            rock.Init(position, stationaryPercent, room, kind);
            return rock;
        }

        public void Init(Vector2 position, int stationaryPercent, Rect? room = null, int? kind = null)
        {
            this.room = room ?? Gml.Room;
            gone = false;
            transform.position = new Vector3(position.x, position.y, 0f);

            Rgb = FullRgb;
            ImageAngle = IRandom(0, 359);
            Kind = kind ?? IRandom(1, 6) - 1;
            Direction = IRandom(0, 359);
            SpeedPxPerStep = IRandom(1, 100) <= stationaryPercent ? 0f : IRandom(-15, 15) * 0.1f;
            StoredSpeedPxPerStep = SpeedPxPerStep;
            RollSpin();
            regenAlarm = AlarmSteps;
            frictionAlarm = AlarmSteps;

            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            spriteRenderer = HareArt.Show(gameObject, SpriteName, Depth);
            UseMask();
            ApplyBlend();
        }

        /// <summary>
        /// spin = irandom_range(-30, 30) * 0.01, none within 0.03, original_spin = spin. Create does
        /// it, and create_new_rock and setup_level roll it again; theirs set the caller's spin
        /// instead of the rock's when it is near zero (fixed: the rock's).
        /// </summary>
        public void RollSpin()
        {
            Spin = IRandom(-30, 30) * 0.01f;
            if (Mathf.Abs(Spin) <= NearZeroSpin + 1e-5f) Spin = 0f;
            OriginalSpin = Spin;
        }

        /// <summary>create_new_rock's writes after instance_create: a heading and speed (Speed follows).</summary>
        public void SetMotion(float directionDegrees, float speedPxPerStep)
        {
            Direction = directionDegrees;
            SpeedPxPerStep = speedPxPerStep;
            StoredSpeedPxPerStep = speedPxPerStep;
        }

        /// <summary>A precise mask: the sprite's physics shape, or its bounding ellipse without art.</summary>
        void UseMask()
        {
            foreach (var old in GetComponents<Collider2D>())
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            Collider2D mask;
            if (spriteRenderer != null && spriteRenderer.sprite != null && spriteRenderer.sprite.GetPhysicsShapeCount() > 0)
            {
                mask = gameObject.AddComponent<PolygonCollider2D>();
            }
            else
            {
                var ellipse = gameObject.AddComponent<CapsuleCollider2D>();
                Vector2 size = BBoxSizesPx[Kind];
                ellipse.direction = size.x > size.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
                ellipse.size = new Vector2(Gml.Px(size.x), Gml.Px(size.y));
                ellipse.offset = new Vector2(Gml.Px(BBoxCentresPx[Kind].x), Gml.Px(BBoxCentresPx[Kind].y));
                mask = ellipse;
            }
            mask.isTrigger = true;
        }

        void ApplyBlend()
        {
            if (spriteRenderer == null) return;
            float g = Rgb / 255f;
            spriteRenderer.color = new Color(g, g, g, 1f);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (alarms, Step, then built-in motion), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (gone) return;
            float steps = dt * Gml.StepsPerSecond;

            if (Ring(ref regenAlarm, steps))
            {
                if (Rgb < FullRgb) Rgb++;
                ApplyBlend();
                regenAlarm = AlarmSteps;
            }
            if (Ring(ref frictionAlarm, steps))
            {
                ApplyFriction();
                frictionAlarm = AlarmSteps;
            }

            if (!Gml.Paused)
            {
                SpeedPxPerStep = StoredSpeedPxPerStep;
                ImageAngle += Spin * steps;
                if (OutsideRoom())
                {
                    Remove(new RockGone(RockRemoval.LeftRoom, Position, 0, false));
                    return;
                }
            }
            else
            {
                SpeedPxPerStep = 0f;
            }

            Vector2 next = Position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            if (Application.isPlaying && body != null) body.MovePosition(next);
            else transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        /// <summary>
        /// Alarm 1: a spin wound up past original_spin slows by 0.96. GML's negative branch (spin &lt;
        /// -original_spin, spin *= -0.96) flipped a negative spin's sign every quarter second; fixed
        /// to slow it the same way as a positive one.
        /// </summary>
        public void ApplyFriction()
        {
            float limit = Mathf.Abs(OriginalSpin);
            if (Spin > 0f ? Spin > limit : Spin < -limit) Spin *= SpinFriction;
        }

        /// <summary>
        /// GML add_spin(id, amount): a still rock gets irandom_range(-15, 15) * 0.01; a spinning one
        /// gains spin * amount, then amount more its own way, capped at spin_max. GML's negative
        /// branch snapped every negative spin straight to -spin_max; fixed to mirror the positive one.
        /// </summary>
        public void AddSpin(float amount)
        {
            if (Spin == 0f)
            {
                Spin += IRandom(-15, 15) * 0.01f;
                return;
            }
            Spin += Spin * amount;
            if (Spin > 0f)
            {
                Spin += amount;
                if (Spin > SpinMax) Spin = SpinMax;
            }
            else
            {
                Spin -= amount;
                if (Spin < -SpinMax) Spin = -SpinMax;
            }
        }

        /// <summary>GML motion_add(dir, amount): adds a push to hspeed/vspeed; speed comes back non-negative.</summary>
        public void MotionAdd(float directionDegrees, float amountPxPerStep)
        {
            Vector2 v = VelocityPxPerStep + Gml.Dir(directionDegrees) * amountPxPerStep;
            SpeedPxPerStep = v.magnitude;
            if (SpeedPxPerStep > 1e-6f) Direction = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        /// <summary>x &lt; -size || x &gt; room_width + size || y &lt; -size || y &gt; room_height + size.</summary>
        public bool OutsideRoom()
        {
            // A hair of slack so a rock create_new_rock puts exactly on x = -size is not lost to float rounding.
            float size = Gml.Px(SizePx) + 1e-4f;
            Vector2 p = Position;
            return p.x < room.xMin - size || p.x > room.xMax + size || p.y < room.yMin - size || p.y > room.yMax + size;
        }

        // ---- Collisions ----

        /// <summary>GML par_rock Collision obj_bullet (and the bullet's own par_rock event): the bullet is always spent.</summary>
        public bool OnProjectileHit(Bullet bullet)
        {
            if (gone || bullet == null) return false;
            Vector2 at = bullet.transform.position;
            if (Rgb > BreakRgb)
            {
                Rgb -= BulletDarken;
                ApplyBlend();
                Push(bullet.Direction, BulletPushPxPerStep);
                AddSpin(BulletSpin);
                TinyExplosion.Spawn(at + PxVec(VelocityPxPerStep));
                return true;
            }
            Break(RockRemoval.Bullet, IsPlayerRound(bullet));
            TinyExplosion.Spawn(at); // obj_bullet's par_rock event
            return true;
        }

        /// <summary>
        /// GML par_rock Collision obj_rocket. Above RGB 128 the rocket goes off at itself plus the
        /// rock's hspeed/vspeed; on the breaking hit it is not touched and flies on. Always false:
        /// the rock sets the rocket off itself, where GML puts the explosion.
        /// </summary>
        public bool OnRocketHit(Rocket rocket)
        {
            if (gone || rocket == null) return false;
            if (Rgb > BreakRgb)
            {
                Rgb -= RocketDarken;
                ApplyBlend();
                Push(rocket.Direction, RocketPushPxPerStep);
                AddSpin(RocketSpin);
                Vector2 at = (Vector2)rocket.transform.position + PxVec(VelocityPxPerStep);
                var explosion = rocket.Explode();
                if (explosion != null) explosion.transform.position = at;
                return false;
            }
            Break(RockRemoval.Rocket, true);
            return false;
        }

        /// <summary>motion_add(other.direction, amount); if (speed &gt; 3) speed = 3; Speed = speed.</summary>
        void Push(float directionDegrees, float amount)
        {
            MotionAdd(directionDegrees, amount);
            if (SpeedPxPerStep > MaxPushedSpeedPxPerStep) SpeedPxPerStep = MaxPushedSpeedPxPerStep;
            StoredSpeedPxPerStep = SpeedPxPerStep;
        }

        /// <summary>The kill branch: score, the size-2 burst, an obj_grenade_explosion, then rock_destroyed.</summary>
        void Break(RockRemoval cause, bool awardsScore)
        {
            Vector2 at = Position;
            GrenadeExplosion.Spawn(at);
            DeathEffects.Burst(at, DeathEffects.Large);
            Remove(new RockGone(cause, at, Points, awardsScore));
        }

        /// <summary>setup_level's clear and move_rocks: gone, with no replacement.</summary>
        public void Clear() => Remove(new RockGone(RockRemoval.Cleared, Position, 0, false));

        void Remove(RockGone info)
        {
            if (gone) return;
            gone = true;
            Removed?.Invoke(this, info);
            if (Field != null) Field.OnRockGone(this, info);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        // ---- helpers ----

        /// <summary>The ship's own rounds (GML other.owner == player.id).</summary>
        static bool IsPlayerRound(Bullet bullet) =>
            !bullet.FromEnemy && bullet.Owner != null && bullet.Owner.GetComponentInParent<PlayerShipController>() != null;

        static Vector2 PxVec(Vector2 px) => new Vector2(Gml.Px(px.x), Gml.Px(px.y));

        static bool Ring(ref float alarm, float steps)
        {
            if (alarm < 0f) return false;
            alarm -= steps;
            if (alarm > 1e-3f) return false;
            alarm = -1f;
            return true;
        }

        /// <summary>GML irandom_range(a, b), inclusive.</summary>
        internal static int IRandom(int min, int max) => UnityEngine.Random.Range(min, max + 1);
    }
}
