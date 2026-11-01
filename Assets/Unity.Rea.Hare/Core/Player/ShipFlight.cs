using System;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Player
{
    /// <summary>obj_energybar's <c>mode</c>: what its Alarm 0 does to the bar every <c>rate</c> steps.</summary>
    public enum EnergyMode
    {
        /// <summary>Create's "idle", and where "fill" ends at maxValue: the bar is left alone.</summary>
        Idle,
        /// <summary>"fill": +1 a tick until maxValue.</summary>
        Fill,
        /// <summary>"drain": -2 a tick, held at 0. Boost, brake and the shield set it; nothing else stops it but a "fill".</summary>
        Drain,
    }

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
        float energyTimer;

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

        /// <summary>
        /// obj_energybar's mode (U.HARE-T89). The bar does not refill because a drain stopped: only landing, a boost or
        /// brake running dry, the boost/brake keys' release while perched and reset_player set "fill".
        /// </summary>
        public EnergyMode EnergyMode { get; private set; }

        /// <summary>GML direction: the heading the ship flies along.</summary>
        public float Direction { get; private set; }

        /// <summary>
        /// GML speed, in the original's px a step (flight_speed 5, brake_speed 2.5, boost doubling it), so the rock's
        /// take-off bonus adds to it and the speedometer reads it as obj_player's speed. <see cref="WorldSpeed"/> is the
        /// same speed in world units a second, which is what moves the ship (U.HARE-T88).
        /// </summary>
        public float Speed { get; private set; }

        /// <summary><see cref="Speed"/> as world units a second: <c>GameClock.Px(speed) * GameClock.StepsPerSecond</c>.</summary>
        public float WorldSpeed => GameClock.Px(Speed) * GameClock.StepsPerSecond;

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
        /// World units per second the ship moves: <see cref="WorldSpeed"/> along <see cref="Direction"/> while flying
        /// (GML direction/speed, so 0 degrees is +x and counter-clockwise positive), nothing while perched or dead.
        /// </summary>
        public Vec2 Velocity
        {
            get
            {
                if (State != PlayerFlightState.Moving && State != PlayerFlightState.Leaving) return Vec2.Zero;
                double radians = Direction * Math.PI / 180.0;
                float speed = WorldSpeed;
                return new Vec2((float)Math.Cos(radians) * speed, (float)Math.Sin(radians) * speed);
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
            EnergyMode = EnergyMode.Idle; // obj_energybar Create: currentValue = 100; mode = "idle"; alarm[0] = rate
            energyTimer = EnergyTickSeconds;
            ClearSpinAndPerch();
        }

        float EnergyTickSeconds => GameClock.Seconds(tuning.EnergyTickSteps);

        /// <summary>
        /// <c>obj_energybar.mode = "fill"</c>: what landing, a boost or brake running dry, the boost/brake keys' release while
        /// perched and reset_player (when the bar is not full) do.
        /// </summary>
        public void StartEnergyFill() => EnergyMode = EnergyMode.Fill;

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
        public void SetTurnInput(float value)
        {
            turnInput = Maths.Clamp(value, -1f, 1f);
            if (turnInput == 0f) IsTurning = false; // the A/D key-release events: turning = false
        }

        /// <summary>
        /// GML turning: set by rotate_clockwise/rotate_counterclockwise the first step a turn key turns the flying ship,
        /// cleared when the key comes up or the ship dies (player_hurt). It is not cleared by landing, so the spark trail
        /// (<see cref="ShipLook"/>) keeps sparking on a perch while the key stays down, as in the original.
        /// </summary>
        public bool IsTurning { get; private set; }

        /// <summary>
        /// GML: player_boost. <c>player.speed = player.speed * 2</c>: the ship's current speed doubles (a take-off's rock
        /// bonus included), held while energy lasts.
        /// </summary>
        public bool TryBoost()
        {
            if (State != PlayerFlightState.Moving || IsBraking) return false;
            if (Energy < MinEnergyToStart) return false;

            IsBoosting = true;
            Speed *= 2f;
            EnergyMode = EnergyMode.Drain;
            sink.Emit(new BoostStarted());
            return true;
        }

        /// <summary>GML: player_brake. Speed eases down to brake_speed over the following steps.</summary>
        public bool TryBrake()
        {
            if (State != PlayerFlightState.Moving || IsBoosting) return false;
            if (Energy < MinEnergyToStart) return false;

            IsBraking = true;
            EnergyMode = EnergyMode.Drain;
            sink.Emit(new BrakeStarted());
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
            EndShieldDrain();
            float perchedFacing = Facing;
            Direction = aimDirection;
            Facing = aimDirection;
            Speed = TakeOffSpeed;
            IsBoosting = false;
            IsBraking = false;
            IsCrouching = false;
            State = PlayerFlightState.Moving;
            CurrentRock = null;
            CurrentStructure = null;
            sink.Emit(new ShipLeapt { PerchedFacing = perchedFacing });
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
            if (State == PlayerFlightState.Dead || State == PlayerFlightState.Leaving) return false;
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
            EnergyMode = EnergyMode.Fill; // obj_player's par_rock / par_structure collision: obj_energybar.mode = "fill"
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
            if (State == PlayerFlightState.Dead || State == PlayerFlightState.Leaving) return false;

            Direction = Facing;
            State = PlayerFlightState.Moving;
            Speed = TakeOffSpeed;
            IsCrouching = false;
            Spin = tuning.SpinDegreesPerSecond;
            endFlashTimer = endFlashSeconds;
            DropShield();
            EndShieldDrain();
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
            EnergyMode = EnergyMode.Drain;
            sink.Emit(new ShieldRaised { Radius = tuning.ShieldRadius });
            return true;
        }

        /// <summary>
        /// GML: shield key release, leap, reset_player, or energy hitting zero. <paramref name="passed"/> for the two that
        /// play sfx_hologram_pass (<see cref="ShieldDropped.Passed"/>).
        /// </summary>
        public void DropShield(bool passed = false)
        {
            if (!IsShielding) return;
            IsShielding = false;
            sink.Emit(new ShieldDropped { Passed = passed });
        }

        /// <summary>
        /// player_leap and fling_player take the shield down (<c>shielding = false; with(obj_shieldwall) instance_destroy()</c>)
        /// but never touch obj_energybar, so in the original the shield's "drain" outlived it and emptied the bar in flight
        /// until the next landing. Fixed (docs/BIBLE.md D25): a drain nothing is paying for any more becomes a "fill", as the
        /// shield key's own release does. A boost or brake still running keeps its drain.
        /// </summary>
        void EndShieldDrain()
        {
            if (EnergyMode == EnergyMode.Drain && !IsBoosting && !IsBraking) EnergyMode = EnergyMode.Fill;
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
            IsTurning = false; // player_hurt: turning = false
            IsCrouching = false;
            CurrentRock = null; // the dead ship no longer rides it; reset_player clears it in the original
            sink.Emit(new ShipHalted());
        }

        /// <summary>
        /// obj_surface_reflection's Step, the water line as a floor (U.HARE-T58): <c>player.direction = (0 or 180);
        /// player.image_angle = player.direction</c>. Nothing else about the ship changes (its speed, spin and state are
        /// left as they were, as the original leaves them).
        /// </summary>
        public void SkimSurface(float direction)
        {
            Direction = direction;
            Facing = direction;
        }

        /// <summary>GML exit_angle (Create's 90): the way the leaving ship turns toward, degrees.</summary>
        public float ExitAngle { get; private set; } = 90f;

        /// <summary>
        /// obj_level_results' Enter (U.HARE-T22): <c>player.state = state_leave</c>, whatever the ship was doing (perched,
        /// flying, spinning). The leave branch of its Step then runs instead of everything else: no shield, perch, boost,
        /// brake or spin applies any more. A dead ship is sent off too, as the original sets state_leave whatever the state was;
        /// its death wait ends with it (docs/BIBLE.md D21). Refused when already leaving.
        /// </summary>
        public bool Leave()
        {
            if (State == PlayerFlightState.Leaving) return false;
            DropShield();
            State = PlayerFlightState.Leaving;
            IsBoosting = false;
            IsBraking = false;
            IsCrouching = false;
            Spin = 0f;
            WasSpinning = false;
            endFlashTimer = 0f;
            CurrentRock = null;
            CurrentStructure = null;
            ExitAngle = 90f; // until the room's first look at exit_x
            Speed = tuning.FlightSpeed * 2f;
            sink.Emit(new ShipLeaving { Direction = Direction });
            return true;
        }

        /// <summary>The leave's <c>exit_angle = point_direction(x, y, exit_x, -sprite_height)</c>, set every step. Refused unless leaving.</summary>
        public bool AimExit(float degrees)
        {
            if (State != PlayerFlightState.Leaving) return false;
            ExitAngle = Maths.Repeat(degrees, 360f);
            return true;
        }

        /// <summary>
        /// GML check_rotate_clockwise(target, angle): the signed turn from <paramref name="angle"/> to
        /// <paramref name="target"/> the short way round (exactly half way round counts as counter-clockwise when the target
        /// is the larger angle, clockwise otherwise), and whether it is negative.
        /// </summary>
        public static bool RotatesClockwise(float target, float angle)
        {
            target = Maths.Repeat(target, 360f);
            angle = Maths.Repeat(angle, 360f);
            float turn;
            if (target > angle) turn = target > angle + 180f ? -(360f - target + angle) : target - angle;
            else turn = angle > target + 180f ? 360f - angle + target : target - angle;
            return turn < 0f;
        }

        /// <summary>
        /// obj_player's Step while state_leave: <c>if(check_rotate_clockwise(exit_angle, direction)) direction -= 3 else
        /// direction += 3; image_angle = direction; speed = flight_speed * 2</c>. It always turns, so on its heading it
        /// wobbles a step either side, as the original does.
        /// </summary>
        void StepLeave(float dt)
        {
            float turned = tuning.LeaveTurnDegreesPerSecond * dt;
            Direction = Maths.Repeat(RotatesClockwise(ExitAngle, Direction) ? Direction - turned : Direction + turned, 360f);
            Facing = Direction;
            Speed = tuning.FlightSpeed * 2f;
        }

        /// <summary>
        /// GML: reset_player's flight half. The bar keeps its value; <c>if(currentValue != maxValue) mode = "fill"</c>.
        /// </summary>
        public void Respawn(float direction)
        {
            State = PlayerFlightState.Moving;
            IsBoosting = false;
            IsBraking = false;
            DropShield(); // reset_player: with(obj_shieldwall) instance_destroy()
            Direction = direction;
            Facing = direction;
            Speed = tuning.FlightSpeed;
            if (Energy != tuning.MaxEnergy) EnergyMode = EnergyMode.Fill;
            ClearSpinAndPerch();
        }

        /// <summary>One physics step of <paramref name="dt"/> seconds.</summary>
        public void Step(float dt)
        {
            TickEndFlash(dt);
            TickEnergy(dt);

            // GML obj_player Step "Shielding": the wall collapses once the energy bar is empty.
            if (IsShielding && Energy <= 0f) DropShield(passed: true); // ... audio_play_sound(sfx_hologram_pass, 1, 0)

            if (State == PlayerFlightState.Leaving)
            {
                StepLeave(dt);
                return;
            }
            if (State != PlayerFlightState.Moving) return;

            // GML Step: image_angle += spin.
            Facing += Spin * dt;

            float turnSpeed = IsBraking ? tuning.BrakeTurnSpeedDegreesPerSecond : tuning.TurnSpeedDegreesPerSecond;
            float turned = turnInput * turnSpeed * dt;
            if (turnInput != 0f) IsTurning = true;
            Direction += turned;
            Facing += turned; // rotate_clockwise / rotate_counterclockwise turn image_angle with direction

            if (IsBoosting && Energy <= 0f)
            {
                Speed = Maths.MoveTowards(Speed, tuning.FlightSpeed, tuning.SpeedChangeRatePerSecond * dt);
                if (Math.Abs(Speed - tuning.FlightSpeed) < 1e-6f)
                {
                    IsBoosting = false; // obj_player Step "Rocketing": rocketing = false; obj_energybar.mode = "fill"
                    EnergyMode = EnergyMode.Fill;
                }
            }
            else if (IsBraking)
            {
                Speed = Maths.MoveTowards(Speed, tuning.BrakeSpeed, tuning.SpeedChangeRatePerSecond * dt);
                if (Energy <= 0f)
                {
                    IsBraking = false; // obj_player Step "Braking": braking = false; obj_energybar.mode = "fill"
                    Speed = tuning.FlightSpeed;
                    EnergyMode = EnergyMode.Fill;
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

        /// <summary>
        /// obj_energybar's Alarm 0, re-armed every <c>rate</c> (4) steps whatever the mode: "fill" adds 1 until maxValue and
        /// then goes "idle", "drain" takes 2 (its Step holds the bar in 0..maxValue). The alarm runs before obj_player's Step,
        /// as here. Its Step skips the range check while the ship is dead, so the original's bar could sink below 0 under a
        /// dead ship's drain and show a negative percentage until reset_player; the first living Step put it back to 0, so
        /// this holds it at 0 throughout, which plays the same.
        /// </summary>
        void TickEnergy(float dt)
        {
            energyTimer -= dt;
            while (energyTimer <= 1e-4f) // a hair of slack so whole steps of 1/120 s ring on the step they should
            {
                energyTimer += EnergyTickSeconds;
                if (EnergyMode == EnergyMode.Fill)
                {
                    if (Energy < tuning.MaxEnergy) Energy = Math.Min(tuning.MaxEnergy, Energy + tuning.EnergyFillPerTick);
                    else EnergyMode = EnergyMode.Idle;
                }
                else if (EnergyMode == EnergyMode.Drain)
                {
                    Energy = Math.Max(0f, Energy - tuning.EnergyDrainPerTick);
                }
            }
        }
    }
}
