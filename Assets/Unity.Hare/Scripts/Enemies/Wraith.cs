using System;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="WraithEnemy"/> (obj_wraith): Core holds its float, charge and stun; this shows them.</summary>
    public class Wraith : SetPieceEnemy
    {
        public const float FloatSpeedPxPerStep = WraithEnemy.FloatSpeedPxPerStep;
        public const float ChargeRangePx = WraithEnemy.ChargeRangePx;
        public const float ChargeOvershootPx = WraithEnemy.ChargeOvershootPx;
        public const float ChargeMaxSpeedPxPerStep = WraithEnemy.ChargeMaxSpeedPxPerStep;
        public const float ArrivalRangePx = WraithEnemy.ArrivalRangePx;
        public const int MinReappearPx = WraithEnemy.MinReappearPx;
        public const int MaxReappearPx = WraithEnemy.MaxReappearPx;
        public const int MinStunSteps = WraithEnemy.MinStunSteps;
        public const int MaxStunSteps = WraithEnemy.MaxStunSteps;
        public const float PulseMinAlpha = WraithEnemy.PulseMinAlpha;
        public const float PulseMaxAlpha = WraithEnemy.PulseMaxAlpha;
        public const int Points = 50;

        WraithEnemy Rules => (WraithEnemy)Core;

        public WraithState State => Rules.State;
        /// <summary>GML facing / image_xscale: 1 faces right, -1 left.</summary>
        public int Facing => Rules.Facing;
        /// <summary>GML destX/destY: where the current charge ends.</summary>
        public Vector2 ChargeTarget => Rules.ChargeTarget.ToVector2();
        /// <summary>Steps left stunned, or negative when not stunned.</summary>
        public float StunStepsLeft => Rules.StunStepsLeft;

        /// <summary>Reappeared beside the ship at the end of a charge.</summary>
        public event Action<Wraith> Reappeared;

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null) =>
            Begin(EnemyKind.Wraith, position, room, player, null);

        protected override void Configure()
        {
            UseEllipse(new Vector2(100f, 112f));
            AddDamageSource();
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            if (e is WraithReappeared) Reappeared?.Invoke(this);
            base.OnEnemyEvent(e);
        }
    }
}
