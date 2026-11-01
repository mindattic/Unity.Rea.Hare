using System;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>obj_*_gib's random Create, dealt from the world's random source so a seed reproduces the scatter.</summary>
    public static class Gibs
    {
        /// <summary>One gib with obj_*_gib's random Create.</summary>
        public static DebrisSpec Gib(IRandomSource rng, DebrisKind kind, Vec2 position)
        {
            int variant = kind == DebrisKind.MeatballGib ? rng.IRandom(1, 6) : kind == DebrisKind.WormGib ? rng.IRandom(1, 8) : 1;
            return new DebrisSpec(kind, position, variant,
                rng.IRandom(0, 359),
                rng.IRandom(1, 3),
                rng.IRandom(0, 359),
                rng.IRandom(-5, 5),
                1f,
                0.001f * rng.IRandom(4, 10));
        }

        /// <summary>GML meatball_gib / worm_gib(x, y): min-max gibs within 40 px of (x, y - 34). Returns how many.</summary>
        public static int Scatter(IRandomSource rng, DebrisKind kind, Vec2 at, int min, int max, Action<DebrisSpec> thrown)
        {
            int count = rng.IRandom(min, max);
            for (int i = 0; i < count; i++)
            {
                // GML y grows downward, so Y - 34 is 34 px up.
                var offset = new Vec2(rng.IRandom(-40, 40), 34f - rng.IRandom(-40, 40));
                thrown(Gib(rng, kind, at + new Vec2(GameClock.Px(offset.X), GameClock.Px(offset.Y))));
            }
            return count;
        }
    }
}
