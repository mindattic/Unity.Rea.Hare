using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_hydra (gmlHARE, par_enemy): a spinning body (image_angle += rot_dir, 1 degree a
    /// step, the sign flipping every 100-1000 then 100-6000 steps) with 2-5 turrets. Every 200-400
    /// steps (Alarm 0, first on step 1) each idle turret picks a random target angle and a turn
    /// direction of 2 degrees/step; because the original negates check_rotate_clockwise's answer
    /// the turrets take the long way round. Once a turret is within 2 degrees of its target it fires
    /// one bullet 28 px out along its angle at 2 px/step (sfx_blipgun). It closes on the ship at
    /// 1 px/step until within 100 px, then holds.
    ///
    /// Each of the ship's bullets costs it a turret (arms -= 1); the hit after the last turret kills
    /// it for 10 points and a pickup. A rocket or a rocket explosion kills it outright for 10 and a
    /// pickup. Enemy rounds pass through; it pushes away from overlapping par_enemy.
    /// </summary>
    public class Hydra : SetPieceEnemy
    {
        public const int MinArms = 2;
        public const int MaxArms = 5;
        public const float TurretTurnDegreesPerStep = 2f;
        public const float AimToleranceDegrees = 2f;
        public const float MuzzlePx = 28f;
        public const float BulletSpeedPxPerStep = 2f;
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 1f;
        public const int Points = 10;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Small;

        readonly float[] targetAngle = new float[MaxArms];
        readonly float[] currentAngle = new float[MaxArms];
        readonly float[] turretDir = new float[MaxArms];
        readonly bool[] canShoot = new bool[MaxArms];
        float aimAlarm;
        float flipAlarm;

        /// <summary>GML arms: turrets left; it dies on the hit that takes this below 0.</summary>
        public int Arms { get; private set; }
        /// <summary>GML rot_dir: +1 or -1 degrees/step.</summary>
        public int RotationDirection { get; private set; }

        public float TurretAngle(int i) => currentAngle[i];
        public float TurretTarget(int i) => targetAngle[i];
        public bool TurretReady(int i) => canShoot[i];

        /// <summary>A turret fired (GML audio_play_sound(sfx_blipgun)).</summary>
        public event Action<Hydra, Bullet> Fired;

        protected override Vector2 BBoxHalfPx => new Vector2(15f, 15f);
        protected override bool PushesAwayFromEnemies => true;

        /// <summary>GML Create. Optional values pin the random ones for tests.</summary>
        public void Init(Vector2 position, Rect? room = null, Unity.Hare.Player.PlayerShipController player = null,
            int? arms = null, int? rotationDirection = null)
        {
            Setup(position, room, player);
            UseEllipse(new Vector2(30f, 30f));
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Arms = Mathf.Clamp(arms ?? IRandom(MinArms, MaxArms), 0, MaxArms);
            int rot = rotationDirection ?? IRandom(-1, 1);
            RotationDirection = rot == 0 ? 1 : Math.Sign(rot);
            for (int i = 0; i < MaxArms; i++)
            {
                targetAngle[i] = IRandom(0, 359);
                currentAngle[i] = IRandom(0, 359);
                turretDir[i] = 3f;
                canShoot[i] = false;
            }
            aimAlarm = 1f;
            flipAlarm = IRandom(100, 1000);
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
                    targetAngle[i] = IRandom(0, 359);
                    turretDir[i] = Dagger.CheckRotateClockwise(currentAngle[i], targetAngle[i]) ? -TurretTurnDegreesPerStep : TurretTurnDegreesPerStep;
                }
                aimAlarm = IRandom(200, 400);
            }

            // Alarm 1: reverse the body's spin.
            if (Ring(ref flipAlarm, steps))
            {
                RotationDirection = -RotationDirection;
                flipAlarm = IRandom(100, 6000);
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += RotationDirection * steps;
            FadeIn(steps);

            for (int i = 0; i < Arms; i++)
            {
                if (!Dagger.ANearB(currentAngle[i], targetAngle[i], AimToleranceDegrees))
                {
                    currentAngle[i] += turretDir[i] * steps;
                    if (currentAngle[i] >= 360f) currentAngle[i] -= 360f;
                    else if (currentAngle[i] < 0f) currentAngle[i] = Mathf.Repeat(currentAngle[i] + 360f, 360f);
                }
                else if (canShoot[i])
                {
                    canShoot[i] = false;
                    var bullet = Fire(Position + Gml.Dir(currentAngle[i]) * Gml.Px(MuzzlePx), currentAngle[i], BulletSpeedPxPerStep);
                    Fired?.Invoke(this, bullet);
                }
            }

            if (HasPlayer && DistanceToPlayerPx() > EngageRangePx) MoveTowardsPoint(PlayerPosition, ChaseSpeedPxPerStep);
            else SpeedPxPerStep = 0f;
        }

        protected override bool RocketHit(Rocket rocket)
        {
            DeathBurst(DeathEffectSize);
            Die(EnemyDeathCause.Rocket, Points, true, true);
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            if (IsEnemyRound(bullet)) return false;
            TinyExplosionAt(bullet);
            Arms -= 1;
            if (Arms < 0)
            {
                DeathBurst(DeathEffectSize);
                Die(EnemyDeathCause.Bullet, Points, IsPlayerRound(bullet), true);
            }
            return true;
        }

        protected override void RocketExplosionHit() => Die(EnemyDeathCause.RocketExplosion, Points, true, true);
    }
}
