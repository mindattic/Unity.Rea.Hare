using System;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>What obj_dagger is in when it moves (GML mode).</summary>
    public enum DaggerMode
    {
        /// <summary>Stopped, turning its blade 0.5 degrees/step toward its new heading.</summary>
        Rotate,
        /// <summary>Lined up: accelerating along its heading, trailing smoke.</summary>
        Fly,
        /// <summary>Braking to a halt before picking a new heading.</summary>
        Stop
    }

    /// <summary>
    /// The rules of obj_dagger (gmlHARE, a par_enemy; it has no ai_* script, its flight lives in its own events). It
    /// spawns at rest facing a random whole degree with a random heading (direction) and cycles three modes. Rotate
    /// turns image_angle 0.5 degrees a step toward the heading (check_rotate_clockwise) until it is within 2 degrees
    /// (a_near_b), then it launches (ef_ring) into Fly for irandom_range(120, 240) steps (Alarm 1), gaining 1 px/step
    /// every 8 steps (Alarm 0) up to 8 and puffing smoke every 20 steps (Alarm 2); Stop then sheds 1 px/step every
    /// 8 steps and, once still, picks a new random heading and rotates again. Its blade never turns while flying.
    /// Once it is more than its sprite width (96 px) outside the room it reverses (direction -= 180, and the blade
    /// snaps to it).
    ///
    /// It dies to any bullet (and takes the bullet with it), any rocket, any rocket explosion, the shield wall, the
    /// laser, the railgun and flame. Its own collision events give 10 points and roll random_pickup; the beams and
    /// flame destroy it from their side, so those kills give neither.
    /// </summary>
    public sealed class DaggerEnemy : EnemyBody
    {
        /// <summary>GML rate: Alarm 0 changes speed by 1 px/step this often.</summary>
        public const float RateSteps = 8f;
        public const float MaxSpeedPxPerStep = 8f;
        public const float TurnRateDegreesPerStep = 0.5f;
        /// <summary>a_near_b(image_angle, direction, 2).</summary>
        public const float AlignToleranceDegrees = 2f;
        /// <summary>alarm[1] = irandom_range(120, 240): how long it flies before braking.</summary>
        public const int MinFlySteps = 120;
        public const int MaxFlySteps = 240;
        /// <summary>Alarm 2: a smoke puff this often while flying.</summary>
        public const float SmokeEverySteps = 20f;
        /// <summary>size = sprite_get_width(spr_dagger).</summary>
        public const float SizePx = 96f;

        float speedAlarm;
        float stopAlarm = Off;
        float smokeAlarm = Off;

        public DaggerEnemy() => Kind = EnemyKind.Dagger;

        public DaggerMode Mode { get; private set; }
        /// <summary>1 = spr_dagger, 2 = spr_dagger2.</summary>
        public int Variant { get; private set; }
        /// <summary>Steps left in Fly before Alarm 1 switches to Stop, or negative when unset.</summary>
        public float FlyStepsLeft => stopAlarm;

        internal override void Create(EnemyOptions o)
        {
            SpeedPxPerStep = 0f;
            Direction = o.Direction ?? Rng.IRandom(0, 359);
            ImageAngle = o.ImageAngle ?? Rng.IRandom(0, 359);
            Mode = DaggerMode.Rotate;
            speedAlarm = RateSteps;
            stopAlarm = Off;
            smokeAlarm = Off;
            Variant = Rng.IRandom(1, 2);
        }

        // Alarms fire on whole steps; the slack absorbs float drift.
        static bool Due(float alarm) => alarm <= 1e-3f;

        protected override void Alarms(float steps)
        {
            // Alarm 0: accelerate while flying, brake while stopping; always re-armed.
            speedAlarm -= steps;
            while (Due(speedAlarm))
            {
                if (Mode == DaggerMode.Fly) { if (SpeedPxPerStep < MaxSpeedPxPerStep) SpeedPxPerStep += 1f; }
                else if (Mode == DaggerMode.Stop) { if (SpeedPxPerStep > 0f) SpeedPxPerStep -= 1f; }
                speedAlarm += RateSteps;
            }

            // Alarm 1: stop flying.
            if (stopAlarm >= 0f)
            {
                stopAlarm -= steps;
                if (Due(stopAlarm))
                {
                    stopAlarm = Off;
                    Mode = DaggerMode.Stop;
                }
            }

            // Alarm 2: smoke trail while flying, re-armed every 20 steps until Stop clears it.
            if (smokeAlarm >= 0f)
            {
                smokeAlarm -= steps;
                while (Due(smokeAlarm))
                {
                    if (Mode == DaggerMode.Fly)
                        Emit(new SmokePuffRaised { At = Position - GmlMath.Dir(Direction) * GameClock.Px(SpeedPxPerStep) });
                    smokeAlarm += SmokeEverySteps;
                }
            }
        }

        protected override void StepEvent(float steps)
        {
            if (OutsideRoom(SizePx))
            {
                Direction -= 180f;
                ImageAngle = Direction;
            }

            switch (Mode)
            {
                case DaggerMode.Rotate:
                    if (!GmlMath.ANearB(ImageAngle, Direction, AlignToleranceDegrees))
                    {
                        float turn = TurnRateDegreesPerStep * steps;
                        ImageAngle += GmlMath.CheckRotateClockwise(ImageAngle, Direction) ? turn : -turn;
                    }
                    else
                    {
                        Mode = DaggerMode.Fly;
                        Emit(new DaggerLaunched { At = Position });
                        stopAlarm = Rng.IRandom(MinFlySteps, MaxFlySteps);
                        smokeAlarm = SmokeEverySteps;
                    }
                    break;

                case DaggerMode.Stop:
                    if (SpeedPxPerStep <= 0f)
                    {
                        SpeedPxPerStep = 0f;
                        Direction = Rng.IRandom(0, 359);
                        Mode = DaggerMode.Rotate;
                        smokeAlarm = Off; // alarm[2] = 0 never fires
                    }
                    break;
            }
        }

        protected override bool HandlesShield => true;

        /// <summary>GML collision with obj_shieldwall: dies with a tiny explosion.</summary>
        protected override void ShieldHit()
        {
            TinyExplosionAt(Position);
            Die(EnemyDeathCause.ShieldWall, EnemyTable.Get(Kind).Points, true, true);
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            DeathBurst();
            Die(EnemyDeathCause.Rocket, EnemyTable.Get(Kind).Points, true, true);
            return true;
        }

        /// <summary>GML collision with obj_bullet: both go; points only for the ship's own rounds. Enemy rounds pass through.</summary>
        protected override bool BulletHit(in BulletShot shot)
        {
            if (IgnoresRound(shot)) return false;
            TinyExplosionAt(shot.Position);
            DeathBurst();
            Die(EnemyDeathCause.Bullet, EnemyTable.Get(Kind).Points, shot.FromPlayer, true);
            return true;
        }

        protected override void RocketExplosionHit() => Die(EnemyDeathCause.RocketExplosion, EnemyTable.Get(Kind).Points, true, true);
    }
}
