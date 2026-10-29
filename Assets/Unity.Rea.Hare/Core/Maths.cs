using System;

namespace Unity.Hare
{
    /// <summary>The few UnityEngine.Mathf helpers the rules need, so Core has no engine reference.</summary>
    internal static class Maths
    {
        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;

        public static float Repeat(float t, float length) => Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);

        public static float DeltaAngle(float current, float target)
        {
            float delta = Repeat(target - current, 360f);
            if (delta > 180f) delta -= 360f;
            return delta;
        }

        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }
}
