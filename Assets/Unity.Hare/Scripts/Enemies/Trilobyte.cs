using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_trilobyte (gmlHARE, par_enemy): a crawler that sweeps the room at a steady
    /// 2 px/step on a random heading, never chasing the ship. Alarm 0 (step 1, then every 240-620)
    /// nudges its direction by up to 20 degrees and flips its curve; Alarm 1 bends it 1-6 degrees
    /// that way every 10 steps, trailing red ef_smoke. Its body turns 2 degrees a step toward the
    /// heading until within 3. More than its width (64 px) outside the room it reverses. Every
    /// 240-620 steps (Alarm 2) it lays an <see cref="EnemyMine"/> at its feet, angled along its heading.
    ///
    /// The ship's bullets and any rocket kill it for 10 points and a pickup; enemy rounds pass
    /// through, rocket explosions and the shield wall do nothing, and it pushes away from
    /// overlapping par_enemy.
    /// </summary>
    public class Trilobyte : SetPieceEnemy
    {
        public const float SpeedPx = 2f;
        public const float SizePx = 64f;
        public const float TurnDegreesPerStep = 2f;
        public const float AlignToleranceDegrees = 3f;
        public const float CurveEverySteps = 10f;
        public const int Points = 10;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Small;
        /// <summary>spr_trilobyte's bbox (3..54 x 7..55 of 64x64, origin 32,32).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(52f, 49f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(-3.5f, 1f);

        float headingAlarm;
        float curveAlarm;
        float mineAlarm;
        bool curveLeft;

        public bool CurvesLeft => curveLeft;

        /// <summary>Alarm 1's ef_smoke puff.</summary>
        public event Action<Trilobyte> Smoked;
        /// <summary>Alarm 2 laid a mine.</summary>
        public event Action<Trilobyte, EnemyMine> MineLaid;

        protected override Vector2 BBoxHalfPx => HitboxSizePx / 2f;
        protected override Vector2 BBoxOffsetPx => HitboxOffsetPx;
        protected override bool PushesAwayFromEnemies => true;

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null, float? direction = null)
        {
            Setup(position, room, player);
            UseBox(HitboxSizePx, HitboxOffsetPx);
            ImageAlpha = 1f;
            SpeedPxPerStep = SpeedPx;
            Direction = direction ?? IRandom(0, 359);
            ImageAngle = Direction;
            curveLeft = true;
            headingAlarm = 1f;
            curveAlarm = CurveEverySteps;
            mineAlarm = IRandom(240, 620);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref headingAlarm, steps))
            {
                Direction += IRandom(-20, 20);
                curveLeft = !curveLeft;
                headingAlarm = IRandom(240, 620);
            }
            if (Ring(ref curveAlarm, steps))
            {
                Direction += curveLeft ? IRandom(1, 6) : -IRandom(1, 6);
                Smoked?.Invoke(this);
                curveAlarm = CurveEverySteps;
            }
            if (Ring(ref mineAlarm, steps))
            {
                var mine = new GameObject("EnemyMine").AddComponent<EnemyMine>();
                mine.Init(Position, gameObject, Player, Direction);
                MineLaid?.Invoke(this, mine);
                mineAlarm = IRandom(240, 620);
            }
        }

        protected override void StepEvent(float steps)
        {
            if (OutsideRoom(SizePx)) Direction -= 180f;
            if (!Dagger.ANearB(ImageAngle, Direction, AlignToleranceDegrees))
            {
                bool clockwise = Dagger.CheckRotateClockwise(Direction, ImageAngle);
                ImageAngle += (clockwise ? -TurnDegreesPerStep : TurnDegreesPerStep) * steps;
            }
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
            DeathBurst(DeathEffectSize);
            Die(EnemyDeathCause.Bullet, Points, IsPlayerRound(bullet), true);
            return true;
        }
    }
}
