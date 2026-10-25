using System;
using Unity.Hare.Content;

namespace Unity.Hare.Player
{
    /// <summary>
    /// obj_player's flight state machine (gmlHARE Create/Step/Alarm events): continuous forward flight along
    /// <see cref="Direction"/> at <see cref="Speed"/>, A/D rotation, boost and brake gated by an energy resource,
    /// leap-from-idle within a facing tolerance and the Idle-only shield (player_shield) that drains the same
    /// energy. It holds numbers only: the view moves the ship's body from <see cref="Direction"/> and
    /// <see cref="Speed"/> and draws the shield wall on the events.
    /// </summary>
    public sealed class ShipFlight
    {
        readonly EventSink sink;
        FlightTuning tuning = GameContent.Current.Flight;
        float turnInput;

        internal ShipFlight(EventSink sink)
        {
            this.sink = sink;
            Reset(tuning);
        }

        public FlightTuning Tuning => tuning;
        public PlayerFlightState State { get; private set; }
        public bool IsBoosting { get; private set; }
        public bool IsBraking { get; private set; }
        public float Energy { get; private set; }
        public float Direction { get; private set; }
        public float Speed { get; private set; }

        /// <summary>GML: shielding. While true weapons hold fire and incoming hits are blocked.</summary>
        public bool IsShielding { get; private set; }

        /// <summary>GML: mouse_angle, degrees (0 = +x, counter-clockwise positive). The shield wall faces this way.</summary>
        public float AimDirection { get; private set; } = 270f;

        /// <summary>
        /// World units per second the ship moves: <see cref="Speed"/> along <see cref="Direction"/> while flying
        /// (GML direction/speed, so 0 degrees is +x and counter-clockwise positive), nothing while perched or dead.
        /// </summary>
        public Vec2 Velocity
        {
            get
            {
                if (State != PlayerFlightState.Moving) return Vec2.Zero;
                double radians = Direction * Math.PI / 180.0;
                return new Vec2((float)Math.Cos(radians) * Speed, (float)Math.Sin(radians) * Speed);
            }
        }

        float MinEnergyToStart => tuning.MaxEnergy * tuning.MinEnergyFractionToStart;

        /// <summary>obj_player Create / reset_player: back in flight at full energy, no shield.</summary>
        public void Reset(FlightTuning newTuning)
        {
            tuning = newTuning ?? GameContent.Current.Flight;
            DropShield(); // GML reset_player: shielding = false; with(obj_shieldwall) instance_destroy()
            State = PlayerFlightState.Moving;
            IsBoosting = false;
            IsBraking = false;
            Direction = 270f;
            Speed = tuning.FlightSpeed;
            Energy = tuning.MaxEnergy;
        }

        public void SetAimDirection(float degrees) => AimDirection = Maths.Repeat(degrees, 360f);

        /// <summary>-1 (clockwise, D) .. +1 (counterclockwise, A), matching GML's direction sign.</summary>
        public void SetTurnInput(float value) => turnInput = Maths.Clamp(value, -1f, 1f);

        /// <summary>GML: player_boost. Instant speed jump, held while energy lasts.</summary>
        public bool TryBoost()
        {
            if (State != PlayerFlightState.Moving || IsBraking) return false;
            if (Energy < MinEnergyToStart) return false;

            IsBoosting = true;
            Speed = tuning.FlightSpeed * 2f;
            return true;
        }

        /// <summary>GML: player_brake. Speed eases down to brake_speed over the following steps.</summary>
        public bool TryBrake()
        {
            if (State != PlayerFlightState.Moving || IsBoosting) return false;
            if (Energy < MinEnergyToStart) return false;

            IsBraking = true;
            return true;
        }

        /// <summary>GML: player_leap. Only launches from Idle, and only within jump_arc of the current facing.</summary>
        public bool TryLeap(float aimDirection)
        {
            if (State != PlayerFlightState.Idle) return false;
            if (Math.Abs(Maths.DeltaAngle(Direction, aimDirection)) > tuning.JumpArcDegrees) return false;

            DropShield(); // GML player_leap: shielding = false; with(obj_shieldwall) instance_destroy()
            Direction = aimDirection;
            Speed = tuning.FlightSpeed;
            IsBoosting = false;
            IsBraking = false;
            State = PlayerFlightState.Moving;
            sink.Emit(new ShipLeapt());
            return true;
        }

        /// <summary>Perching on a rock or structure (par_rock/par_structure).</summary>
        public bool Land()
        {
            if (State == PlayerFlightState.Dead) return false;
            State = PlayerFlightState.Idle;
            IsBoosting = false;
            IsBraking = false;
            Speed = 0f;
            sink.Emit(new ShipLanded());
            return true;
        }

        /// <summary>GML: player_shield. Only while perched (Idle) and with at least 33% energy.</summary>
        public bool TryShield()
        {
            if (State != PlayerFlightState.Idle || IsShielding) return false;
            if (Energy < MinEnergyToStart) return false; // GML: sfx_disabled

            IsShielding = true;
            sink.Emit(new ShieldRaised { Radius = tuning.ShieldRadius });
            return true;
        }

        /// <summary>GML: shield key release, leap, reset_player, or energy hitting zero.</summary>
        public void DropShield()
        {
            if (!IsShielding) return;
            IsShielding = false;
            sink.Emit(new ShieldDropped());
        }

        /// <summary>GML: player_hurt's "HP reached 0" branch. Stops the ship dead and drops every flight mode.</summary>
        public void Die()
        {
            State = PlayerFlightState.Dead;
            IsBoosting = false;
            IsBraking = false;
            DropShield();
            Speed = 0f;
            turnInput = 0f;
            sink.Emit(new ShipHalted());
        }

        /// <summary>GML: reset_player's flight half. Energy is left alone and refills on its own.</summary>
        public void Respawn(float direction)
        {
            State = PlayerFlightState.Moving;
            IsBoosting = false;
            IsBraking = false;
            DropShield(); // reset_player: with(obj_shieldwall) instance_destroy()
            Direction = direction;
            Speed = tuning.FlightSpeed;
        }

        /// <summary>One physics step of <paramref name="dt"/> seconds.</summary>
        public void Step(float dt)
        {
            TickEnergy(dt);

            // GML obj_player Step "Shielding": the wall collapses once the energy bar is empty.
            if (IsShielding && Energy <= 0f) DropShield();

            if (State != PlayerFlightState.Moving) return;

            float turnSpeed = IsBraking ? tuning.BrakeTurnSpeedDegreesPerSecond : tuning.TurnSpeedDegreesPerSecond;
            Direction += turnInput * turnSpeed * dt;

            if (IsBoosting && Energy <= 0f)
            {
                Speed = Maths.MoveTowards(Speed, tuning.FlightSpeed, tuning.SpeedChangeRatePerSecond * dt);
                if (Math.Abs(Speed - tuning.FlightSpeed) < 1e-6f) IsBoosting = false;
            }
            else if (IsBraking)
            {
                Speed = Maths.MoveTowards(Speed, tuning.BrakeSpeed, tuning.SpeedChangeRatePerSecond * dt);
                if (Energy <= 0f)
                {
                    IsBraking = false;
                    Speed = tuning.FlightSpeed;
                }
            }
        }

        void TickEnergy(float dt)
        {
            if (IsBoosting || IsBraking || IsShielding)
                Energy = Math.Max(0f, Energy - tuning.EnergyDrainPerSecond * dt);
            else
                Energy = Math.Min(tuning.MaxEnergy, Energy + tuning.EnergyFillPerSecond * dt);
        }
    }
}
