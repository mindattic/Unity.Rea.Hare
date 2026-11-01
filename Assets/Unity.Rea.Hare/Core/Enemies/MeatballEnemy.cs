namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_meatball (gmlHARE, par_enemy): a big (181x172) blob that closes on the ship at 1 px/step until
    /// within 100 px. On its first step and then every 80-200 steps (Alarm 0) it pulses (ef_ring, ef_smokeup) and
    /// sprays 4-6 bullets in random directions at 2 px/step. Its eyes glance to a random offset of up to 6 px every
    /// 20-120 steps (Alarm 2).
    ///
    /// Damage darkens it: image_blend is grey RGB, starting at 255 and healing 1 every 120 steps (Alarm 1). While RGB
    /// is above 128 a bullet of the ship's costs 5 and shoves it half the bullet's velocity, and a rocket costs 16
    /// (the rocket still explodes). The hit after that bursts it: 15-30 bullets in random directions (1-4 px/step
    /// from a rocket, 2-4 from a bullet), 50 points (always for a rocket, only for the ship's bullet), a pickup, an
    /// obj_grenade_explosion and meatball_gib's 8-12 gibs. Rocket explosions do nothing to it, enemy rounds pass
    /// through, and it pushes away from overlapping par_enemy.
    /// </summary>
    public sealed class MeatballEnemy : EnemyBody
    {
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 1f;
        public const int FullRgb = 255;
        public const int BurstRgb = 128;
        public const int BulletDamage = 5;
        public const int RocketDamage = 16;
        public const float RegenEverySteps = 120f;
        public const float SprayBulletSpeedPxPerStep = 2f;

        float sprayAlarm;
        float regenAlarm;
        float eyeAlarm;

        public MeatballEnemy() => Kind = EnemyKind.Meatball;

        /// <summary>GML RGB: image_blend's grey level.</summary>
        public int Rgb { get; private set; }
        /// <summary>GML eyeX in px (GML axes).</summary>
        public int EyeX { get; private set; }
        /// <summary>GML eyeY in px (GML axes).</summary>
        public int EyeY { get; private set; }

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Rgb = FullRgb;
            sprayAlarm = 1f;
            regenAlarm = RegenEverySteps;
            eyeAlarm = Rng.IRandom(20, 240);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref sprayAlarm, steps))
            {
                int count = Rng.IRandom(4, 6);
                for (int i = 0; i < count; i++) Fire(Position, Rng.IRandom(0, 359), SprayBulletSpeedPxPerStep);
                Emit(new MeatballSprayed { At = Position, Bullets = count });
                sprayAlarm = Rng.IRandom(80, 200);
            }
            if (Ring(ref regenAlarm, steps))
            {
                if (Rgb < FullRgb) Rgb += 1;
                regenAlarm = RegenEverySteps;
            }
            if (Ring(ref eyeAlarm, steps))
            {
                EyeX = Rng.IRandom(-6, 6);
                EyeY = Rng.IRandom(-6, 6);
                eyeAlarm = Rng.IRandom(20, 120);
            }
        }

        protected override void StepEvent(float steps)
        {
            FadeIn(steps);
            var target = Target;
            if (target != null && DistanceToPlayerPx() > EngageRangePx) MoveTowardsPoint(target.Position, ChaseSpeedPxPerStep);
            else SpeedPxPerStep = 0f;
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            if (Rgb > BurstRgb) Rgb -= RocketDamage;
            else Burst(EnemyDeathCause.Rocket, 1, true, rocketAt);
            return true;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            if (IgnoresRound(shot)) return false;
            if (Rgb > BurstRgb)
            {
                // Pushback: x += other.hspeed / 2.
                Teleport(Position + GmlMath.Dir(shot.Direction) * (shot.SpeedUnitsPerSecond / GameClock.StepsPerSecond / 2f));
                Rgb -= BulletDamage;
                TinyExplosionAt(shot.Position);
            }
            else Burst(EnemyDeathCause.Bullet, 2, shot.FromPlayer, shot.Position);
            return true;
        }

        /// <summary>
        /// "Explosive destruction": a ring of 15-30 bullets, a tiny explosion where the bullet was (the rocket branch has
        /// none: its rocket explodes instead), the grenade blast, the large ef_smokeup and ef_firework, then the gibs.
        /// </summary>
        void Burst(EnemyDeathCause cause, int minSpeed, bool awardsScore, Vec2 hitAt)
        {
            Vec2 at = Position;
            int count = Rng.IRandom(15, 30);
            for (int i = 0; i < count; i++) Fire(at, Rng.IRandom(0, 359), Rng.IRandom(minSpeed, 4));
            if (cause == EnemyDeathCause.Bullet) TinyExplosionAt(hitAt);
            Emit(new GrenadeExplosionRaised { At = at });
            DeathBurst();
            Die(cause, EnemyTable.Get(Kind).Points, awardsScore, true);
            Gibs.Scatter(Rng, DebrisKind.MeatballGib, at, 8, 12, Debris);
        }
    }
}
