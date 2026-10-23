using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_phalanx (gmlHARE, par_enemy): a slow body (two counter-rotating layers, image_angle
    /// +2 and back_angle -2 a step, trailing ef_spark) that creeps toward the ship at 0.5 px/step
    /// until within 100 px, carrying an <see cref="PhalanxShield"/> that turns to face the ship.
    /// Any bullet of the ship's or rocket that reaches the body kills it for 20 points and a pickup;
    /// its rocket-explosion and shield-wall events are empty, enemy rounds pass through, and it has
    /// no push-apart (that event is commented out).
    /// </summary>
    public class Phalanx : SetPieceEnemy
    {
        public const float SpinDegreesPerStep = 2f;
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 0.5f;
        public const int Points = 20;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Medium;

        /// <summary>GML back_angle: the under-layer's angle.</summary>
        public float BackAngle { get; private set; }
        /// <summary>GML shield: the obj_phalanx_shield created with it.</summary>
        public PhalanxShield Shield { get; private set; }

        protected override Vector2 BBoxHalfPx => new Vector2(15f, 15f);

        /// <summary>GML Create, which also creates the shield (shield.parent = id).</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null)
        {
            Setup(position, room, player);
            UseBox(new Vector2(30f, 30f));
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            BackAngle = IRandom(0, 359);
            Shield = new GameObject("PhalanxShield").AddComponent<PhalanxShield>();
            Shield.Init(this);
        }

        protected override void Alarms(float steps) { }

        protected override void StepEvent(float steps)
        {
            ImageAngle += SpinDegreesPerStep * steps;
            BackAngle = Mathf.Repeat(BackAngle - SpinDegreesPerStep * steps, 360f);
            FadeIn(steps);
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
            DeathBurst(DeathEffectSize);
            Die(EnemyDeathCause.Bullet, Points, IsPlayerRound(bullet), true);
            return true;
        }
    }
}
