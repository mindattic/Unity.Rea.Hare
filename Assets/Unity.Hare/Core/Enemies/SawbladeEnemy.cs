namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_sawblade (gmlHARE, par_enemy): a small spinning blade (image_angle +rot_speed, its back layer
    /// +rot_speed+1). Farther than 400 px from the ship it drifts in at 0.5 px/step spinning 3 a step; within 400 px
    /// it spins 6 and swarms at 2 px/step toward a swarm point, re-picked every 30-60 steps (first after 30-120) as
    /// the ship's position plus twice its velocity plus up to 48 px of jitter each way. Until the first pick that
    /// point is GML (0, 0), the room's top-left corner.
    ///
    /// It dies to anything for 10 points and a pickup: the ship's bullets (enemy rounds pass through; points only for
    /// the ship's), rockets, rocket explosions and the shield wall. It pushes away from overlapping par_enemy.
    /// </summary>
    public sealed class SawbladeEnemy : EnemyBody
    {
        public const float SwarmRangePx = 400f;
        public const float DriftSpeedPxPerStep = 0.5f;
        public const float SwarmSpeedPxPerStep = 2f;
        public const float DriftSpin = 3f;
        public const float SwarmSpin = 6f;
        public const float LeadSteps = 2f;
        public const int JitterPx = 48;

        float swarmAlarm;

        public SawbladeEnemy() => Kind = EnemyKind.Sawblade;

        /// <summary>GML rot_speed, degrees/step.</summary>
        public float Spin { get; private set; }
        public float BackAngle { get; private set; }
        /// <summary>GML destX/destY: the swarm point.</summary>
        public Vec2 SwarmPoint { get; private set; }

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Spin = DriftSpin;
            BackAngle = Rng.IRandom(0, 359);
            SwarmPoint = new Vec2(Room.XMin, Room.YMax); // GML destX = destY = 0
            swarmAlarm = Rng.IRandom(30, 120);
        }

        protected override void Alarms(float steps)
        {
            if (!Ring(ref swarmAlarm, steps)) return;
            var target = Target;
            if (target != null)
            {
                Vec2 leadPx = target.VelocityPxPerStep * LeadSteps + new Vec2(Rng.IRandom(-JitterPx, JitterPx), Rng.IRandom(-JitterPx, JitterPx));
                SwarmPoint = target.Position + new Vec2(GameClock.Px(leadPx.X), GameClock.Px(leadPx.Y));
            }
            swarmAlarm = Rng.IRandom(30, 60);
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            BackAngle = GmlMath.Wrap(BackAngle + (Spin + 1f) * steps);
            FadeIn(steps);
            var target = Target;
            if (target == null) { SpeedPxPerStep = 0f; return; }

            if (DistanceToPlayerPx() > SwarmRangePx)
            {
                Spin = DriftSpin;
                MoveTowardsPoint(target.Position, DriftSpeedPxPerStep);
            }
            else
            {
                Spin = SwarmSpin;
                MoveTowardsPoint(SwarmPoint, SwarmSpeedPxPerStep);
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

        protected override void RocketExplosionHit() => Die(EnemyDeathCause.RocketExplosion, EnemyTable.Get(Kind).Points, true, true);

        protected override bool HandlesShield => true;

        /// <summary>GML collision with obj_shieldwall: 10 points, a pickup, a tiny explosion.</summary>
        protected override void ShieldHit()
        {
            TinyExplosionAt(Position);
            Die(EnemyDeathCause.ShieldWall, EnemyTable.Get(Kind).Points, true, true);
        }
    }
}
