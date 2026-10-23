using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_phalanx_shield (gmlHARE, par_enemy, spr_shield_hitbox: the half disc in front of its
    /// angle). Each step it sits on its phalanx, flickers (alpha 0.3-0.6), cycles its 6-frame
    /// spr_shieldwall_anime and turns 0.2 degrees toward the ship the short way. Bullets and rockets
    /// bounce off it along the normal (obj_bullet/obj_rocket collision with obj_phalanx_shield,
    /// once per shot: last_impact) instead of hitting the body, enemy rounds included. Being a
    /// par_enemy, it hurts the ship on contact, draws rockets and mines, and the laser, railgun or
    /// flame destroy it, leaving the phalanx bare. It removes itself once its phalanx is gone.
    /// (Its rot_speed, flipped every 120-240 steps by Alarm 0, is never used, so it is not ported.)
    /// </summary>
    public class PhalanxShield : SetPieceEnemy
    {
        public const float TurnDegreesPerStep = 0.2f;
        public const float RadiusPx = 46f;
        public const int Frames = 6;
        public const float AnimationEverySteps = 3f;

        float animationAlarm;
        Object lastImpact;

        public Phalanx Parent { get; private set; }
        /// <summary>spr_shieldwall_anime frame.</summary>
        public int CurrentFrame { get; private set; }

        protected override Vector2 BBoxHalfPx => new Vector2(RadiusPx / 2f, RadiusPx);

        public void Init(Phalanx parent)
        {
            Parent = parent;
            Setup(parent.Position, parent.Room, parent.Player);
            var poly = GetComponent<PolygonCollider2D>();
            if (poly == null) poly = gameObject.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            const int arc = 12;
            var points = new Vector2[arc + 1];
            for (int i = 0; i <= arc; i++)
                points[i] = Gml.Dir(-90f + 180f * i / arc) * Gml.Px(RadiusPx);
            poly.points = points;
            ImageAngle = 0f;
            ImageAlpha = 0.1f * IRandom(3, 6);
            CurrentFrame = 0;
            animationAlarm = AnimationEverySteps;
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref animationAlarm, steps))
            {
                NextFrame();
                animationAlarm = AnimationEverySteps;
            }
        }

        protected override void StepEvent(float steps)
        {
            if (Parent == null || Parent.IsDead)
            {
                Die(EnemyDeathCause.Orphaned, 0, false, false);
                return;
            }
            Teleport(Parent.Position);
            ImageAlpha = 0.1f * IRandom(3, 6);
            if (HasPlayer)
            {
                float playerAngle = PointDirection(Position, PlayerPosition);
                bool clockwise = Dagger.CheckRotateClockwise(playerAngle, ImageAngle);
                ImageAngle += (clockwise ? -TurnDegreesPerStep : TurnDegreesPerStep) * steps;
            }
            NextFrame();
            SpeedPxPerStep = 0f;
        }

        void NextFrame() => CurrentFrame = CurrentFrame < Frames - 1 ? CurrentFrame + 1 : 0;

        /// <summary>GML normal_detect off the shield: outward from its centre through <paramref name="point"/>.</summary>
        public Vector2 NormalAt(Vector2 point)
        {
            Vector2 n = point - Position;
            return n.sqrMagnitude > Mathf.Epsilon ? n.normalized : Gml.Dir(ImageAngle);
        }

        protected override bool RocketHit(Rocket rocket)
        {
            if (rocket != null) rocket.Deflect(this, NormalAt(rocket.Position));
            return false;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            if (bullet == null || bullet == lastImpact) return false;
            lastImpact = bullet;
            Vector2 n = NormalAt(bullet.transform.position);
            bullet.SetDirection(Mathf.Atan2(n.y, n.x) * Mathf.Rad2Deg);
            return false;
        }
    }
}
