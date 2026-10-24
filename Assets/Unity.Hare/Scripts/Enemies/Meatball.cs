using System;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="MeatballEnemy"/> (obj_meatball): Core holds its colour, bursts and sprays; this shows them.</summary>
    public class Meatball : SetPieceEnemy
    {
        public const int FullRgb = MeatballEnemy.FullRgb;
        public const int BurstRgb = MeatballEnemy.BurstRgb;
        public const int Points = 50;
        /// <summary>spr_meatball's bbox centre sits 34 px above its origin (meatball_gib uses y - 34).</summary>
        public static readonly Vector2 CentreOffsetPx = new Vector2(-0.5f, 34f);

        MeatballEnemy Rules => (MeatballEnemy)Core;

        /// <summary>GML RGB: image_blend's grey level.</summary>
        public int Rgb => Rules.Rgb;
        /// <summary>GML eyeX/eyeY in px (GML axes).</summary>
        public Vector2Int EyeOffset => new Vector2Int(Rules.EyeX, Rules.EyeY);

        /// <summary>Alarm 0's pulse (ef_ring + ef_smokeup) with the bullets it sprayed.</summary>
        public event Action<Meatball, int> Sprayed;

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null) =>
            Begin(EnemyKind.Meatball, position, room, player, null);

        protected override void Configure()
        {
            UseEllipse(new Vector2(178f, 167f), CentreOffsetPx);
            AddDamageSource();
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            if (e is MeatballSprayed sprayed) Sprayed?.Invoke(this, sprayed.Bullets);
            base.OnEnemyEvent(e);
        }
    }
}
