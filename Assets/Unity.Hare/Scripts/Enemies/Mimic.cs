using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_mimic (gmlHARE, par_enemy): a creature disguised as a rock. It drifts along GML's
    /// default direction 0 at -1.5..1.5 px/step (anything within 0.03 of 0 rounds to a standstill),
    /// spinning -0.15..0.15 degrees a step, and reverses once 200 px outside the room. Its
    /// <see cref="MimicIllusion"/> draws one of spr_rock1-5 or spr_asteroid_experiment over it.
    /// Within 275 px of the ship the disguise fades (illusion alpha -0.02, the mimic +0.02 a step)
    /// until the mimic is fully shown; farther away it fades back.
    ///
    /// While the disguise is complete (illusion alpha 1) shots strike the illusion, which soaks
    /// them up harmlessly, and the mimic lets them through. Once it slips, any rocket or bullet not
    /// an enemy's kills the mimic for 10 points (a rocket always scores, a bullet only the ship's)
    /// and a pickup, and a bullet's kill also sets off an obj_rocket_explosion where the bullet was.
    /// No push-apart, rocket-explosion or shield-wall events.
    /// </summary>
    public class Mimic : SetPieceEnemy
    {
        public const float RevealRangePx = 275f;
        public const float FadePerStep = 0.02f;
        public const float SizePx = 200f;
        public const int Points = 10;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Medium;
        /// <summary>illusion_asset: 1-5 are spr_rock1-5, 6 is spr_asteroid_experiment.</summary>
        public const int IllusionAssets = 6;

        /// <summary>GML spin, degrees/step.</summary>
        public float Spin { get; private set; }
        public float IllusionAngle { get; private set; }
        /// <summary>GML illusion_alpha: 1 is fully disguised.</summary>
        public float IllusionAlpha { get; private set; }
        /// <summary>Which rock it looks like (see <see cref="IllusionAssets"/>).</summary>
        public int IllusionAsset { get; private set; }
        public MimicIllusion Illusion { get; private set; }
        /// <summary>GML illusion_alpha == 1, which GML compares within its 0.00001 epsilon.</summary>
        public bool IsDisguised => Mathf.Abs(IllusionAlpha - 1f) <= 1e-5f;

        protected override Vector2 BBoxHalfPx => new Vector2(53f, 53f);

        /// <summary>GML Create, which also creates the illusion. Optional values pin the random ones.</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null,
            float? speedPxPerStep = null, float? spin = null)
        {
            Setup(position, room, player);
            UseCircle(53.5f);
            ImageAlpha = 0f;
            float speed = speedPxPerStep ?? IRandom(-15, 15) * 0.1f;
            SpeedPxPerStep = Dagger.ANearB(speed, 0f, 0.03f) ? 0f : speed;
            Direction = 0f;
            ImageAngle = IRandom(0, 359);
            IllusionAngle = IRandom(0, 359);
            IllusionAlpha = 1f;
            IllusionAsset = IRandom(1, IllusionAssets);
            float s = spin ?? IRandom(-15, 15) * 0.01f;
            Spin = Dagger.ANearB(s, 0f, 0.03f) ? 0f : s;
            Illusion = new GameObject("MimicIllusion").AddComponent<MimicIllusion>();
            Illusion.Init(this);
        }

        protected override void Alarms(float steps) { }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            IllusionAngle = Mathf.Repeat(IllusionAngle + Spin * steps, 360f);
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

        protected override bool RocketHit(Rocket rocket)
        {
            if (IllusionAlpha >= 1f) return false;
            DeathBurst(DeathEffectSize);
            Die(EnemyDeathCause.Rocket, Points, true, true);
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            if (IllusionAlpha >= 1f || IsEnemyRound(bullet)) return false;
            Vector2 at = PositionOf(bullet, Position);
            TinyExplosion.Spawn(at);
            new GameObject("RocketExplosion").AddComponent<RocketExplosion>().Init(at);
            DeathBurst(DeathEffectSize);
            Die(EnemyDeathCause.Bullet, Points, IsPlayerRound(bullet), true);
            return true;
        }
    }
}
