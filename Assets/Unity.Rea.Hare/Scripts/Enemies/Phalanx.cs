using UnityEngine;
using Unity.Rea.Hare.Player;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>The view on <see cref="PhalanxEnemy"/> (obj_phalanx): the body that creeps behind its <see cref="PhalanxShield"/>.</summary>
    public class Phalanx : SetPieceEnemy
    {
        public const int Points = 20;

        PhalanxEnemy Rules => (PhalanxEnemy)Core;

        protected override string BodySprite => "spr_phalanx_body";

        /// <summary>obj_phalanx Draw: spr_phalanx_body at (x, y) turned to back_angle, then draw_self.</summary>
        protected override void DrawLayers(int bodyOrder) =>
            DrawLayer("back", "spr_phalanx_body", bodyOrder, 1, Position, BackAngle);

        /// <summary>GML back_angle: the under-layer's angle.</summary>
        public float BackAngle => Rules.BackAngle;
        /// <summary>GML shield: the obj_phalanx_shield created with it.</summary>
        public PhalanxShield Shield => EnemyDirector.ViewOf<PhalanxShield>(Rules.Shield.Id);

        /// <summary>GML Create, which also creates the shield (shield.parent = id).</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null) =>
            Begin(EnemyKind.Phalanx, position, room, player, null);

        protected override void Configure()
        {
            UseBox(new Vector2(30f, 30f));
            AddDamageSource();
        }
    }
}
