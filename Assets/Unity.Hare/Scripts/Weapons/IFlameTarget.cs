using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Anything a flamethrower <see cref="Flame"/> burns: enemies (GML par_enemy). obj_flame kills
    /// outright (Collision par_enemy: with(other) instance_destroy()) and is not spent, so a flame
    /// burns through every enemy in its path. Rocks and structures don't implement this: the
    /// original gave obj_flame no collision with them. The enemy tickets implement it alongside
    /// <see cref="IProjectileTarget"/>.
    /// </summary>
    public interface IFlameTarget
    {
        void OnFlameHit(Flame flame);
    }

    static class WeaponMath
    {
        public const float StepsPerSecond = 120f; // room_gamescreen speed

        /// <summary>GML lengthdir_x/lengthdir_y in Unity's y-up space (degrees, 0 = +x, CCW positive).</summary>
        public static Vector2 LengthDir(float length, float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad)) * length;

        public static void DestroyObject(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
