using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_sawblade (gmlHARE, par_enemy): a small spinning blade (image_angle +rot_speed,
    /// its back layer +rot_speed+1). Farther than 400 px from the ship it drifts in at 0.5 px/step
    /// spinning 3 a step; within 400 px it spins 6 and swarms at 2 px/step toward a swarm point,
    /// re-picked every 30-60 steps (first after 30-120) as the ship's position plus twice its
    /// velocity plus up to 48 px of jitter each way. Until the first pick that point is GML (0, 0),
    /// the room's top-left corner.
    ///
    /// It dies to anything for 10 points and a pickup: the ship's bullets (enemy rounds pass
    /// through; points only for the ship's), rockets, rocket explosions and the shield wall. It
    /// pushes away from overlapping par_enemy.
    /// </summary>
    public class Sawblade : SetPieceEnemy, IShieldable
    {
        public const float SwarmRangePx = 400f;
        public const float DriftSpeedPxPerStep = 0.5f;
        public const float SwarmSpeedPxPerStep = 2f;
        public const float DriftSpin = 3f;
        public const float SwarmSpin = 6f;
        public const float LeadSteps = 2f;
        public const int JitterPx = 48;
        public const int Points = 10;

        float swarmAlarm;

        /// <summary>GML rot_speed, degrees/step.</summary>
        public float Spin { get; private set; }
        public float BackAngle { get; private set; }
        /// <summary>GML destX/destY: the swarm point.</summary>
        public Vector2 SwarmPoint { get; private set; }

        protected override Vector2 BBoxHalfPx => new Vector2(15f, 15f);
        protected override bool PushesAwayFromEnemies => true;

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null)
        {
            Setup(position, room, player);
            UseCircle(15.5f);
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Spin = DriftSpin;
            BackAngle = IRandom(0, 359);
            SwarmPoint = new Vector2(Room.xMin, Room.yMax); // GML destX = destY = 0
            swarmAlarm = IRandom(30, 120);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref swarmAlarm, steps))
            {
                if (HasPlayer)
                {
                    Vector2 leadPx = PlayerVelocityPxPerStep * LeadSteps + new Vector2(IRandom(-JitterPx, JitterPx), IRandom(-JitterPx, JitterPx));
                    SwarmPoint = PlayerPosition + new Vector2(Gml.Px(leadPx.x), Gml.Px(leadPx.y));
                }
                swarmAlarm = IRandom(30, 60);
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            BackAngle = Mathf.Repeat(BackAngle + (Spin + 1f) * steps, 360f);
            FadeIn(steps);
            if (!HasPlayer) { SpeedPxPerStep = 0f; return; }

            if (DistanceToPlayerPx() > SwarmRangePx)
            {
                Spin = DriftSpin;
                MoveTowardsPoint(PlayerPosition, DriftSpeedPxPerStep);
            }
            else
            {
                Spin = SwarmSpin;
                MoveTowardsPoint(SwarmPoint, SwarmSpeedPxPerStep);
            }
        }

        protected override bool RocketHit(Rocket rocket)
        {
            Die(EnemyDeathCause.Rocket, Points, true, true);
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            if (IsEnemyRound(bullet)) return false;
            Die(EnemyDeathCause.Bullet, Points, IsPlayerRound(bullet), true);
            return true;
        }

        protected override void RocketExplosionHit() => Die(EnemyDeathCause.RocketExplosion, Points, true, true);

        /// <summary>GML collision with obj_shieldwall: 10 points, a pickup, a tiny explosion.</summary>
        public void OnShieldHit(ShieldWall wall, Vector2 normal)
        {
            if (!IsDead) Die(EnemyDeathCause.ShieldWall, Points, true, true);
        }
    }
}
