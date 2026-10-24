using System;

namespace Unity.Hare
{
    /// <summary>
    /// The one source of randomness the rules use. GML's irandom_range and random_range become these two calls, so a
    /// seeded source makes a whole run reproducible and Core never reaches for an engine random.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>GML irandom_range(min, max): a whole number in [min, max], either order.</summary>
        int IRandom(int min, int max);

        /// <summary>GML random_range(min, max): a real in [min, max).</summary>
        float Range(float min, float max);
    }

    /// <summary>A reproducible <see cref="IRandomSource"/>: the same seed always deals the same numbers.</summary>
    public sealed class SeededRandom : IRandomSource
    {
        readonly Random random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        public int Seed { get; }

        public int IRandom(int min, int max) => min <= max ? random.Next(min, max + 1) : random.Next(max, min + 1);

        public float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
    }
}
