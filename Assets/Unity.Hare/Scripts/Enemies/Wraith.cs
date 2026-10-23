using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>obj_wraith's GML state string.</summary>
    public enum WraithState
    {
        /// <summary>"float": drifting toward the ship at 2 px/step, facing it.</summary>
        Float,
        /// <summary>"charge": a dash through the ship's position to 600 px beyond it.</summary>
        Charge,
        /// <summary>"stunned": frozen and opaque after a hit, the only state rockets can kill.</summary>
        Stunned
    }

    /// <summary>
    /// Ports obj_wraith (gmlHARE, par_enemy): a half-transparent ghost whose alpha breathes between
    /// 0.3 and 0.6 (0.002 a step) and that leaves an obj_wraith_blur afterimage every 10 steps. It
    /// floats toward the ship at 2 px/step until within 300 px, then charges: it aims at the ship,
    /// picks the point 600 px beyond it, and flies there from 2 px/step gaining 0.015 a step up to 5.
    /// Once within 32 px of that point it reappears 350-400 px from the ship in a random direction
    /// and floats again.
    ///
    /// Only the ship's bullets touch it: one stuns it (opaque, frozen, its blurs scattering at
    /// 2 px/step) for 4-7 seconds, then it floats again at alpha 0.3-0.6. A rocket kills it only
    /// while stunned (50 points, a pickup); otherwise rockets fly through. Rocket explosions and the
    /// shield wall do nothing (their events are commented out).
    /// </summary>
    public class Wraith : SetPieceEnemy
    {
        public const float FloatSpeedPxPerStep = 2f;
        public const float ChargeRangePx = 300f;
        public const float ChargeOvershootPx = 600f;
        public const float ChargeStartSpeedPxPerStep = 2f;
        public const float ChargeAccelerationPxPerStep = 0.015f;
        public const float ChargeMaxSpeedPxPerStep = 5f;
        public const float ArrivalRangePx = 32f;
        public const int MinReappearPx = 350;
        public const int MaxReappearPx = 400;
        public const float BlurEverySteps = 10f;
        public const int MinStunSteps = 4 * 120;
        public const int MaxStunSteps = 7 * 120;
        public const float PulsePerStep = 0.002f;
        public const float PulseMinAlpha = 0.3f;
        public const float PulseMaxAlpha = 0.6f;
        public const float BlurAlpha = 0.3f;
        public const float BlurFadePerStep = 0.005f;
        public const float StunnedBlurSpeedPxPerStep = 2f;
        public const int Points = 50;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Small;

        float blurAlarm;
        float recoverAlarm = Off;
        bool firstPass;
        bool fadeIn;

        public WraithState State { get; private set; }
        /// <summary>GML facing / image_xscale: 1 faces right, -1 left.</summary>
        public int Facing { get; private set; }
        /// <summary>GML destX/destY: where the current charge ends.</summary>
        public Vector2 ChargeTarget { get; private set; }
        /// <summary>Steps left stunned, or negative when not stunned.</summary>
        public float StunStepsLeft => recoverAlarm;

        /// <summary>Reappeared beside the ship at the end of a charge.</summary>
        public event Action<Wraith> Reappeared;

        protected override Vector2 BBoxHalfPx => new Vector2(50f, 56f);

        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null)
        {
            Setup(position, room, player);
            UseEllipse(new Vector2(100f, 112f));
            ImageAlpha = 0.5f;
            SpeedPxPerStep = 0f;
            State = WraithState.Float;
            firstPass = false;
            fadeIn = false;
            Facing = 1;
            blurAlarm = BlurEverySteps;
            recoverAlarm = Off;
        }

        protected override void Alarms(float steps)
        {
            // Alarm 1: the stun wears off.
            if (Ring(ref recoverAlarm, steps))
            {
                State = WraithState.Float;
                ImageAlpha = 0.01f * IRandom(30, 60);
            }

            // Alarm 0: afterimage.
            if (Ring(ref blurAlarm, steps))
            {
                if (State == WraithState.Stunned)
                {
                    int dir = IRandom(0, 359);
                    FadingDebris.Spawn(DebrisKind.WraithBlur, Position, 0, dir, StunnedBlurSpeedPxPerStep, 0f, 0f,
                        BlurAlpha, BlurFadePerStep, dir > 90 && dir < 270 ? -1 : 1);
                }
                else FadingDebris.Spawn(DebrisKind.WraithBlur, Position, 0, 0f, 0f, 0f, 0f, BlurAlpha, BlurFadePerStep, Facing);
                blurAlarm = BlurEverySteps;
            }
        }

        protected override void StepEvent(float steps)
        {
            if (State != WraithState.Stunned)
            {
                if (fadeIn)
                {
                    if (ImageAlpha < PulseMaxAlpha) ImageAlpha += PulsePerStep * steps;
                    else fadeIn = false;
                }
                else
                {
                    if (ImageAlpha > PulseMinAlpha) ImageAlpha -= PulsePerStep * steps;
                    else fadeIn = true;
                }
            }

            switch (State)
            {
                case WraithState.Float:
                    firstPass = false;
                    if (!HasPlayer) { SpeedPxPerStep = 0f; break; }
                    Facing = Position.x < PlayerPosition.x ? 1 : -1;
                    if (DistanceToPlayerPx() > ChargeRangePx) MoveTowardsPoint(PlayerPosition, FloatSpeedPxPerStep);
                    else
                    {
                        State = WraithState.Charge;
                        firstPass = true;
                    }
                    break;

                case WraithState.Charge:
                    if (firstPass)
                    {
                        firstPass = false;
                        if (HasPlayer)
                        {
                            float chargeAngle = PointDirection(Position, PlayerPosition);
                            ChargeTarget = PlayerPosition + Gml.Dir(chargeAngle) * Gml.Px(ChargeOvershootPx);
                        }
                        else ChargeTarget = Position;
                        Facing = Position.x < ChargeTarget.x ? 1 : -1;
                        SpeedPxPerStep = ChargeStartSpeedPxPerStep;
                    }

                    if (DistanceToPointPx(ChargeTarget) > ArrivalRangePx)
                    {
                        float speed = SpeedPxPerStep;
                        if (speed < ChargeMaxSpeedPxPerStep) speed += ChargeAccelerationPxPerStep * steps;
                        MoveTowardsPoint(ChargeTarget, speed);
                    }
                    else
                    {
                        Vector2 anchor = HasPlayer ? PlayerPosition : Position;
                        Teleport(anchor + Gml.Dir(IRandom(0, 359)) * Gml.Px(IRandom(MinReappearPx, MaxReappearPx)));
                        State = WraithState.Float;
                        firstPass = true;
                        Reappeared?.Invoke(this);
                    }
                    break;

                case WraithState.Stunned:
                    if (firstPass)
                    {
                        firstPass = false;
                        SpeedPxPerStep = 0f;
                    }
                    break;
            }
        }

        protected override bool RocketHit(Rocket rocket)
        {
            if (State != WraithState.Stunned) return false;
            DeathBurst(DeathEffectSize);
            Die(EnemyDeathCause.Rocket, Points, true, true);
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            if (!IsPlayerRound(bullet)) return false;
            TinyExplosionAt(bullet);
            State = WraithState.Stunned;
            firstPass = true;
            ImageAlpha = 1f;
            recoverAlarm = IRandom(MinStunSteps, MaxStunSteps);
            return true;
        }
    }
}
