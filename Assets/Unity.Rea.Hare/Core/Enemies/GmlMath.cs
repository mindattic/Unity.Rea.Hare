using System;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>GML's geometry and angle scripts, as the enemies' rules call them.</summary>
    public static class GmlMath
    {
        /// <summary>GML lengthdir as a unit vector: degrees, 0 = +x, counter-clockwise positive.</summary>
        public static Vec2 Dir(float degrees)
        {
            double r = degrees * Math.PI / 180.0;
            return new Vec2((float)Math.Cos(r), (float)Math.Sin(r));
        }

        /// <summary>Degrees wrapped into [0, 360).</summary>
        public static float Wrap(float degrees) => Maths.Repeat(degrees, 360f);

        /// <summary>GML point_direction(a, b): degrees in [0, 360), 0 for the same point.</summary>
        public static float PointDirection(Vec2 a, Vec2 b)
        {
            Vec2 d = b - a;
            if (d.SqrMagnitude <= 1e-12f) return 0f;
            return Wrap((float)(Math.Atan2(d.Y, d.X) * 180.0 / Math.PI));
        }

        /// <summary>GML a_near_b: plain numeric closeness, no wrap-around.</summary>
        public static bool ANearB(float a, float b, float range) => a <= b + range && a >= b - range;

        /// <summary>
        /// GML check_rotate_clockwise(target, angle): true when the first angle should grow to reach the second the
        /// short way round. (GML angles grow counter-clockwise on screen; the name is the original's.)
        /// </summary>
        public static bool CheckRotateClockwise(float target, float angle)
        {
            float turn;
            if (target > angle) turn = target > angle + 180f ? -(360f - target + angle) : target - angle;
            else turn = angle > target + 180f ? 360f - angle + target : target - angle;
            return turn < 0f;
        }

        /// <summary>Px gap between an axis-aligned box (centre in world units, half size in px) and a point (GML distance_to_point).</summary>
        public static float BoxDistancePx(Vec2 centre, Vec2 halfPx, Vec2 point)
        {
            float dx = Math.Max(0f, Math.Abs((point.X - centre.X) * GameClock.PixelsPerUnit) - halfPx.X);
            float dy = Math.Max(0f, Math.Abs((point.Y - centre.Y) * GameClock.PixelsPerUnit) - halfPx.Y);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
