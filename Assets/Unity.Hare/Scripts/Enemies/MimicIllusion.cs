using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_mimic_illusion (gmlHARE, par_enemy, masked by spr_rock1): the fake rock riding on
    /// a <see cref="Mimic"/>, drawn with the mimic's rock sprite, angle and illusion alpha. While
    /// the disguise is complete it stops every bullet (any owner) and blows up every rocket without
    /// harm to either of them; otherwise shots pass through. As a par_enemy it still hurts the ship
    /// on contact, draws rockets and mines, and dies to the laser, railgun or flame (the mimic
    /// lives on, undisguised but still fading). It removes itself once the mimic is gone.
    /// </summary>
    public class MimicIllusion : SetPieceEnemy
    {
        /// <summary>spr_rock1's bbox (4..238 x 4..163, origin 121,85).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(234f, 159f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(0f, 1.5f);

        public Mimic Parent { get; private set; }

        protected override Vector2 BBoxHalfPx => HitboxSizePx / 2f;
        protected override Vector2 BBoxOffsetPx => HitboxOffsetPx;

        public void Init(Mimic parent)
        {
            Parent = parent;
            Setup(parent.Position, parent.Room, parent.Player);
            UseEllipse(HitboxSizePx, HitboxOffsetPx);
            SpeedPxPerStep = 0f;
        }

        bool ParentDisguised => Parent != null && !Parent.IsDead && Parent.IsDisguised;

        protected override void Alarms(float steps) { }

        protected override void StepEvent(float steps)
        {
            if (Parent == null || Parent.IsDead)
            {
                Die(EnemyDeathCause.Orphaned, 0, false, false);
                return;
            }
            Teleport(Parent.Position);
            ImageAlpha = Parent.IllusionAlpha;
        }

        protected override bool RocketHit(Rocket rocket) => ParentDisguised;

        protected override bool BulletHit(Bullet bullet) => ParentDisguised;
    }
}
