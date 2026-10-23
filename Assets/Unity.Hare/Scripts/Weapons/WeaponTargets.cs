using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>What dealt a <see cref="WeaponHit"/>.</summary>
    public enum WeaponHitKind
    {
        Laser,
        Railgun,
        Rocket,
        RocketExplosion,
        /// <summary>A flamethrower <see cref="Flame"/>: always lethal (obj_flame destroys par_enemy).</summary>
        Flame
    }

    /// <summary>One weapon contact, handed to <see cref="IWeaponTarget.OnWeaponHit"/>.</summary>
    public readonly struct WeaponHit
    {
        public readonly WeaponHitKind Kind;
        public readonly Vector2 Point;
        /// <summary>Unit vector the hit travelled along (beam or rocket heading; outward for explosions).</summary>
        public readonly Vector2 Direction;

        public WeaponHit(WeaponHitKind kind, Vector2 point, Vector2 direction)
        {
            Kind = kind;
            Point = point;
            Direction = direction;
        }
    }

    /// <summary>
    /// Anything the player's weapons can strike. In the GML each target owns its reaction (its own
    /// collision event with obj_rocket/obj_rocket_explosion, e.g. par_rock darkens and gets pushed,
    /// obj_meatball spawns the explosion), so the weapon only reports the hit and the target
    /// decides what it costs. A rocket explodes on contact with any target.
    /// </summary>
    public interface IWeaponTarget
    {
        void OnWeaponHit(in WeaponHit hit);
    }

    /// <summary>
    /// GML par_enemy: what the laser and railgun kill along their line (collision_line ...
    /// par_enemy) and what rockets home on (instance_nearest(x, y, par_enemy)). The enemy tickets
    /// implement this on their own components.
    /// </summary>
    public interface IEnemyTarget : IWeaponTarget
    {
    }

    /// <summary>
    /// GML draw_beam's blockers (par_rock, par_structure): the laser beam is drawn only up to the
    /// first of these. The rock and structure tickets implement it.
    /// </summary>
    public interface IBeamBlocker
    {
    }

    /// <summary>GML-to-Unity unit conversions shared by the weapons.</summary>
    public static class Gml
    {
        /// <summary>room_gamescreen's room_speed.</summary>
        public const float StepsPerSecond = 120f;

        /// <summary>GML pixels per Unity world unit (Unity's default sprite import PPU).</summary>
        public const float PixelsPerUnit = 100f;

        /// <summary>room_gamescreen's size in GML pixels.</summary>
        public const float RoomWidthPx = 3840f;
        public const float RoomHeightPx = 2160f;

        /// <summary>
        /// room_gamescreen in world units, centred on the origin, for code that needs room_width /
        /// room_height before levels define their own rooms (UH-T20/UH-T21). GML's y grows downward,
        /// so GML y = 0 is this rect's yMax.
        /// </summary>
        public static Rect Room => new Rect(-Px(RoomWidthPx) / 2f, -Px(RoomHeightPx) / 2f, Px(RoomWidthPx), Px(RoomHeightPx));

        public static float Px(float pixels) => pixels / PixelsPerUnit;
        public static float Steps(float steps) => steps / StepsPerSecond;

        /// <summary>GML lengthdir as a unit vector: degrees, 0 = +x, CCW positive.</summary>
        public static Vector2 Dir(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        /// <summary>The nearest component implementing <typeparamref name="T"/> whose collider sits on the segment a→b, or null.</summary>
        public static T FirstAlong<T>(Vector2 a, Vector2 b, out Vector2 point) where T : class
        {
            point = b;
            Physics2D.SyncTransforms();
            var hits = Physics2D.LinecastAll(a, b, Physics2D.AllLayers, -Mathf.Infinity, Mathf.Infinity);
            foreach (var hit in hits) // LinecastAll sorts by distance
            {
                var found = hit.collider.GetComponentInParent<T>();
                if (found == null || (found is Object o && o == null)) continue;
                point = hit.point;
                return found;
            }
            return null;
        }

        /// <summary>A colored line renderer for beam visuals; the shader falls back to Unity's default when unavailable.</summary>
        public static LineRenderer AddLine(GameObject host, Color color, float width)
        {
            var line = host.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = width;
            line.startColor = line.endColor = color;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null) line.sharedMaterial = new Material(shader);
            return line;
        }
    }
}
