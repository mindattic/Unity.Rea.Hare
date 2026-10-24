using System;

namespace Unity.Hare
{
    /// <summary>A 2D point or vector in world units (y up) that Core's rules use, so Core has no engine reference.</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 Right => new Vec2(1f, 0f);

        public float SqrMagnitude => X * X + Y * Y;
        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        public Vec2 Normalized
        {
            get
            {
                float m = Magnitude;
                return m > 1e-9f ? new Vec2(X / m, Y / m) : Zero;
            }
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() * 397 ^ Y.GetHashCode();
        public override string ToString() => "(" + X + ", " + Y + ")";

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);
        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);
        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);
    }

    /// <summary>A room's bounds in world units (y up): GML's room_width/room_height with y = 0 at <see cref="YMax"/>.</summary>
    public readonly struct RoomBounds
    {
        public readonly float XMin, YMin, XMax, YMax;

        public RoomBounds(float xMin, float yMin, float xMax, float yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        /// <summary>room_gamescreen: 3840 x 2160 px, centred on the origin.</summary>
        public static RoomBounds Gamescreen
        {
            get
            {
                float w = GameClock.Px(3840f), h = GameClock.Px(2160f);
                return new RoomBounds(-w / 2f, -h / 2f, w / 2f, h / 2f);
            }
        }

        public float Width => XMax - XMin;
        public float Height => YMax - YMin;
        public float CenterY => (YMin + YMax) / 2f;
    }
}
