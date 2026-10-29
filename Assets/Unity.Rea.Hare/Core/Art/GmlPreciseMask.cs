using System;
using System.Collections.Generic;

namespace Unity.Hare.Art
{
    /// <summary>
    /// A sprite's precise (per pixel, colkind 0) collision mask as data (U.HARE-T19): its bounding box in the frame's
    /// pixels (inclusive, as sprite.gmx stores it), its origin, and for each frame the rows of the box, top to bottom,
    /// separated by spaces, <c>#</c> where the pixel's alpha is above the sprite's tolerance (0) and <c>.</c> where it is
    /// not. A pixel outside the box never collides, whatever the image holds there. An empty <see cref="Frames"/> list
    /// is a solid box (spr_destination's is opaque from edge to edge).
    /// </summary>
    [Serializable]
    public class GmlPreciseMask
    {
        public int XOrigin;
        public int YOrigin;
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public string[] Frames = Array.Empty<string>();

        public GmlPreciseMask() { }

        public GmlPreciseMask(int xorig, int yorig, int left, int top, int right, int bottom, params string[] frames)
        {
            XOrigin = xorig;
            YOrigin = yorig;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
            Frames = frames ?? Array.Empty<string>();
        }

        public int Width => Right - Left + 1;
        public int Height => Bottom - Top + 1;

        /// <summary>Whether frame pixel (<paramref name="px"/>, <paramref name="py"/>) of <paramref name="frame"/> is solid.</summary>
        public bool Solid(int frame, int px, int py)
        {
            if (px < Left || px > Right || py < Top || py > Bottom) return false;
            if (Frames == null || Frames.Length == 0) return true;
            string rows = Frames[((frame % Frames.Length) + Frames.Length) % Frames.Length];
            int index = (py - Top) * (Width + 1) + (px - Left);
            return index >= 0 && index < rows.Length && rows[index] == '#';
        }

        /// <summary>
        /// The instance's bbox_left/top/right/bottom in room pixels at (<paramref name="x"/>, <paramref name="y"/>) turned
        /// <paramref name="angle"/> degrees (GameMaker's anticlockwise, y down): the box's corners (its far edges one past
        /// the last pixel) rotated about the origin, then the whole pixels they cover.
        /// </summary>
        public void Bounds(double x, double y, double angle, out int left, out int top, out int right, out int bottom)
        {
            double l = Left - XOrigin, t = Top - YOrigin, r = Right + 1 - XOrigin, b = Bottom + 1 - YOrigin;
            if (angle == 0d)
            {
                left = (int)Math.Floor(x + l);
                top = (int)Math.Floor(y + t);
                right = (int)Math.Ceiling(x + r) - 1;
                bottom = (int)Math.Ceiling(y + b) - 1;
                return;
            }
            double rad = angle * Math.PI / 180d, c = Math.Cos(rad), s = Math.Sin(rad);
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (var (cx, cy) in new[] { (l, t), (r, t), (r, b), (l, b) })
            {
                // A local point (cx, cy) lands at (cx cos a + cy sin a, -cx sin a + cy cos a): anticlockwise on screen.
                double wx = x + cx * c + cy * s, wy = y - cx * s + cy * c;
                if (wx < minX) minX = wx;
                if (wx > maxX) maxX = wx;
                if (wy < minY) minY = wy;
                if (wy > maxY) maxY = wy;
            }
            left = (int)Math.Floor(minX);
            top = (int)Math.Floor(minY);
            right = (int)Math.Ceiling(maxX) - 1;
            bottom = (int)Math.Ceiling(maxY) - 1;
        }

        /// <summary>Whether room pixel (<paramref name="i"/>, <paramref name="j"/>) is solid on an instance of this mask.</summary>
        public bool SolidAt(int frame, double x, double y, double angle, int i, int j)
        {
            double dx = i - x, dy = j - y;
            double rad = angle * Math.PI / 180d, c = Math.Cos(rad), s = Math.Sin(rad);
            // The room point back into the frame: the inverse of the turn above, then the origin.
            int px = (int)Math.Floor(dx * c - dy * s + XOrigin);
            int py = (int)Math.Floor(dx * s + dy * c + YOrigin);
            return Solid(frame, px, py);
        }

        /// <summary>
        /// GameMaker's precise collision of two instances: their bounding boxes must overlap, and some room pixel of the
        /// overlap must be solid on both masks.
        /// </summary>
        public static bool Collide(GmlPreciseMask a, int frameA, double ax, double ay, double angleA,
                                   GmlPreciseMask b, int frameB, double bx, double by, double angleB)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            a.Bounds(ax, ay, angleA, out int al, out int at, out int ar, out int ab);
            b.Bounds(bx, by, angleB, out int bl, out int bt, out int br, out int bb);
            int l = Math.Max(al, bl), r = Math.Min(ar, br), t = Math.Max(at, bt), btm = Math.Min(ab, bb);
            if (l > r || t > btm) return false;
            for (int j = t; j <= btm; j++)
                for (int i = l; i <= r; i++)
                    if (a.SolidAt(frameA, ax, ay, angleA, i, j) && b.SolidAt(frameB, bx, by, angleB, i, j)) return true;
            return false;
        }

        internal void Validate(List<string> errors, string path)
        {
            if (Right < Left) errors.Add($"{path}.Right: must not be left of Left (was {Right}).");
            if (Bottom < Top) errors.Add($"{path}.Bottom: must not be above Top (was {Bottom}).");
            if (Frames == null) { errors.Add(path + ".Frames: must not be null (an empty list for a solid box)."); return; }
            int length = Height * (Width + 1) - 1;
            for (int i = 0; i < Frames.Length; i++)
            {
                var rows = Frames[i];
                if (rows == null || rows.Length != length)
                    errors.Add($"{path}.Frames[{i}]: must be {Height} rows of {Width} '#' or '.' separated by spaces (was {rows?.Length ?? 0} characters).");
            }
        }
    }
}
