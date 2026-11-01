using UnityEngine;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>The view on <see cref="MimicIllusionBody"/> (obj_mimic_illusion, masked by spr_rock1): the fake rock riding on a <see cref="Mimic"/>.</summary>
    public class MimicIllusion : SetPieceEnemy
    {
        /// <summary>spr_rock1's bbox (4..238 x 4..163, origin 121,85).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(234f, 159f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(0f, 1.5f);

        MimicIllusionBody Rules => (MimicIllusionBody)Core;

        /// <summary>The illusion draws its parent's illusion_asset (spr_rock1..5, spr_asteroid_experiment) at depth 0.</summary>
        protected override string BodySprite
        {
            get
            {
                var parent = Parent;
                int asset = parent != null ? parent.IllusionAsset : 1;
                return asset >= 6 ? "spr_asteroid_experiment" : "spr_rock" + asset;
            }
        }
        protected override int BodyDepth => 0;

        /// <summary>Drawn at the mimic's illusion_alpha and illusion_angle.</summary>
        protected override void Draw()
        {
            base.Draw();
            var parent = Parent;
            var renderer = GetComponent<SpriteRenderer>();
            if (parent == null || renderer == null) return;
            renderer.color = new Color(1f, 1f, 1f, parent.IllusionAlpha);
            transform.rotation = Quaternion.Euler(0f, 0f, parent.IllusionAngle);
        }

        public Mimic Parent => EnemyDirector.ViewOf<Mimic>(Rules.Parent.Id);

        protected override void Configure()
        {
            UseEllipse(HitboxSizePx, HitboxOffsetPx);
            AddDamageSource();
        }
    }
}
