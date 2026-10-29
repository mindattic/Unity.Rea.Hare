using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// The physics half of obj_player's par_rock / par_structure collisions (U.HARE-T53): which surface the flying ship's
    /// mask touches, normal_detect around the ship, and the "move out of collision" loop. The ship's masks are its
    /// sprites' (gml-art.json): spr_rabbit_leap's ellipse while it flies, spr_rabbit_idle's rectangle once the landing has
    /// switched it to asset_idle, both turned to image_angle. The rules (what landing does) are Core's
    /// <see cref="ShipFlight.LandOn"/>.
    /// </summary>
    public static class PerchProbe
    {
        /// <summary>spr_rabbit_leap's bbox (5..85, 31..62 about the 48, 48 origin, colkind 2: an ellipse), px.</summary>
        public static readonly Vector2 FlightMaskSizePx = new Vector2(81f, 32f);
        /// <summary>Its centre from the origin, px, Unity axes (y up).</summary>
        public static readonly Vector2 FlightMaskOffsetPx = new Vector2(-2.5f, 1f);

        /// <summary>spr_rabbit_idle's bbox (5..78, 36..68 about the 48, 48 origin, colkind 1: a rectangle), px.</summary>
        public static readonly Vector2 PerchMaskSizePx = new Vector2(74f, 33f);
        public static readonly Vector2 PerchMaskOffsetPx = new Vector2(-6f, -4.5f);

        /// <summary>globals normal_radius and normal_resolution, px.</summary>
        public const float NormalRadiusPx = 43f;
        public const float NormalResolutionPx = 1f;

        /// <summary>A guard on the move-out loop (GML's while has none), px.</summary>
        public const int MaxMoveOutPx = 2000;

        static readonly List<Collider2D> Near = new List<Collider2D>();

        /// <summary>The surfaces the flying ship's mask touches at <paramref name="position"/> facing <paramref name="facing"/>.</summary>
        public static List<IPerchSurface> Touching(Vector2 position, float facing)
        {
            Physics2D.SyncTransforms();
            var found = new List<IPerchSurface>();
            var hits = Physics2D.OverlapCapsuleAll(MaskCentre(position, FlightMaskOffsetPx, facing), Px(FlightMaskSizePx),
                CapsuleDirection2D.Horizontal, facing);
            foreach (var hit in hits)
            {
                var surface = Surface(hit);
                if (surface != null && !found.Contains(surface)) found.Add(surface);
            }
            return found;
        }

        /// <summary>
        /// Every collider the ship's mask overlaps at <paramref name="position"/> turned to <paramref name="facing"/>:
        /// spr_rabbit_idle's rectangle while <paramref name="perched"/>, spr_rabbit_leap's ellipse otherwise (the
        /// collision events with anything, e.g. the pickups').
        /// </summary>
        public static Collider2D[] ShipMaskHits(Vector2 position, float facing, bool perched)
        {
            Physics2D.SyncTransforms();
            return perched
                ? Physics2D.OverlapBoxAll(MaskCentre(position, PerchMaskOffsetPx, facing), Px(PerchMaskSizePx), facing)
                : Physics2D.OverlapCapsuleAll(MaskCentre(position, FlightMaskOffsetPx, facing), Px(FlightMaskSizePx),
                    CapsuleDirection2D.Horizontal, facing);
        }

        /// <summary>
        /// normal_detect(x, y, par_rock or par_structure, normal_radius, normal_resolution): every surface of
        /// <paramref name="kind"/> counts as solid. -1 when none is within the radius or every point is solid.
        /// </summary>
        public static float DetectNormal(Vector2 position, SurfaceKind kind)
        {
            Physics2D.SyncTransforms();
            Near.Clear();
            foreach (var hit in Physics2D.OverlapCircleAll(position, Gml.Px(NormalRadiusPx + 0.5f)))
            {
                var surface = Surface(hit);
                if (surface != null && surface.Kind == kind) Near.Add(hit);
            }
            if (Near.Count == 0) return -1f; // collision_circle found nothing

            return SurfaceNormal.Detect(NormalRadiusPx, NormalResolutionPx, (dx, dy) =>
            {
                var point = position + new Vector2(Gml.Px(dx), Gml.Px(dy));
                foreach (var c in Near)
                    if (c != null && c.OverlapPoint(point)) return true;
                return false;
            });
        }

        /// <summary>
        /// <c>while (place_meeting(x, y, other)) { x += lengthdir_x(1, normal); y += lengthdir_y(1, normal); }</c> with the
        /// perched mask turned to <paramref name="normal"/>. Returns where the ship ends up.
        /// </summary>
        public static Vector2 MoveOut(Vector2 position, float normal, Collider2D mask)
        {
            if (mask == null) return position;
            Physics2D.SyncTransforms();
            Vector2 step = Gml.Dir(normal) * Gml.Px(1f);
            for (int i = 0; i < MaxMoveOutPx && PerchMaskTouches(position, normal, mask); i++) position += step;
            return position;
        }

        /// <summary>place_meeting(x, y, other) with the perched (spr_rabbit_idle) mask.</summary>
        public static bool PerchMaskTouches(Vector2 position, float facing, Collider2D mask)
        {
            var hits = Physics2D.OverlapBoxAll(MaskCentre(position, PerchMaskOffsetPx, facing), Px(PerchMaskSizePx), facing);
            foreach (var hit in hits)
                if (hit == mask) return true;
            return false;
        }

        /// <summary>
        /// GML in_view(x, y): strictly inside view 0, <c>X &gt; view_xview &amp;&amp; X &lt; view_xview + view_wview</c> and the
        /// same on y, all in room pixels.
        /// </summary>
        public static bool InView(float x, float y, float viewX, float viewY, float viewWidth, float viewHeight) =>
            x > viewX && x < viewX + viewWidth && y > viewY && y < viewY + viewHeight;

        static IPerchSurface Surface(Collider2D hit)
        {
            if (hit == null) return null;
            var surface = hit.GetComponentInParent<IPerchSurface>();
            if (surface == null || (surface is Object o && o == null) || surface.IsGone) return null;
            return surface;
        }

        static Vector2 MaskCentre(Vector2 position, Vector2 offsetPx, float facing) =>
            position + (Vector2)(Quaternion.Euler(0f, 0f, facing) * Px(offsetPx));

        static Vector2 Px(Vector2 px) => new Vector2(Gml.Px(px.x), Gml.Px(px.y));
    }
}
