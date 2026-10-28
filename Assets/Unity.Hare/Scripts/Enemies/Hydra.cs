using System;
using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="HydraEnemy"/> (obj_hydra): its rules live in Core; this shows the body and plays its shots.</summary>
    public class Hydra : SetPieceEnemy
    {
        public const int MinArms = HydraEnemy.MinArms;
        public const int MaxArms = HydraEnemy.MaxArms;
        public const float MuzzlePx = HydraEnemy.MuzzlePx;
        public const int Points = 10;

        HydraEnemy Rules => (HydraEnemy)Core;

        protected override string BodySprite => "spr_hydra_body";

        /// <summary>GML arms: turrets left; it dies on the hit that takes this below 0.</summary>
        public int Arms => Rules.Arms;
        /// <summary>GML rot_dir: +1 or -1 degrees/step.</summary>
        public int RotationDirection => Rules.RotationDirection;

        public float TurretAngle(int i) => Rules.TurretAngle(i);
        public float TurretTarget(int i) => Rules.TurretTarget(i);
        public bool TurretReady(int i) => Rules.TurretReady(i);

        /// <summary>A turret fired (GML audio_play_sound(sfx_blipgun)).</summary>
        public event Action<Hydra, Bullet> Fired;

        /// <summary>GML Create. Optional values pin the random ones for tests.</summary>
        public void Init(Vector2 position, Rect? room = null, Unity.Hare.Player.PlayerShipController player = null,
            int? arms = null, int? rotationDirection = null) =>
            Begin(EnemyKind.Hydra, position, room, player, new EnemyOptions { Arms = arms, RotationDirection = rotationDirection });

        protected override void Configure()
        {
            UseEllipse(new Vector2(30f, 30f));
            AddDamageSource();
        }

        public override void OnRound(RoundFired fired, Bullet bullet)
        {
            if (fired.Turret >= 0) Fired?.Invoke(this, bullet);
        }
    }
}
