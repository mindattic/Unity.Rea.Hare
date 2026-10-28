using System;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_mimic (gmlHARE, par_enemy): a creature disguised as a rock. It drifts along GML's default
    /// direction 0 at -1.5..1.5 px/step (anything within 0.03 of 0 rounds to a standstill), spinning -0.15..0.15
    /// degrees a step, and reverses once 200 px outside the room. Its <see cref="MimicIllusionBody"/> draws one of
    /// spr_rock1-5 or spr_asteroid_experiment over it. Within 275 px of the ship the disguise fades (illusion alpha
    /// -0.02, the mimic +0.02 a step) until the mimic is fully shown; farther away it fades back.
    ///
    /// While the disguise is complete (illusion alpha 1) shots strike the illusion, which soaks them up harmlessly,
    /// and the mimic lets them through. Once it slips, any rocket or bullet not an enemy's kills the mimic for 10
    /// points (a rocket always scores, a bullet only the ship's) and a pickup, and a bullet's kill also sets off an
    /// obj_rocket_explosion where the bullet was. No push-apart, rocket-explosion or shield-wall events.
    /// </summary>
    public sealed class MimicEnemy : EnemyBody
    {
        public const float RevealRangePx = 275f;
        public const float FadePerStep = 0.02f;
        public const float SizePx = 200f;
        /// <summary>illusion_asset: 1-5 are spr_rock1-5, 6 is spr_asteroid_experiment.</summary>
        public const int IllusionAssets = 6;

        public MimicEnemy() => Kind = EnemyKind.Mimic;

        /// <summary>GML spin, degrees/step.</summary>
        public float Spin { get; private set; }
        public float IllusionAngle { get; private set; }
        /// <summary>GML illusion_alpha: 1 is fully disguised.</summary>
        public float IllusionAlpha { get; private set; }
        /// <summary>Which rock it looks like (see <see cref="IllusionAssets"/>).</summary>
        public int IllusionAsset { get; private set; }
        public MimicIllusionBody Illusion { get; private set; }
        /// <summary>GML illusion_alpha == 1, which GML compares within its 0.00001 epsilon.</summary>
        public bool IsDisguised => Math.Abs(IllusionAlpha - 1f) <= 1e-5f;

        /// <summary>GML Create, which also creates the illusion. Optional values pin the random ones.</summary>
        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 0f;
            float speed = o.SpeedPxPerStep ?? Rng.IRandom(-15, 15) * 0.1f;
            SpeedPxPerStep = GmlMath.ANearB(speed, 0f, 0.03f) ? 0f : speed;
            Direction = 0f;
            ImageAngle = Rng.IRandom(0, 359);
            IllusionAngle = Rng.IRandom(0, 359);
            IllusionAlpha = 1f;
            IllusionAsset = Rng.IRandom(1, IllusionAssets);
            float s = o.Spin ?? Rng.IRandom(-15, 15) * 0.01f;
            Spin = GmlMath.ANearB(s, 0f, 0.03f) ? 0f : s;
            Illusion = new MimicIllusionBody(this);
            Spawn(Illusion);
        }

        protected override void Alarms(float steps) { }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            IllusionAngle = GmlMath.Wrap(IllusionAngle + Spin * steps);
            if (OutsideRoom(SizePx)) Direction -= 180f;

            if (DistanceToPlayerPx() < RevealRangePx)
            {
                if (IllusionAlpha > 0f)
                {
                    ImageAlpha += FadePerStep * steps;
                    IllusionAlpha -= FadePerStep * steps;
                }
                else
                {
                    ImageAlpha = 1f;
                    IllusionAlpha = 0f;
                }
            }
            else
            {
                if (IllusionAlpha < 1f)
                {
                    ImageAlpha -= FadePerStep * steps;
                    IllusionAlpha += FadePerStep * steps;
                }
                else
                {
                    ImageAlpha = 0f;
                    IllusionAlpha = 1f;
                }
            }
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            if (IllusionAlpha >= 1f) return false;
            DeathBurst();
            Die(EnemyDeathCause.Rocket, EnemyTable.Get(Kind).Points, true, true);
            return true;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            if (IllusionAlpha >= 1f || IgnoresRound(shot)) return false;
            TinyExplosionAt(shot.Position);
            Emit(new RocketExplosionRaised { At = shot.Position });
            DeathBurst();
            Die(EnemyDeathCause.Bullet, EnemyTable.Get(Kind).Points, shot.FromPlayer, true);
            return true;
        }
    }

    /// <summary>
    /// The rules of obj_mimic_illusion (gmlHARE, par_enemy, masked by spr_rock1): the fake rock riding on a
    /// <see cref="MimicEnemy"/>, drawn with the mimic's rock sprite, angle and illusion alpha. While the disguise is
    /// complete it stops every bullet (any owner) and blows up every rocket without harm to either of them; otherwise
    /// shots pass through. As a par_enemy it still hurts the ship on contact, draws rockets and mines, and dies to the
    /// laser, railgun or flame (the mimic lives on, undisguised but still fading). It removes itself once the mimic is gone.
    /// </summary>
    public sealed class MimicIllusionBody : EnemyBody
    {
        internal MimicIllusionBody(MimicEnemy parent)
        {
            Kind = EnemyKind.Mimic;
            Parent = parent;
        }

        public override BodyPart Part => BodyPart.Illusion;
        /// <summary>spr_rock1's bbox (4..238 x 4..163, origin 121,85).</summary>
        protected override Vec2 BBoxHalfPx => new Vec2(117f, 79.5f);
        protected override Vec2 BBoxOffsetPx => new Vec2(0f, 1.5f);
        public override bool PushesAway => false;

        public MimicEnemy Parent { get; }

        bool ParentDisguised => Parent != null && !Parent.IsDead && Parent.IsDisguised;

        internal override void Create(EnemyOptions o) => SpeedPxPerStep = 0f;

        protected override void Alarms(float steps) { }

        protected override void StepEvent(float steps)
        {
            if (Parent == null || Parent.IsDead)
            {
                Die(EnemyDeathCause.Orphaned, 0, false, false);
                return;
            }
            Teleport(Parent.Position);
            ImageAlpha = Parent.IllusionAlpha;
        }

        protected override bool RocketHit(Vec2 rocketAt) => ParentDisguised;

        /// <summary>A complete disguise stops the bullet with a tiny explosion (the illusion never moves by speed, so hspeed is 0).</summary>
        protected override bool BulletHit(in BulletShot shot)
        {
            if (!ParentDisguised) return false;
            TinyExplosionAt(shot.Position);
            return true;
        }
    }
}
