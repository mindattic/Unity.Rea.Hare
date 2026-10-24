namespace Unity.Hare
{
    /// <summary>
    /// The one place the original's units are converted (docs/BIBLE.md 5.4): room_gamescreen runs at 120 steps a
    /// second with 1 px = 1 unit, and the rebuild keeps 100 px to a world unit. Tuning data is written in the
    /// original's steps and pixels and turned into seconds and units here.
    /// </summary>
    public static class GameClock
    {
        /// <summary>room_gamescreen's room_speed.</summary>
        public const float StepsPerSecond = 120f;

        /// <summary>GML pixels per Unity world unit (Unity's default sprite import PPU).</summary>
        public const float PixelsPerUnit = 100f;

        /// <summary>A step count as seconds.</summary>
        public static float Seconds(float steps) => steps / StepsPerSecond;

        /// <summary>GML pixels as world units.</summary>
        public static float Px(float pixels) => pixels / PixelsPerUnit;
    }
}
