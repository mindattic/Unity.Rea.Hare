using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_meatball (gmlHARE, par_enemy): a big (181x172) blob that closes on the ship at
    /// 1 px/step until within 100 px. On its first step and then every 80-200 steps (Alarm 0) it
    /// pulses (ef_ring, ef_smokeup) and sprays 4-6 bullets in random directions at 2 px/step. Its
    /// eyes glance to a random offset of up to 6 px every 20-120 steps (Alarm 2).
    ///
    /// Damage darkens it: image_blend is grey RGB, starting at 255 and healing 1 every 120 steps
    /// (Alarm 1). While RGB is above 128 a bullet of the ship's costs 5 and shoves it half the
    /// bullet's velocity, and a rocket costs 16 (the rocket still explodes). The hit after that
    /// bursts it: 15-30 bullets in random directions (1-4 px/step from a rocket, 2-4 from a
    /// bullet), 50 points (always for a rocket, only for the ship's bullet), a pickup, an
    /// obj_grenade_explosion and meatball_gib's 8-12 gibs. Rocket explosions do nothing to it,
    /// enemy rounds pass through, and it pushes away from overlapping par_enemy.
    /// </summary>
    public class Meatball : SetPieceEnemy
    {
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 1f;
        public const int FullRgb = 255;
        public const int BurstRgb = 128;
        public const int BulletDamage = 5;
        public const int RocketDamage = 16;
        public const float RegenEverySteps = 120f;
        public const float SprayBulletSpeedPxPerStep = 2f;
        public const int Points = 50;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Large;
        /// <summary>spr_meatball's bbox centre sits 34 px above its origin (meatball_gib uses y - 34).</summary>
        public static readonly Vector2 CentreOffsetPx = new Vector2(-0.5f, 34f);

        float sprayAlarm;
        float regenAlarm;
        float eyeAlarm;

        /// <summary>GML RGB: image_blend's grey level.</summary>
        public int Rgb { get; private set; }
        /// <summary>GML eyeX/eyeY in px (GML axes).</summary>
        public Vector2Int EyeOffset { get; private set; }

        /// <summary>Alarm 0's pulse (ef_ring + ef_smokeup) with the bullets it sprayed.</summary>
        public event Action<Meatball, int> Sprayed;

        protected override Vector2 BBoxHalfPx => new Vector2(89f, 83.5f);
        protected override Vector2 BBoxOffsetPx => CentreOffsetPx;
        protected override bool PushesAwayFromEnemies => true;

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null)
        {
            Setup(position, room, player);
            UseEllipse(new Vector2(178f, 167f), CentreOffsetPx);
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Rgb = FullRgb;
            EyeOffset = Vector2Int.zero;
            sprayAlarm = 1f;
            regenAlarm = RegenEverySteps;
            eyeAlarm = IRandom(20, 240);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref sprayAlarm, steps))
            {
                int count = IRandom(4, 6);
                for (int i = 0; i < count; i++) Fire(Position, IRandom(0, 359), SprayBulletSpeedPxPerStep);
                Sprayed?.Invoke(this, count);
                sprayAlarm = IRandom(80, 200);
            }
            if (Ring(ref regenAlarm, steps))
            {
                if (Rgb < FullRgb) Rgb += 1;
                regenAlarm = RegenEverySteps;
            }
            if (Ring(ref eyeAlarm, steps))
            {
                EyeOffset = new Vector2Int(IRandom(-6, 6), IRandom(-6, 6));
                eyeAlarm = IRandom(20, 120);
            }
        }

        protected override void StepEvent(float steps)
        {
            FadeIn(steps);
            if (HasPlayer && DistanceToPlayerPx() > EngageRangePx) MoveTowardsPoint(PlayerPosition, ChaseSpeedPxPerStep);
            else SpeedPxPerStep = 0f;
        }

        protected override bool RocketHit(Rocket rocket)
        {
            if (Rgb > BurstRgb) Rgb -= RocketDamage;
            else Burst(EnemyDeathCause.Rocket, 1, true, null);
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            if (IsEnemyRound(bullet)) return false;
            if (Rgb > BurstRgb)
            {
                // Pushback: x += other.hspeed / 2.
                if (bullet != null) Teleport(Position + Gml.Dir(bullet.Direction) * (bullet.Speed / Gml.StepsPerSecond / 2f));
                Rgb -= BulletDamage;
                TinyExplosionAt(bullet);
            }
            else Burst(EnemyDeathCause.Bullet, 2, IsPlayerRound(bullet), bullet);
            return true;
        }

        /// <summary>
        /// "Explosive destruction": a ring of 15-30 bullets, a tiny explosion where the bullet was
        /// (the rocket branch has none: its rocket explodes instead), the grenade blast, the large
        /// ef_smokeup and ef_firework, then the gibs.
        /// </summary>
        void Burst(EnemyDeathCause cause, int minSpeed, bool awardsScore, Bullet bullet)
        {
            Vector2 at = Position;
            int count = IRandom(15, 30);
            for (int i = 0; i < count; i++) Fire(at, IRandom(0, 359), IRandom(minSpeed, 4));
            if (cause == EnemyDeathCause.Bullet) TinyExplosionAt(bullet);
            GrenadeExplosion.Spawn(at);
            DeathBurst(DeathEffectSize);
            Die(cause, Points, awardsScore, true);
            FadingDebris.MeatballGibs(at);
        }
    }
}
