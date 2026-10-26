using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Pickups;
using Unity.Hare.Rocks;
using Unity.Hare.Structures;
using Unity.Hare.Weapons;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// setup_level's "Clear Objects" block (U.HARE-T56), run before a level places anything:
    /// <c>with(par_rock)</c>, <c>with(par_structure)</c>, <c>with(par_structure_cover)</c>, <c>with(par_enemy)</c>,
    /// <c>with(par_projectile)</c>, <c>with(par_enemy_emitter)</c> and <c>with(par_pickup) instance_destroy()</c>. Each goes
    /// with no score, effect, drop or replacement: a rock's <see cref="Rock.Clear"/> (a ship on it lets go), a
    /// structure's <see cref="Structure.Clear"/> (its cover with it), the enemy world's <see cref="EnemyDirector.ClearAll"/>
    /// (obj_enemy_mine, par_projectile's enemy child, is an enemy view there), the ship's and enemies' rounds
    /// (<see cref="Bullet"/>), rockets and mines (par_projectile's obj_bullet, obj_rocket, obj_mine), and the pickups.
    /// The ship itself (with(obj_player)) is the death and respawn flow's, which keeps the one ship and resets it.
    /// </summary>
    public static class LevelReset
    {
        public static void ClearObjects()
        {
            foreach (var rock in Object.FindObjectsByType<Rock>(FindObjectsSortMode.InstanceID))
                if (rock != null && !rock.IsGone) rock.Clear();
            foreach (var structure in Object.FindObjectsByType<Structure>(FindObjectsSortMode.InstanceID))
                if (structure != null && !structure.IsGone) structure.Clear();
            // A cover whose structure was destroyed some other way still goes (with(par_structure_cover)).
            foreach (var cover in Object.FindObjectsByType<StructureCover>(FindObjectsSortMode.InstanceID))
                if (cover != null && !cover.IsGone) cover.Clear();

            EnemyDirector.ClearAll();

            foreach (var bullet in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.InstanceID)) Remove(bullet);
            foreach (var rocket in Object.FindObjectsByType<Rocket>(FindObjectsSortMode.InstanceID)) Remove(rocket);
            foreach (var mine in Object.FindObjectsByType<Mine>(FindObjectsSortMode.InstanceID)) Remove(mine);

            PickupDirector.ClearPickups();
        }

        static void Remove(Component projectile)
        {
            if (projectile == null) return;
            if (Application.isPlaying) Object.Destroy(projectile.gameObject); else Object.DestroyImmediate(projectile.gameObject);
        }
    }
}
