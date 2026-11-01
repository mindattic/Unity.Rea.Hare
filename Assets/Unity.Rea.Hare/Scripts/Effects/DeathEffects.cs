using UnityEngine;

namespace Unity.Rea.Hare.Effects
{
    /// <summary>
    /// The death burst every gmlHARE enemy shares, sized per enemy: in each kill branch of its
    /// collision events the enemy calls effect_create_above(ef_smokeup, x, y, size, c_black) and
    /// effect_create_above(ef_firework, x, y, size, c_orange) at its own position. Size 0 for the
    /// dagger, hydra, sawblade, trilobyte, wraith and enemy mine; 1 for the phalanx and mimic; 2 for
    /// the meatball and the worm's head and segments (which add an obj_grenade_explosion and their
    /// gibs). Not every kill has it: a rocket explosion's kill, the shield wall's and the laser,
    /// railgun and flame's (which destroy from their side) show none, and the enemy mine's own fuse
    /// and proximity detonations are only the obj_rocket_explosion.
    /// </summary>
    public static class DeathEffects
    {
        public const int Small = 0;
        public const int Medium = 1;
        public const int Large = 2;

        /// <summary>GML c_orange: 4235519, $40A0FF in GameMaker's BGR, so RGB 255, 160, 64.</summary>
        public static readonly Color Orange = new Color32(255, 160, 64, 255);

        /// <summary>globals.gml's c_gold: make_color_rgb(255, 223, 0).</summary>
        public static readonly Color Gold = new Color32(255, 223, 0, 255);

        /// <summary>The ef_smokeup + ef_firework pair at <paramref name="at"/>.</summary>
        public static void Burst(Vector2 at, int size)
        {
            GmlEffects.CreateAbove(GmlEffectKind.SmokeUp, at, size, Color.black);
            GmlEffects.CreateAbove(GmlEffectKind.Firework, at, size, Orange);
        }
    }
}
