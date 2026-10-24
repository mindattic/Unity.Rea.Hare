using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="MimicEnemy"/> (obj_mimic): a creature disguised as a rock, its <see cref="MimicIllusion"/> drawn over it.</summary>
    public class Mimic : SetPieceEnemy
    {
        public const float RevealRangePx = MimicEnemy.RevealRangePx;
        public const float SizePx = MimicEnemy.SizePx;
        public const int IllusionAssets = MimicEnemy.IllusionAssets;
        public const int Points = 10;

        MimicEnemy Rules => (MimicEnemy)Core;

        /// <summary>GML spin, degrees/step.</summary>
        public float Spin => Rules.Spin;
        public float IllusionAngle => Rules.IllusionAngle;
        /// <summary>GML illusion_alpha: 1 is fully disguised.</summary>
        public float IllusionAlpha => Rules.IllusionAlpha;
        /// <summary>Which rock it looks like (see <see cref="IllusionAssets"/>).</summary>
        public int IllusionAsset => Rules.IllusionAsset;
        public MimicIllusion Illusion => EnemyDirector.ViewOf<MimicIllusion>(Rules.Illusion.Id);
        public bool IsDisguised => Rules.IsDisguised;

        /// <summary>GML Create, which also creates the illusion. Optional values pin the random ones.</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null,
            float? speedPxPerStep = null, float? spin = null) =>
            Begin(EnemyKind.Mimic, position, room, player, new EnemyOptions { SpeedPxPerStep = speedPxPerStep, Spin = spin });

        protected override void Configure()
        {
            UseCircle(53.5f);
            AddDamageSource();
        }
    }
}
