using System;
using UnityEngine;

namespace Unity.Rea.Hare.Weapons
{
    /// <summary>
    /// A drop-in par_enemy hitbox: put it beside a collider to make an object something the laser
    /// and railgun kill and rockets home on, before (or instead of) an enemy-specific component.
    /// Every hit raises <see cref="Hit"/>; with <see cref="DestroyOnHit"/> on, the object is
    /// removed the way obj_laser/obj_railgun's with(inst) instance_destroy() removes an enemy.
    /// </summary>
    public class EnemyWeaponTarget : MonoBehaviour, IEnemyTarget
    {
        [SerializeField] bool destroyOnHit;

        public event Action<WeaponHit> Hit;

        public bool DestroyOnHit
        {
            get => destroyOnHit;
            set => destroyOnHit = value;
        }

        public void OnWeaponHit(in WeaponHit hit)
        {
            Hit?.Invoke(hit);
            if (!destroyOnHit) return;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
