using System;

namespace Unity.Rea.Hare.Player
{
    /// <summary>What the ship perched on: obj_player's par_rock and par_structure collision events.</summary>
    public enum SurfaceKind
    {
        /// <summary>par_rock: the ship rides it (current_rock) and is flung off when it goes.</summary>
        Rock,
        /// <summary>par_structure: the ship stays put on it (current_structure).</summary>
        Structure
    }

    /// <summary>
    /// What the key that leaps on release does when it goes down in flight (U.HARE-T53). Space's press event runs
    /// player_boost; the right mouse button's runs player_brake, or player_boost with Ctrl held.
    /// </summary>
    public enum AirPress
    {
        Boost,
        Brake
    }

    /// <summary>How the ship takes a hit that landed, as each of obj_player's hit collisions reacts before player_hurt.</summary>
    public enum HitReaction
    {
        /// <summary>obj_rocket_explosion: player_hurt only.</summary>
        None,
        /// <summary>obj_bullet, par_enemy: in flight <c>spin = 3</c>; perched, nothing (their fling_player is commented out).</summary>
        Spin,
        /// <summary>obj_rocket: in flight <c>spin = 3</c>; perched on a rock, fling_player and current_rock = noone.</summary>
        SpinOrFling
    }

    /// <summary>
    /// One landing as obj_player's par_rock/par_structure collision works it out: the surface normal it turns to
    /// (normal_detect), and for a rock distance_to_center and land_angle from where the ship ended up, with the rock's
    /// speed and spin for the take-off bonus. Distances are in world units; the normal and land angle in degrees (GML's
    /// convention: 0 = +x, counter-clockwise positive).
    /// </summary>
    public readonly struct LandingSurface
    {
        public LandingSurface(SurfaceKind kind, int id, float normal, float landAngle = 0f, float distanceToCenter = 0f,
            float speedPxPerStep = 0f, float spinPerStep = 0f)
        {
            Kind = kind;
            Id = id;
            Normal = normal;
            LandAngle = landAngle;
            DistanceToCenter = distanceToCenter;
            SpeedPxPerStep = speedPxPerStep;
            SpinPerStep = spinPerStep;
        }

        public SurfaceKind Kind { get; }

        /// <summary>The surface's instance id (GML other.id).</summary>
        public int Id { get; }

        /// <summary>normal_detect's result, the way the ship faces once perched (image_angle = normal); -1 when it found none.</summary>
        public float Normal { get; }

        /// <summary>land_angle = point_direction(ship, rock centre).</summary>
        public float LandAngle { get; }

        /// <summary>distance_to_center = point_distance(ship, rock centre), world units.</summary>
        public float DistanceToCenter { get; }

        /// <summary>The rock's speed, px/step (player_leap and fling_player add abs(current_rock.speed)).</summary>
        public float SpeedPxPerStep { get; }

        /// <summary>The rock's spin, degrees/step (they add abs(current_rock.spin) too).</summary>
        public float SpinPerStep { get; }
    }

    /// <summary>GMLscripts' normal_detect, which the landing collisions turn the ship to.</summary>
    public static class SurfaceNormal
    {
        /// <summary>
        /// normal_detect(x, y, obj, rad, res): scans a disc of <paramref name="radius"/> + 0.5 around the point at
        /// <paramref name="resolution"/>, summing the offsets that are not inside the surface (<paramref name="solid"/>
        /// takes an offset from the point), and returns their direction, or -1 when every offset is solid. The offsets are
        /// y-up, so the angle is GML's point_direction on screen (0 = +x, counter-clockwise positive). The caller makes the
        /// original's collision_circle pre-check (nothing solid in the disc is also -1).
        /// </summary>
        public static float Detect(float radius, float resolution, Func<float, float, bool> solid)
        {
            if (solid == null) throw new ArgumentNullException(nameof(solid));
            if (radius <= 0f) radius = 4f;
            if (resolution <= 0f) resolution = 1f;
            float rad = radius + 0.5f;
            float nx = 0f, ny = 0f;
            for (float j = resolution; j <= rad; j += resolution)
            {
                for (float i = 0f; i < rad; i += resolution)
                {
                    if (Math.Sqrt(i * i + j * j) > rad) continue;
                    if (!solid(i, j)) { nx += i; ny += j; }
                    if (!solid(j, -i)) { nx += j; ny -= i; }
                    if (!solid(-i, -j)) { nx -= i; ny -= j; }
                    if (!solid(-j, i)) { nx -= j; ny += i; }
                }
            }
            if (nx == 0f && ny == 0f) return -1f;
            return Maths.Repeat((float)(Math.Atan2(ny, nx) * 180.0 / Math.PI), 360f);
        }
    }
}
