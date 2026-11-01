namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_trilobyte (gmlHARE, par_enemy): a crawler that sweeps the room at a steady 2 px/step on a
    /// random heading, never chasing the ship. Alarm 0 (step 1, then every 240-620) nudges its direction by up to 20
    /// degrees and flips its curve; Alarm 1 bends it 1-6 degrees that way every 10 steps, trailing red ef_smoke. Its
    /// body turns 2 degrees a step toward the heading until within 3. More than its width (64 px) outside the room it
    /// reverses. Every 240-620 steps (Alarm 2) it lays an <see cref="EnemyMineBody"/> at its feet, angled along its heading.
    ///
    /// The ship's bullets and any rocket kill it for 10 points and a pickup; enemy rounds pass through, rocket
    /// explosions and the shield wall do nothing, and it pushes away from overlapping par_enemy.
    /// </summary>
    public sealed class TrilobyteEnemy : EnemyBody
    {
        public const float SpeedPx = 2f;
        public const float SizePx = 64f;
        public const float TurnDegreesPerStep = 2f;
        public const float AlignToleranceDegrees = 3f;
        public const float CurveEverySteps = 10f;

        float headingAlarm;
        float curveAlarm;
        float mineAlarm;
        bool curveLeft;

        public TrilobyteEnemy() => Kind = EnemyKind.Trilobyte;

        public bool CurvesLeft => curveLeft;

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = SpeedPx;
            Direction = o.Direction ?? Rng.IRandom(0, 359);
            ImageAngle = Direction;
            curveLeft = true;
            headingAlarm = 1f;
            curveAlarm = CurveEverySteps;
            mineAlarm = Rng.IRandom(240, 620);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref headingAlarm, steps))
            {
                Direction += Rng.IRandom(-20, 20);
                curveLeft = !curveLeft;
                headingAlarm = Rng.IRandom(240, 620);
            }
            if (Ring(ref curveAlarm, steps))
            {
                Direction += curveLeft ? Rng.IRandom(1, 6) : -Rng.IRandom(1, 6);
                Emit(new TrilobyteSmoked { At = Position });
                curveAlarm = CurveEverySteps;
            }
            if (Ring(ref mineAlarm, steps))
            {
                var mine = new EnemyMineBody(this, Direction);
                Spawn(mine);
                mineAlarm = Rng.IRandom(240, 620);
            }
        }

        protected override void StepEvent(float steps)
        {
            if (OutsideRoom(SizePx)) Direction -= 180f;
            if (!GmlMath.ANearB(ImageAngle, Direction, AlignToleranceDegrees))
            {
                bool clockwise = GmlMath.CheckRotateClockwise(Direction, ImageAngle);
                ImageAngle += (clockwise ? -TurnDegreesPerStep : TurnDegreesPerStep) * steps;
            }
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            DeathBurst();
            Die(EnemyDeathCause.Rocket, EnemyTable.Get(Kind).Points, true, true);
            return true;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            if (IgnoresRound(shot)) return false;
            TinyExplosionAt(shot.Position);
            DeathBurst();
            Die(EnemyDeathCause.Bullet, EnemyTable.Get(Kind).Points, shot.FromPlayer, true);
            return true;
        }
    }

    /// <summary>
    /// The rules of obj_enemy_mine (gmlHARE, par_projectile, laid by a <see cref="TrilobyteEnemy"/>): it sits still for
    /// 3 seconds, then arms (ef_ring, sfx_beep). Armed, it turns 3 degrees a step toward the ship and creeps at
    /// 1 px/step while the ship is within 200 px, and blows up as an obj_rocket_explosion within 24 px. Its fuse (20-30
    /// whole seconds) detonates it anyway; it pings (ef_ring) every 5 seconds.
    ///
    /// Not a par_enemy: beams, flame and rocket explosions ignore it and the ship can touch it harmlessly, but any
    /// bullet (whoever fired it) or rocket sets it off where the shot struck, for 1 point when it was the ship's.
    /// </summary>
    public sealed class EnemyMineBody : EnemyBody
    {
        public const float ArmSteps = 3 * 120f;
        public const int MinFuseSeconds = 20;
        public const int MaxFuseSeconds = 30;
        public const float PingEverySteps = 5 * 120f;
        public const float ChaseRangePx = 200f;
        public const float ChaseSpeedPxPerStep = 1f;
        public const float TurnDegreesPerStep = 3f;
        public const float DetonateRangePx = 24f;
        public const int Points = 1;
        /// <summary>spr_mine's bbox (18x11).</summary>
        public static readonly Vec2 SizePx = new Vec2(18f, 11f);

        readonly float? layingAngle;
        readonly int? fuseSeconds;
        float armAlarm;
        float fuseAlarm;
        float pingAlarm;

        /// <param name="layer">The trilobyte that laid it (GML owner).</param>
        /// <param name="imageAngle">The layer's heading (the trilobyte sets mine.image_angle = direction); random when null.</param>
        internal EnemyMineBody(EnemyBody layer, float? imageAngle, int? fuseSeconds = null)
        {
            Kind = EnemyKind.Trilobyte;
            Owner = layer;
            layingAngle = imageAngle;
            this.fuseSeconds = fuseSeconds;
        }

        public override BodyPart Part => BodyPart.Mine;
        public override bool IsParEnemy => false;
        public override bool PushesAway => false;
        protected override Vec2 BBoxHalfPx => SizePx / 2f;
        protected override Vec2 BBoxOffsetPx => Vec2.Zero;

        public bool IsActive { get; private set; }
        /// <summary>GML owner: who laid it.</summary>
        public EnemyBody Owner { get; }
        public float FuseStepsLeft => fuseAlarm;

        internal override void Create(EnemyOptions o)
        {
            ImageAngle = layingAngle ?? Rng.IRandom(0, 359);
            Direction = 0f;
            SpeedPxPerStep = 0f;
            IsActive = false;
            armAlarm = ArmSteps;
            fuseAlarm = 120f * (o.FuseSeconds ?? fuseSeconds ?? Rng.IRandom(MinFuseSeconds, MaxFuseSeconds));
            pingAlarm = PingEverySteps;
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref pingAlarm, steps))
            {
                Emit(new MinePinged { At = Position });
                pingAlarm = PingEverySteps;
            }
            if (Ring(ref fuseAlarm, steps))
            {
                Detonate(EnemyDeathCause.Fuse, Position, false);
                return;
            }
            if (Ring(ref armAlarm, steps))
            {
                IsActive = true;
                Emit(new MineArmed { At = Position });
            }
        }

        protected override void StepEvent(float steps)
        {
            if (!IsActive) return;
            var target = Target;
            float distance = target != null ? DistanceToPlayerPx() : float.PositiveInfinity;
            if (distance < ChaseRangePx)
            {
                float toward = GmlMath.PointDirection(Position, target.Position);
                bool clockwise = GmlMath.CheckRotateClockwise(toward, ImageAngle);
                ImageAngle += (clockwise ? -TurnDegreesPerStep : TurnDegreesPerStep) * steps;
                Direction = toward;
                SpeedPxPerStep = ChaseSpeedPxPerStep;
            }
            else SpeedPxPerStep = 0f;

            if (distance < DetonateRangePx) Detonate(EnemyDeathCause.Proximity, Position, false);
        }

        /// <summary>The rocket explodes where it is and takes the mine with it.</summary>
        protected override bool RocketHit(Vec2 rocketAt)
        {
            DeathBurst();
            Die(EnemyDeathCause.Rocket, Points, true, false);
            return true;
        }

        /// <summary>Any bullet: an obj_rocket_explosion where the bullet was, both gone.</summary>
        protected override bool BulletHit(in BulletShot shot)
        {
            DeathBurst();
            Detonate(EnemyDeathCause.Bullet, shot.Position, shot.FromPlayer);
            return true;
        }

        void Detonate(EnemyDeathCause cause, Vec2 at, bool awardsScore)
        {
            Emit(new RocketExplosionRaised { At = at });
            Die(cause, Points, awardsScore, false);
        }
    }
}
