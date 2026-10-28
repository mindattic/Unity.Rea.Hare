using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="SawbladeEnemy"/> (obj_sawblade); the shield wall reaches it as an <see cref="IShieldable"/>.</summary>
    public class Sawblade : SetPieceEnemy, IShieldable
    {
        public const int Points = 10;

        SawbladeEnemy Rules => (SawbladeEnemy)Core;

        protected override string BodySprite => "spr_sawblade";

        /// <summary>obj_sawblade Draw: spr_sawblade at (x, y) turned to back_angle, then draw_self.</summary>
        protected override void DrawLayers(int bodyOrder) =>
            DrawLayer("back", "spr_sawblade", bodyOrder, 1, Position, BackAngle);

        /// <summary>GML rot_speed, degrees/step.</summary>
        public float Spin => Rules.Spin;
        public float BackAngle => Rules.BackAngle;
        /// <summary>GML destX/destY: the swarm point.</summary>
        public Vector2 SwarmPoint => Rules.SwarmPoint.ToVector2();

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null) =>
            Begin(EnemyKind.Sawblade, position, room, player, null);

        protected override void Configure()
        {
            UseCircle(15.5f);
            AddDamageSource();
        }

        /// <summary>GML collision with obj_shieldwall: 10 points, a pickup, a tiny explosion.</summary>
        public void OnShieldHit(ShieldWall wall, Vector2 normal)
        {
            if (!IsDead) Send(new StrikeShield(Core.Id));
        }
    }
}
