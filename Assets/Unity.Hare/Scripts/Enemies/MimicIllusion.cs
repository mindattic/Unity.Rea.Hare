using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="MimicIllusionBody"/> (obj_mimic_illusion, masked by spr_rock1): the fake rock riding on a <see cref="Mimic"/>.</summary>
    public class MimicIllusion : SetPieceEnemy
    {
        /// <summary>spr_rock1's bbox (4..238 x 4..163, origin 121,85).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(234f, 159f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(0f, 1.5f);

        MimicIllusionBody Rules => (MimicIllusionBody)Core;

        public Mimic Parent => EnemyDirector.ViewOf<Mimic>(Rules.Parent.Id);

        protected override void Configure()
        {
            UseEllipse(HitboxSizePx, HitboxOffsetPx);
            AddDamageSource();
        }
    }
}
