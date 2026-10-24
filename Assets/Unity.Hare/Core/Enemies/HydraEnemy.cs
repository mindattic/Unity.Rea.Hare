using System;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_hydra (gmlHARE, par_enemy): a spinning body (image_angle += rot_dir, 1 degree a step, the sign
    /// flipping every 100-1000 then 100-6000 steps) with 2-5 turrets. Every 200-400 steps (Alarm 0, first on step 1)
    /// each idle turret picks a random target angle and a turn direction of 2 degrees/step; because the original
    /// negates check_rotate_clockwise's answer the turrets take the long way round. Once a turret is within 2 degrees
    /// of its target it fires one bullet 28 px out along its angle at 2 px/step. It closes on the ship at 1 px/step
    /// until within 100 px, then holds.
    ///
    /// Each of the ship's bullets costs it a turret (arms -= 1); the hit after the last turret kills it for 10 points
    /// and a pickup. A rocket or a rocket explosion kills it outright for 10 and a pickup. Enemy rounds pass through;
    /// it pushes away from overlapping par_enemy.
    /// </summary>
    public sealed class HydraEnemy : EnemyBody
    {
        public const int MinArms = 2;
        public const int MaxArms = 5;
        public const float TurretTurnDegreesPerStep = 2f;
        public const float AimToleranceDegrees = 2f;
        public const float MuzzlePx = 28f;
        public const float BulletSpeedPxPerStep = 2f;
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 1f;

        readonly float[] targetAngle = new float[MaxArms];
        readonly float[] currentAngle = new float[MaxArms];
        readonly float[] turretDir = new float[MaxArms];
        readonly bool[] canShoot = new bool[MaxArms];
        float aimAlarm;
        float flipAlarm;

        public HydraEnemy() => Kind = EnemyKind.Hydra;

        /// <summary>GML arms: turrets left; it dies on the hit that takes this below 0.</summary>
        public int Arms { get; private set; }
        /// <summary>GML rot_dir: +1 or -1 degrees/step.</summary>
        public int RotationDirection { get; private set; }

        public float TurretAngle(int i) => currentAngle[i];
        public float TurretTarget(int i) => targetAngle[i];
        public bool TurretReady(int i) => canShoot[i];

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Arms = Math.Max(0, Math.Min(MaxArms, o.Arms ?? Rng.IRandom(MinArms, MaxArms)));
            int rot = o.RotationDirection ?? Rng.IRandom(-1, 1);
            RotationDirection = rot == 0 ? 1 : Math.Sign(rot);
            for (int i = 0; i < MaxArms; i++)
            {
                targetAngle[i] = Rng.IRandom(0, 359);
                currentAngle[i] = Rng.IRandom(0, 359);
                turretDir[i] = 3f;
                canShoot[i] = false;
            }
            aimAlarm = 1f;
            flipAlarm = Rng.IRandom(100, 1000);
        }

        protected override void Alarms(float steps)
        {
            // Alarm 0: idle turrets pick a new target.
            if (Ring(ref aimAlarm, steps))
            {
                for (int i = 0; i < Arms; i++)
                {
                    if (canShoot[i]) continue;
                    canShoot[i] = true;
                    targetAngle[i] = Rng.IRandom(0, 359);
                    turretDir[i] = GmlMath.CheckRotateClockwise(currentAngle[i], targetAngle[i]) ? -TurretTurnDegreesPerStep : TurretTurnDegreesPerStep;
                }
                aimAlarm = Rng.IRandom(200, 400);
            }

            // Alarm 1: reverse the body's spin.
            if (Ring(ref flipAlarm, steps))
            {
                RotationDirection = -RotationDirection;
                flipAlarm = Rng.IRandom(100, 6000);
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += RotationDirection * steps;
            FadeIn(steps);

            for (int i = 0; i < Arms; i++)
            {
                if (!GmlMath.ANearB(currentAngle[i], targetAngle[i], AimToleranceDegrees))
                {
                    currentAngle[i] += turretDir[i] * steps;
                    if (currentAngle[i] >= 360f) currentAngle[i] -= 360f;
                    else if (currentAngle[i] < 0f) currentAngle[i] = GmlMath.Wrap(currentAngle[i] + 360f);
                }
                else if (canShoot[i])
                {
                    canShoot[i] = false;
                    Fire(Position + GmlMath.Dir(currentAngle[i]) * GameClock.Px(MuzzlePx), currentAngle[i], BulletSpeedPxPerStep, i);
                }
            }

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
            Arms -= 1;
            if (Arms < 0)
            {
                DeathBurst();
                Die(EnemyDeathCause.Bullet, EnemyTable.Get(Kind).Points, shot.FromPlayer, true);
            }
            return true;
        }

        protected override void RocketExplosionHit() => Die(EnemyDeathCause.RocketExplosion, EnemyTable.Get(Kind).Points, true, true);
    }
}
