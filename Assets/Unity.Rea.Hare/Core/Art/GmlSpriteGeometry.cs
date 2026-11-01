using System;

namespace Unity.Rea.Hare.Art
{
    /// <summary>A sprite.gmx colkind: the shape GameMaker tests collisions against.</summary>
    public enum GmlMaskKind
    {
        /// <summary>Per pixel, inside the bounding box (GameMaker's "precise").</summary>
        Precise = 0,
        Rectangle = 1,
        /// <summary>The ellipse inscribed in the bounding box.</summary>
        Ellipse = 2,
        /// <summary>The diamond through the midpoints of the bounding box's edges.</summary>
        Diamond = 3,
    }

    /// <summary>
    /// A sprite.gmx bounding box in the frame's pixels: x right and y down from the top left, every edge inclusive
    /// (bbox_right is the last covered column), exactly as GameMaker stores it.
    /// </summary>
    public readonly struct GmlBoundingBox
    {
        public readonly int Left, Top, Right, Bottom;

        public GmlBoundingBox(int left, int top, int right, int bottom)
        {
            if (right < left || bottom < top) throw new ArgumentException($"Empty bounding box ({left},{top})-({right},{bottom})");
            Left = left; Top = top; Right = right; Bottom = bottom;
        }

        public int Width => Right - Left + 1;
        public int Height => Bottom - Top + 1;
    }

    /// <summary>
    /// The one place a GameMaker sprite's geometry becomes the rebuild's (docs/BIBLE.md, art pipeline): its origin as a
    /// pivot, its collision mask as an outline, and its image_speed as an animation frame rate. Engine-free, so the
    /// importer and the runtime both use it and it is tested outside Unity.
    /// </summary>
    public static class GmlSpriteGeometry
    {
        /// <summary>
        /// The GML origin (xorig, yorig from the top left, in pixels) as a pivot fraction from the bottom left, the
        /// way Unity measures a sprite's pivot.
        /// </summary>
        public static Vec2 Pivot(int xorig, int yorig, int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException($"Sprite size {width}x{height} must be positive");
            return new Vec2((float)xorig / width, (float)(height - yorig) / height);
        }

        /// <summary>
        /// image_speed frames per step at room_speed steps per second, as frames per second. GameMaker advances
        /// image_index by image_speed every step, so each frame shows for 1 / image_speed steps.
        /// </summary>
        public static float FramesPerSecond(float imageSpeed, float roomSpeed) => imageSpeed * roomSpeed;

        /// <summary>
        /// The collision mask's outline in pixels relative to the origin, y up (so it lines up with a sprite pivoted on
        /// that origin), counter-clockwise. Precise masks have no closed-form shape and give null: their shape is the
        /// sprite's own alpha, which the importer turns into the sprite's physics shape.
        /// </summary>
        public static Vec2[] MaskOutline(GmlMaskKind kind, GmlBoundingBox box, int xorig, int yorig, int ellipseSegments = 32)
        {
            // The box covers pixels Left..Right, so its far edge is at Right + 1.
            float left = box.Left - xorig, right = box.Right + 1 - xorig;
            float top = yorig - box.Top, bottom = yorig - (box.Bottom + 1);
            float cx = (left + right) / 2f, cy = (top + bottom) / 2f;
            switch (kind)
            {
                case GmlMaskKind.Precise:
                    return null;
                case GmlMaskKind.Rectangle:
                    return new[] { new Vec2(left, bottom), new Vec2(right, bottom), new Vec2(right, top), new Vec2(left, top) };
                case GmlMaskKind.Diamond:
                    return new[] { new Vec2(cx, bottom), new Vec2(right, cy), new Vec2(cx, top), new Vec2(left, cy) };
                case GmlMaskKind.Ellipse:
                    if (ellipseSegments < 4) throw new ArgumentOutOfRangeException(nameof(ellipseSegments), "An ellipse needs at least 4 segments");
                    float rx = (right - left) / 2f, ry = (top - bottom) / 2f;
                    var points = new Vec2[ellipseSegments];
                    for (int i = 0; i < ellipseSegments; i++)
                    {
                        double a = 2.0 * Math.PI * i / ellipseSegments - Math.PI / 2.0;
                        points[i] = new Vec2(cx + rx * (float)Math.Cos(a), cy + ry * (float)Math.Sin(a));
                    }
                    return points;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GameMaker collision kind");
            }
        }
    }
}
