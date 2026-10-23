using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_worm_segment (gmlHARE, par_enemy): one link of a <see cref="Worm"/>. While both its
    /// parent (the part ahead) and the head live, it moves toward the parent at the head's speed
    /// whenever the parent's centre is more than radius (4) px outside its bounding box, so links
    /// trail about a body width apart; otherwise it keeps its last velocity. Its sprite spins like
    /// the head's. Hits work as on the head (see <see cref="Worm"/>), for 10 points.
    /// </summary>
    public class WormSegment : SetPieceEnemy
    {
        public const int Points = 10;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Large;

        float spinAlarm;

        public Worm Head { get; private set; }
        /// <summary>GML parent: the part this one follows.</summary>
        public SetPieceEnemy Parent { get; private set; }
        /// <summary>GML child: the next segment back, or null for the tail.</summary>
        public WormSegment Child { get; internal set; }
        public bool IsExposed => Child == null || Child.IsDead;
        public float RadiusPx { get; private set; }
        public int Rgb { get; private set; }
        public float Spin { get; private set; }
        public float BackAngle { get; private set; }

        protected override Vector2 BBoxHalfPx => new Vector2(56.5f, 56.5f);

        public void Init(Worm head, SetPieceEnemy parent, float radiusPx)
        {
            Head = head;
            Parent = parent;
            RadiusPx = radiusPx;
            Setup(head.Position, head.Room, head.Player);
            UseCircle(56.5f);
            SpeedPxPerStep = 0f;
            Rgb = Worm.FullRgb;
            BackAngle = IRandom(0, 359);
            Spin = IRandom(-3, 3);
            spinAlarm = IRandom(60, 240);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref spinAlarm, steps))
            {
                Spin = IRandom(-2, 2);
                spinAlarm = IRandom(60, 240);
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            BackAngle = Mathf.Repeat(BackAngle - Spin * steps, 360f);
            if (Parent != null && !Parent.IsDead && Head != null && !Head.IsDead && DistanceToPointPx(Parent.Position) > RadiusPx)
                MoveTowardsPoint(Parent.Position, Head.SpeedPxPerStep);
        }

        protected override bool RocketHit(Rocket rocket)
        {
            if (IsExposed)
            {
                if (Rgb > Worm.DeathRgb) Rgb -= Worm.RocketDamage;
                else
                {
                    TinyExplosionAt(rocket);
                    Gib(EnemyDeathCause.Rocket, true);
                }
            }
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            // Every bullet puffs where it is spent; the kill branch adds a second puff.
            TinyExplosionAt(bullet);
            if (IsExposed)
            {
                if (Rgb > Worm.DeathRgb) Rgb -= Worm.BulletDamage;
                else
                {
                    TinyExplosionAt(bullet);
                    Gib(EnemyDeathCause.Bullet, IsPlayerRound(bullet));
                }
            }
            return true;
        }

        void Gib(EnemyDeathCause cause, bool awardsScore)
        {
            Vector2 at = Position;
            GrenadeExplosion.Spawn(at);
            DeathBurst(DeathEffectSize);
            Die(cause, Points, awardsScore, true);
            FadingDebris.WormGibs(at);
            if (Head != null && !Head.IsDead) Head.OnSegmentKilled();
        }
    }
}
