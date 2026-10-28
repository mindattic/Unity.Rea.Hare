using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="PhalanxShieldBody"/> (obj_phalanx_shield, spr_shield_hitbox: the half disc in front of its
    /// angle). Bullets and rockets bounce off it along the normal (Core says to where); it is a par_enemy, so it hurts
    /// the ship on contact, draws rockets and mines, and the laser, railgun or flame destroy it.
    /// </summary>
    public class PhalanxShield : SetPieceEnemy
    {
        public const float RadiusPx = PhalanxShieldBody.RadiusPx;
        public const int Frames = PhalanxShieldBody.Frames;

        PhalanxShieldBody Rules => (PhalanxShieldBody)Core;

        /// <summary>draw_sprite_ext(spr_shieldwall_anime, current_frame) at depth 0.</summary>
        protected override string BodySprite => "spr_shieldwall_anime";
        protected override int BodyFrame => Rules.CurrentFrame;
        protected override int BodyDepth => 0;

        public Phalanx Parent => EnemyDirector.ViewOf<Phalanx>(Rules.Parent.Id);
        /// <summary>spr_shieldwall_anime frame.</summary>
        public int CurrentFrame => Rules.CurrentFrame;

        protected override void Configure()
        {
            var poly = GetComponent<PolygonCollider2D>();
            if (poly == null) poly = gameObject.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            const int arc = 12;
            var points = new Vector2[arc + 1];
            for (int i = 0; i <= arc; i++)
                points[i] = Gml.Dir(-90f + 180f * i / arc) * Gml.Px(RadiusPx);
            poly.points = points;
            AddDamageSource();
        }

        /// <summary>GML normal_detect off the shield: outward from its centre through <paramref name="point"/>.</summary>
        public Vector2 NormalAt(Vector2 point) => Rules.NormalAt(point.ToVec2()).ToVector2();
    }
}
