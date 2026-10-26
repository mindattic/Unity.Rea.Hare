using System;
using Unity.Hare.Content;

namespace Unity.Hare.Player
{
    /// <summary>
    /// obj_player's flight state machine (gmlHARE Create/Step/Alarm events): continuous forward flight along
    /// <see cref="Direction"/> at <see cref="Speed"/>, A/D rotation, boost and brake gated by an energy resource,
    /// perching on rocks and structures (the par_rock/par_structure collisions), leap-from-idle within jump_arc of the
    /// ship's facing, fling_player, the spin while airborne and the Idle-only shield (player_shield) that drains the same
    /// energy. It holds numbers only: the view moves the ship's body from <see cref="Direction"/> and <see cref="Speed"/>,
    /// turns it to <see cref="Facing"/>, carries it round its rock from <see cref="LandAngle"/> and
    /// <see cref="DistanceToCenter"/>, and draws the shield wall on the events.
    /// </summary>
    public sealed class ShipFlight
    {
        readonly EventSink sink;
        FlightTuning tuning = GameContent.Current.Flight;
        float turnInput;
        float facing;
        float endFlashTimer;

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

        /// <summary>GML direction: the heading the ship flies along.</summary>
        public float Direction { get; private set; }

        public float Speed { get; private set; }

        /// <summary>
        /// GML image_angle, degrees in [0, 360): the way the ship points. It turns with <see cref="Direction"/>, runs away
        /// from it while the ship spins, is the surface normal once perched and turns with its rock; the leap's jump_arc
        /// is measured from it and fling_player launches along it.
        /// </summary>
        public float Facing
        {
            get => facing;
            private set
            {
                float wrapped = Maths.Repeat(value, 360f);
                facing = wrapped >= 360f ? 0f : wrapped;
            }
        }

        /// <summary>GML: shielding. While true weapons hold fire and incoming hits are blocked.</summary>
        public bool IsShielding { get; private set; }

        /// <summary>GML: crouching. Set by Space or the right mouse button going down while perched; leap, W/S release, reset and death clear it.</summary>
        public bool IsCrouching { get; private set; }

        /// <summary>GML: mouse_angle, degrees (0 = +x, counter-clockwise positive). The shield wall faces this way.</summary>
        public float AimDirection { get; private set; } = 270f;

        /// <summary>
        /// GML spin, degrees a second (spin = 3 a step) the ship's <see cref="Facing"/> turns while it flies spinning:
        /// Alt or the middle mouse button held in flight, a hit in flight, and fling_player set it; letting go, the
        /// end_flash alarm, reset_player and landing put it back to 0. obj_player's Draw shows spr_direction_arrow while it
        /// is not 0.
        /// </summary>
        public float Spin { get; private set; }

        /// <summary>GML was_spinning: after a spin the facing winds round (3 degrees a step) until it is back on <see cref="Direction"/>.</summary>
        public bool WasSpinning { get; private set; }

        /// <summary>GML current_rock: the id of the rock the ship rides, or null (noone).</summary>
        public int? CurrentRock { get; private set; }

        /// <summary>GML current_structure: the id of the structure the ship last perched on, or null (noone).</summary>
        public int? CurrentStructure { get; private set; }

        /// <summary>GML land_angle: the direction from the ship to its rock's centre, turning with the rock's spin.</summary>
        public float LandAngle { get; private set; }

        /// <summary>GML distance_to_center: how far the ship sits from its rock's centre, world units.</summary>
        public float DistanceToCenter { get; private set; }

        /// <summary>The ship's rock's speed (px/step) and spin (degrees/step) as last seen, for the take-off bonus.</summary>
        public float RockSpeedPxPerStep { get; private set; }
        public float RockSpinPerStep { get; private set; }

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

        /// <summary>
        /// The speed player_leap and fling_player take off at: flight_speed plus abs(current_rock.speed) and
        /// abs(current_rock.spin) while the ship rides a rock. <see cref="FlightTuning.FlightSpeed"/> is GML's flight_speed
        /// number, so the rock's px/step and degrees/step add to it as they do in the original.
        /// </summary>
        float TakeOffSpeed =>
            tuning.FlightSpeed + (CurrentRock.HasValue ? Math.Abs(RockSpeedPxPerStep) + Math.Abs(RockSpinPerStep) : 0f);

        /// <summary>obj_player Create / reset_player: back in flight at full energy, no shield.</summary>
        public void Reset(FlightTuning newTuning)
        {
            tuning = newTuning ?? GameContent.Current.Flight;
            DropShield(); // GML reset_player: shielding = false; with(obj_shieldwall) instance_destroy()
            State = PlayerFlightState.Moving;
            IsBoosting = false;
            IsBraking = false;
            Direction = 270f;
            Facing = Direction;
            Speed = tuning.FlightSpeed;
            Energy = tuning.MaxEnergy;
            ClearSpinAndPerch();
        }

