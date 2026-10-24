namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_phalanx (gmlHARE, par_enemy): a slow body (two counter-rotating layers, image_angle +2 and
    /// back_angle -2 a step, trailing ef_spark) that creeps toward the ship at 0.5 px/step until within 100 px,
    /// carrying a <see cref="PhalanxShieldBody"/> that turns to face the ship. Any bullet of the ship's or rocket that
    /// reaches the body kills it for 20 points and a pickup; its rocket-explosion and shield-wall events are empty,
    /// enemy rounds pass through, and it has no push-apart (that event is commented out).
    /// </summary>
    public sealed class PhalanxEnemy : EnemyBody
    {
        public const float SpinDegreesPerStep = 2f;
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 0.5f;

        public PhalanxEnemy() => Kind = EnemyKind.Phalanx;

        /// <summary>GML back_angle: the under-layer's angle.</summary>
        public float BackAngle { get; private set; }
        /// <summary>GML shield: the obj_phalanx_shield created with it.</summary>
        public PhalanxShieldBody Shield { get; private set; }

        /// <summary>GML Create, which also creates the shield (shield.parent = id).</summary>
        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            BackAngle = Rng.IRandom(0, 359);
            Shield = new PhalanxShieldBody(this);
            Spawn(Shield);
        }

        protected override void Alarms(float steps) { }

        protected override void StepEvent(float steps)
        {
            ImageAngle += SpinDegreesPerStep * steps;
            BackAngle = GmlMath.Wrap(BackAngle - SpinDegreesPerStep * steps);
            FadeIn(steps);
            var target = Target;
            if (target != null && DistanceToPlayerPx() > EngageRangePx) MoveTowardsPoint(target.Position, ChaseSpeedPxPerStep);
            else SpeedPxPerStep = 0f;
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            DeathBurst();
            Die(EnemyDeathCause.Rocket, EnemyTable.Get(Kind).Points, true, true);
            return true;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            if (shot.FromEnemy) return false;
            TinyExplosionAt(shot.Position);
            DeathBurst();
            Die(EnemyDeathCause.Bullet, EnemyTable.Get(Kind).Points, shot.FromPlayer, true);
            return true;
        }
    }

    /// <summary>
    /// The rules of obj_phalanx_shield (gmlHARE, par_enemy, spr_shield_hitbox: the half disc in front of its angle).
    /// Each step it sits on its phalanx, flickers (alpha 0.3-0.6), cycles its 6-frame spr_shieldwall_anime and turns
    /// 0.2 degrees toward the ship the short way. Bullets and rockets bounce off it along the normal (once per shot:
    /// last_impact) instead of hitting the body, enemy rounds included. Being a par_enemy, it hurts the ship on
    /// contact, draws rockets and mines, and the laser, railgun or flame destroy it, leaving the phalanx bare. It
    /// removes itself once its phalanx is gone. (Its rot_speed, flipped every 120-240 steps by Alarm 0, is never used.)
    /// </summary>
    public sealed class PhalanxShieldBody : EnemyBody
    {
        public const float TurnDegreesPerStep = 0.2f;
        public const float RadiusPx = 46f;
        public const int Frames = 6;
        public const float AnimationEverySteps = 3f;

        float animationAlarm;
        int lastImpact = int.MinValue;

        internal PhalanxShieldBody(PhalanxEnemy parent)
        {
            Kind = EnemyKind.Phalanx;
            Parent = parent;
        }

        public override BodyPart Part => BodyPart.Shield;
        protected override Vec2 BBoxHalfPx => new Vec2(RadiusPx / 2f, RadiusPx);
        protected override Vec2 BBoxOffsetPx => Vec2.Zero;
        public override bool PushesAway => false;

        public PhalanxEnemy Parent { get; }
        /// <summary>spr_shieldwall_anime frame.</summary>
        public int CurrentFrame { get; private set; }

        internal override void Create(EnemyOptions o)
        {
            ImageAngle = 0f;
            ImageAlpha = 0.1f * Rng.IRandom(3, 6);
            CurrentFrame = 0;
            animationAlarm = AnimationEverySteps;
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref animationAlarm, steps))
            {
                NextFrame();
                animationAlarm = AnimationEverySteps;
            }
        }

        protected override void StepEvent(float steps)
        {
            if (Parent == null || Parent.IsDead)
            {
                Die(EnemyDeathCause.Orphaned, 0, false, false);
                return;
            }
            Teleport(Parent.Position);
            ImageAlpha = 0.1f * Rng.IRandom(3, 6);
            var target = Target;
            if (target != null)
            {
                float playerAngle = GmlMath.PointDirection(Position, target.Position);
                bool clockwise = GmlMath.CheckRotateClockwise(playerAngle, ImageAngle);
                ImageAngle += (clockwise ? -TurnDegreesPerStep : TurnDegreesPerStep) * steps;
            }
            NextFrame();
            SpeedPxPerStep = 0f;
        }

        void NextFrame() => CurrentFrame = CurrentFrame < Frames - 1 ? CurrentFrame + 1 : 0;

        /// <summary>GML normal_detect off the shield: outward from its centre through <paramref name="point"/>.</summary>
        public Vec2 NormalAt(Vec2 point)
        {
            Vec2 n = point - Position;
            return n.SqrMagnitude > 1e-12f ? n.Normalized : GmlMath.Dir(ImageAngle);
        }

        static float Degrees(Vec2 n) => GmlMath.Wrap((float)(System.Math.Atan2(n.Y, n.X) * 180.0 / System.Math.PI));

        /// <summary>A rocket bounces off along the normal; it never explodes on the shield (the rocket dedupes per surface).</summary>
        protected override bool RocketHit(Vec2 rocketAt)
        {
            Deflection = Degrees(NormalAt(rocketAt));
            return false;
        }

        /// <summary>A bullet bounces off along the normal, once per shot (last_impact); it is never spent.</summary>
        protected override bool BulletHit(in BulletShot shot)
        {
            if (shot.Key == 0 || shot.Key == lastImpact) return false;
            lastImpact = shot.Key;
            Deflection = Degrees(NormalAt(shot.Position));
            return false;
        }
    }
}
