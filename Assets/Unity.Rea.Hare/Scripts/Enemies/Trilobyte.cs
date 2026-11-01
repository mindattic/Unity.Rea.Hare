using System;
using UnityEngine;
using Unity.Rea.Hare.Player;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>The view on <see cref="TrilobyteEnemy"/> (obj_trilobyte): sweeps the room and lays <see cref="EnemyMine"/>s.</summary>
    public class Trilobyte : SetPieceEnemy
    {
        public const float SpeedPx = TrilobyteEnemy.SpeedPx;
        public const float SizePx = TrilobyteEnemy.SizePx;
        public const int Points = 10;
        /// <summary>spr_trilobyte's bbox (3..54 x 7..55 of 64x64, origin 32,32).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(52f, 49f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(-3.5f, 1f);

        TrilobyteEnemy Rules => (TrilobyteEnemy)Core;

        protected override string BodySprite => "spr_trilobyte";

        public bool CurvesLeft => Rules.CurvesLeft;

        /// <summary>Alarm 1's ef_smoke puff.</summary>
        public event Action<Trilobyte> Smoked;
        /// <summary>Alarm 2 laid a mine.</summary>
        public event Action<Trilobyte, EnemyMine> MineLaid;

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null, float? direction = null) =>
            Begin(EnemyKind.Trilobyte, position, room, player, new EnemyOptions { Direction = direction });

        protected override void Configure()
        {
            UseBox(HitboxSizePx, HitboxOffsetPx);
            AddDamageSource();
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            if (e is TrilobyteSmoked) Smoked?.Invoke(this);
            base.OnEnemyEvent(e);
        }

        /// <summary>A mine this trilobyte laid has its view now.</summary>
        internal void OnMineLaid(EnemyMine mine) => MineLaid?.Invoke(this, mine);
    }
}