        void ClearSpinAndPerch()
        {
            Spin = 0f;
            WasSpinning = false;
            endFlashTimer = 0f;
            IsCrouching = false;
            CurrentRock = null;
            CurrentStructure = null;
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

        /// <summary>
        /// GML: player_leap. Only launches from Idle, and only within jump_arc of the ship's <see cref="Facing"/>: it flies
        /// off toward <paramref name="aimDirection"/> at the take-off speed (plus its rock's speed and spin), stands up,
        /// drops the shield and leaves its rock and structure behind.
        /// </summary>
        public bool TryLeap(float aimDirection)
        {
            if (!CanLeap(aimDirection)) return false;

            DropShield(); // GML player_leap: shielding = false; with(obj_shieldwall) instance_destroy()
            Direction = aimDirection;
            Facing = aimDirection;
            Speed = TakeOffSpeed;
            IsBoosting = false;
            IsBraking = false;
            IsCrouching = false;
            State = PlayerFlightState.Moving;
            CurrentRock = null;
            CurrentStructure = null;
            sink.Emit(new ShipLeapt());
            return true;
        }

        /// <summary>
        /// GML: can_jump, worked out every step while perched: the aim is within jump_arc of the ship's facing
        /// (<c>abs(mouse_angle - image_angle) &lt;= jump_arc || abs(mouse_angle - image_angle) &gt;= 360 - jump_arc</c>).
        /// The leap and obj_player's dotted leap line both need it; it is false in flight.
        /// </summary>
        public bool CanLeap(float aimDirection) =>
            State == PlayerFlightState.Idle && Math.Abs(Maths.DeltaAngle(Facing, aimDirection)) <= tuning.JumpArcDegrees;

        /// <summary>Perching where the ship is, facing as it does (no surface to ride: scripted landings and tests).</summary>
        public bool Land()
        {
            if (State == PlayerFlightState.Dead) return false;
            Perch();
            sink.Emit(new ShipLanded());
            return true;
        }

        /// <summary>
        /// GML obj_player's par_rock / par_structure collision: while flying (and not on that very rock or structure) the
        /// ship goes to state_idle, stops boosting, braking and spinning, faces the surface normal and, on a rock,
        /// remembers current_rock, land_angle and distance_to_center so it rides the rock from now on.
        /// </summary>
        public bool LandOn(LandingSurface surface)
        {
            if (State != PlayerFlightState.Moving) return false;
            if (surface.Kind == SurfaceKind.Rock && CurrentRock == surface.Id) return false;
            if (surface.Kind == SurfaceKind.Structure && CurrentStructure == surface.Id) return false;

            Perch();
            Facing = surface.Normal; // image_angle = normal (normal_detect's -1 normalises to 359, as GML's does)
            if (surface.Kind == SurfaceKind.Rock)
            {
                CurrentRock = surface.Id;
                LandAngle = Maths.Repeat(surface.LandAngle, 360f);
                DistanceToCenter = surface.DistanceToCenter;
                RockSpeedPxPerStep = surface.SpeedPxPerStep;
                RockSpinPerStep = surface.SpinPerStep;
            }
            else
            {
                CurrentStructure = surface.Id;
            }
            sink.Emit(new ShipLanded { Surface = surface.Kind, SurfaceId = surface.Id, Facing = Facing });
            return true;
        }

        void Perch()
        {
            State = PlayerFlightState.Idle;
            IsBoosting = false;
            IsBraking = false;
            Speed = 0f;
            Spin = 0f;
            WasSpinning = false;
        }

        /// <summary>
        /// obj_player's Step while it has a current_rock: <c>land_angle += current_rock.spin</c> and
        /// <c>image_angle += current_rock.spin</c> (the view then puts the ship at the rock's centre minus
        /// lengthdir(distance_to_center, land_angle)). It also keeps the rock's speed and spin for the take-off bonus.
        /// </summary>
        public bool RideRock(float spinPerStep, float speedPxPerStep, float dt)
        {
            if (State != PlayerFlightState.Idle || !CurrentRock.HasValue) return false;
            float turned = spinPerStep * dt * GameClock.StepsPerSecond;
            LandAngle = Maths.Repeat(LandAngle + turned, 360f);
            Facing += turned;
            RockSpinPerStep = spinPerStep;
            RockSpeedPxPerStep = speedPxPerStep;
            return true;
        }

        /// <summary>
        /// GML fling_player: the ship is thrown off along its <see cref="Facing"/> (direction = image_angle) at the take-off
        /// speed, spinning (spin = 3) until the end_flash alarm, <paramref name="endFlashSeconds"/> from now, stops it; the
        /// shield drops and current_rock is let go, as every caller does. Refused while dead (fixed: the original could fling
        /// a dead ship whose rock broke back into flight).
        /// </summary>
        public bool Fling(float endFlashSeconds)
        {
            if (State == PlayerFlightState.Dead) return false;

            Direction = Facing;
            State = PlayerFlightState.Moving;
            Speed = TakeOffSpeed;
            IsCrouching = false;
            Spin = tuning.SpinDegreesPerSecond;
            endFlashTimer = endFlashSeconds;
            DropShield();
            CurrentRock = null;
            sink.Emit(new ShipFlung { Direction = Direction, Speed = Speed });
            return true;
        }

        /// <summary>The ship's rock left play without rock_destroyed (setup_level's clear): current_rock = noone, nothing else.</summary>
        public bool LetGoOfRock()
        {
            if (!CurrentRock.HasValue) return false;
            CurrentRock = null;
            return true;
        }

        /// <summary>GML Space/right mouse press while state_idle: crouching = true.</summary>
        public bool Crouch()
        {
            if (State != PlayerFlightState.Idle) return false;
            IsCrouching = true;
            return true;
        }

        /// <summary>GML W/S release while state_idle: crouching = false.</summary>
        public void StandUp() => IsCrouching = false;

        /// <summary>GML Alt / middle mouse press: <c>if(state == state_moving) spin = 3</c>.</summary>
        public bool StartSpin()
        {
            if (State != PlayerFlightState.Moving) return false;
            Spin = tuning.SpinDegreesPerSecond;
            return true;
        }

        /// <summary>GML Alt / middle mouse release: <c>if(state == state_moving) { spin = 0; was_spinning = true; }</c>.</summary>
        public bool StopSpin()
        {
            if (State != PlayerFlightState.Moving) return false;
            Spin = 0f;
            WasSpinning = true;
            return true;
        }

        /// <summary>
        /// A hit that landed (the hit collisions' <c>alarm[alarm_end_flash] = flash_time</c>, plus <c>spin = 3</c> while
        /// flying when <paramref name="spin"/>). Returns whether it set the ship spinning.
        /// </summary>
        internal bool TakeHit(bool spin, float endFlashSeconds)
        {
            endFlashTimer = endFlashSeconds;
            if (!spin || State != PlayerFlightState.Moving) return false;
            Spin = tuning.SpinDegreesPerSecond;
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
            IsCrouching = false;
            CurrentRock = null; // the dead ship no longer rides it; reset_player clears it in the original
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
            Facing = direction;
            Speed = tuning.FlightSpeed;
            ClearSpinAndPerch();
        }

        /// <summary>One physics step of <paramref name="dt"/> seconds.</summary>
        public void Step(float dt)
        {
            TickEndFlash(dt);
            TickEnergy(dt);

            // GML obj_player Step "Shielding": the wall collapses once the energy bar is empty.
            if (IsShielding && Energy <= 0f) DropShield();

            if (State != PlayerFlightState.Moving) return;

            // GML Step: image_angle += spin.
            Facing += Spin * dt;

            float turnSpeed = IsBraking ? tuning.BrakeTurnSpeedDegreesPerSecond : tuning.TurnSpeedDegreesPerSecond;
            float turned = turnInput * turnSpeed * dt;
            Direction += turned;
            Facing += turned; // rotate_clockwise / rotate_counterclockwise turn image_angle with direction

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

            if (WasSpinning) WindBackToDirection(dt);
        }

        /// <summary>
        /// GML Alarm 1 (alarm_end_flash): <c>spin = 0; if(state == state_moving) was_spinning = true</c>. The invulnerability
        /// half of that alarm is <see cref="ShipHealth"/>'s.
        /// </summary>
        void TickEndFlash(float dt)
        {
            if (endFlashTimer <= 0f) return;
            endFlashTimer -= dt;
            if (endFlashTimer > 1e-4f) return; // a hair of slack so whole steps of 1/120 s ring on the step they should
            endFlashTimer = 0f;
            Spin = 0f;
            if (State == PlayerFlightState.Moving) WasSpinning = true;
        }

        /// <summary>
        /// GML Step's was_spinning block: <c>if(!a_near_b(image_angle, direction, 3)) image_angle += 3; else { image_angle =
        /// direction; was_spinning = false; }</c>, always winding counter-clockwise.
        /// </summary>
        void WindBackToDirection(float dt)
        {
            float remaining = Maths.Repeat(Direction - Facing, 360f);
            float snap = tuning.SpinSnapDegrees;
            if (remaining <= snap || remaining >= 360f - snap)
            {
                Facing = Direction;
                WasSpinning = false;
                return;
            }
            Facing += Math.Min(tuning.SpinRecoverDegreesPerSecond * dt, remaining);
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
