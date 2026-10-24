using System;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="EnemyMineBody"/> (obj_enemy_mine, par_projectile, laid by a <see cref="Trilobyte"/>). Not a
    /// par_enemy: beams, flame and rocket explosions ignore it and the ship can touch it harmlessly, but any bullet
    /// (whoever fired it) or rocket sets it off where the shot struck. Core holds the arming, the chase and the fuse.
    /// </summary>
    public class EnemyMine : EnemyBodyView
    {
        public const float ArmSteps = EnemyMineBody.ArmSteps;
        public const int Points = EnemyMineBody.Points;
        /// <summary>spr_mine's bbox (18x11).</summary>
        public static readonly Vector2 SizePx = new Vector2(18f, 11f);

        EnemyMineBody Rules => (EnemyMineBody)Core;

        public bool IsActive => Rules.IsActive;
        public bool IsGone => IsDead;
        /// <summary>GML owner: the view of whoever laid it.</summary>
        public Component Owner => EnemyDirector.ViewOf<Component>(Rules.Owner.Id);
        public float FuseStepsLeft => Rules.FuseStepsLeft;

        public event Action<EnemyMine> Armed;
        public event Action<EnemyMine> Pinged;
        /// <summary>Raised once as it goes, with how (and whether it scored).</summary>
        public event Action<EnemyMine, EnemyKill> Detonated;

        /// <summary>A mine laid at <paramref name="position"/> by <paramref name="owner"/> (a trilobyte, or null for a bare one).</summary>
        /// <param name="imageAngle">The layer's heading (the trilobyte sets mine.image_angle = direction); random when null.</param>
        public void Init(Vector2 position, EnemyBodyView owner = null, PlayerShipController player = null, float? imageAngle = null, int? fuseSeconds = null)
        {
            var result = EnemyDirector.Execute(new LayMine(owner != null ? owner.Core : null, position.ToVec2(), imageAngle, fuseSeconds), this);
            Attach(result.Body, player);
        }

        protected override void Configure()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(Gml.Px(SizePx.x), Gml.Px(SizePx.y));
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            if (e is MineArmed) Armed?.Invoke(this);
            else if (e is MinePinged) Pinged?.Invoke(this);
            else WatchKill(e);
        }

        protected override void OnKilled(EnemyKill kill) => Detonated?.Invoke(this, kill);
    }
}
