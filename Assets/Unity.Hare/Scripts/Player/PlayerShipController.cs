using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Content;
using Unity.Hare.Input;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// The view on obj_player's flight state machine (gmlHARE, Create/Step/Alarm events). The rules (continuous
    /// forward flight, boost and brake gated by energy, perching, leap-from-idle within a facing tolerance, fling_player,
    /// the spin while airborne, the Idle-only shield) live in Core's <see cref="ShipFlight"/>; this component turns Input
    /// System actions into commands, lands the ship on the rocks and structures its mask touches (U.HARE-T53, the
    /// physics half in <see cref="PerchProbe"/>), carries it round its rock, moves the body from the flight state each
    /// physics step and draws the shield wall on the events.
    /// GML's per-step deltas (room_gamescreen runs at 120 steps/sec) are per-second rates in <see cref="FlightTuning"/>.
    /// Input comes from <see cref="Controls"/> (U.HARE-T42: the generated <see cref="HareControls"/> wrapper over
    /// Assets/Unity.Hare/Scripts/Input/HareControls.inputactions), not raw Keyboard.current/Mouse.current polling
    /// (the one exception is the <see cref="Mouse"/>.current null check in <see cref="UpdateAim"/>, which only
    /// gates whether a pointer is present to read screen-space aim from, not a value read); this ship's sibling
    /// weapon views (<see cref="Weapons.AirWeaponLoadout"/>, <see cref="Weapons.GroundWeaponLoadout"/>,
    /// <see cref="Weapons.PerchedWeapon"/>, <see cref="Weapons.VulcanWeapon"/>) share this same instance.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerShipController : SeatView
    {
        /// <summary>
        /// The loaded content's flight tuning (AGENTS.md rule 4); no longer an Inspector field. Read live, not
        /// cached at construction, so a component built before <see cref="ContentBootstrap"/> runs (e.g. Editor
        /// scene load, or Enter Play Mode without domain reload) still picks up the loaded file once it does.
        /// </summary>
        static FlightTuning Tuning => GameContent.Current.Flight;

        Rigidbody2D body;
        HareControls controls;
        bool flewLastStep;

        /// <summary>
        /// The shared input wrapper for this ship (lazy: components on this GameObject run their Awake in an
        /// undefined order, and EditMode tests call <see cref="ResetState"/> directly without Awake at all).
        /// </summary>
        public HareControls Controls
        {
            get
            {
                if (controls == null)
                {
                    controls = new HareControls();
                    controls.Player.Enable();
                    controls.Helper.Enable(); // the arrows, Page Down and Delete: obj_helper's keys (U.HARE-T66)
                }
                return controls;
            }
        }

        public PlayerFlightState State => Core.Flight.State;
        public bool IsBoosting => Core.Flight.IsBoosting;
        public bool IsBraking => Core.Flight.IsBraking;
        public float Energy => Core.Flight.Energy;
        public float Direction => Core.Flight.Direction;
        public float Speed => Core.Flight.Speed;

        /// <summary>GML: shielding. While true, weapons hold fire (fire_primary/fire_secondary) and incoming hits are blocked.</summary>
        public bool IsShielding => Core.Flight.IsShielding;

        /// <summary>The live obj_shieldwall instance while <see cref="IsShielding"/>, else null.</summary>
        public ShieldWall Shield { get; private set; }

        /// <summary>GML: mouse_angle, degrees in GML's math convention. The shield wall faces this way.</summary>
        public float AimDirection => Core.Flight.AimDirection;

        /// <summary>GML: spin, degrees a second the ship's facing turns while it flies spinning (Alt, a hit, fling_player).</summary>
        public float Spin => Core.Flight.Spin;

        /// <summary>GML: was_spinning, the facing winding back onto the heading after a spin.</summary>
        public bool WasSpinning => Core.Flight.WasSpinning;

        /// <summary>GML: image_angle, the way the ship points (the transform's rotation).</summary>
        public float Facing => Core.Flight.Facing;

        /// <summary>GML: crouching (Space or the right mouse button held while perched).</summary>
        public bool IsCrouching => Core.Flight.IsCrouching;

        /// <summary>GML: can_jump: perched with the aim within jump_arc of the ship's facing.</summary>
        public bool CanLeap => Core.Flight.CanLeap(AimDirection);

        /// <summary>GML current_rock / current_structure: the surface the ship is perched on, or null.</summary>
        public IPerchSurface Perch { get; private set; }

        /// <summary>
        /// GML in_view for a world point: whether it is inside the room's view 0. The arena the ship plays in sets it
        /// (<see cref="Arena.ArenaRoom.AttachPlayer"/>); with none, everything is in view.
        /// </summary>
        public Func<Vector2, bool> InViewCheck { get; set; }

        bool InView(Vector2 world) => InViewCheck == null || InViewCheck(world);

        /// <summary>
        /// GML: mouse_x/mouse_y, the world point the mouse aims at (where the dotted leap line ends); null while the
        /// aim comes from the gamepad's stick or there is no pointer.
        /// </summary>
        public Vector2? AimPoint { get; private set; }

        /// <summary>The damage gate player_hurt consults: a raised shield absorbs the hit instead of costing HP.</summary>
        public bool BlocksDamage => IsShielding;

        /// <summary>Raised after the ship perches (GML landing collisions; they call set_ammo_cost).</summary>
        public event Action Landed;

        /// <summary>Raised after the ship leaps off a perch (GML player_leap; it calls set_ammo_cost).</summary>
        public event Action Leapt;

        /// <summary>Raised after fling_player throws the ship off its rock.</summary>
        public event Action Flung;

        void Awake() => ResetState();

        /// <summary>
        /// Everything Awake would otherwise do, pulled out so EditMode tests can call it directly: Awake (and the
        /// rest of the MonoBehaviour lifecycle) never runs in EditMode.
        /// </summary>
        public void ResetState()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            Send(new ResetFlight(Id, Tuning)); // GML reset_player: drops the shield, back in flight at full energy
            Perch = null;
            flewLastStep = true; // reset_player: it flies in
            transform.rotation = Quaternion.Euler(0f, 0f, Facing);
            Rocks.Rock.Removed -= OnRockRemoved;
            Rocks.Rock.Removed += OnRockRemoved;
        }

        void Update()
        {
            var player = Controls.Player;

            // GML rotate_counterclockwise/rotate_clockwise: A is +turn_speed, D is -turn_speed; both held cancels to 0,
            // same as the two separate key checks before it. The arrows are not the ship's: they call and fly obj_helper
            // (U.HARE-T66, ReadHelperKeys).
            SetTurnInput(player.Rotate.ReadValue<float>());

            UpdateAim();

            // GML key-press events: the boost and brake keys raise the shield instead while Idle (Core's
            // PressBoostKey/PressBrakeKey, the same commands an online player's NetEntity sends its server).
            if (player.Boost.WasPressedThisFrame()) Send(new PressBoostKey(Id));
            if (player.Brake.WasPressedThisFrame()) Send(new PressBrakeKey(Id));

            // GML key-release events (Idle only): drop the shield and let energy refill.
            if (player.Boost.WasReleasedThisFrame() || player.Brake.WasReleasedThisFrame()) Send(new ReleaseShieldKey(Id));

            // GML Space / right mouse: the press crouches while perched (in flight Space boosts, the right button brakes
            // or, with Ctrl, boosts) and the release is player_leap toward the aim.
            if (player.Leap.WasPressedThisFrame()) Send(new PressLeapKey(Id, LeapKeyAirPress()));
            if (player.Leap.WasReleasedThisFrame()) TryLeap(AimDirection);

            // GML Alt / middle mouse: spin while held in flight, wind the facing back on release.
            if (player.Spin.WasPressedThisFrame()) Send(new PressSpinKey(Id));
            if (player.Spin.WasReleasedThisFrame()) Send(new ReleaseSpinKey(Id));

            ReadHelperKeys();
        }

        /// <summary>
        /// The helper hawk's keys (U.HARE-T66), HareControls' Helper map. obj_helper's own key events run first: the
        /// held left (37) and right (39) arrows turn it and their release stops the turn, the Up (38) and Down (40) presses
        /// step its speed, Page Down (34) toggles solo and Delete (46) sets it off. Then obj_player's own press of any
        /// arrow, create_helper: a new helper at the ship when there is none (it has not seen the press that made it).
        /// </summary>
        void ReadHelperKeys()
        {
            var keys = Controls.Helper;
            Helper.HelperDirector.HoldTurn(Id, keys.TurnLeft.IsPressed(), keys.TurnRight.IsPressed());
            if (keys.Faster.WasPressedThisFrame()) Helper.HelperDirector.Execute(new Helper.PressHelperFaster(Id));
            if (keys.Slower.WasPressedThisFrame()) Helper.HelperDirector.Execute(new Helper.PressHelperSlower(Id));
            if (keys.Solo.WasPressedThisFrame()) Helper.HelperDirector.Execute(new Helper.ToggleHelperSolo(Id));
            if (keys.Detonate.WasPressedThisFrame()) Helper.HelperDirector.Execute(new Helper.DetonateHelper(Id));
            if (keys.TurnLeft.WasPressedThisFrame() || keys.TurnRight.WasPressedThisFrame()
                || keys.Faster.WasPressedThisFrame() || keys.Slower.WasPressedThisFrame())
                CallHelper();
        }

        /// <summary>create_helper: obj_helper at the ship, heading its image_angle, unless this ship's helper exists.</summary>
        public bool CallHelper() => Helper.HelperDirector.Call(Id, transform.position, Facing);

        /// <summary>Space's press boosts in flight; the right mouse button's brakes, or boosts with Ctrl (Alternate) held.</summary>
        AirPress LeapKeyAirPress()
        {
            var player = Controls.Player;
            bool mouse = player.Leap.activeControl != null && player.Leap.activeControl.device is Mouse;
            return mouse && !player.Alternate.IsPressed() ? AirPress.Brake : AirPress.Boost;
        }

        /// <summary>
        /// GML mouse_angle: aimed from the right stick while it is past its deadzone (gamepad), else from the
        /// mouse's screen position (keyboard&amp;mouse); with neither, the aim direction holds.
        /// </summary>
        void UpdateAim()
        {
            var player = Controls.Player;

            Vector2 stick = player.AimStick.ReadValue<Vector2>();
            if (stick.sqrMagnitude > 0.25f) // ~0.5 deadzone, squared
            {
                SetAimDirection(Mathf.Atan2(stick.y, stick.x) * Mathf.Rad2Deg);
                AimPoint = null;
                return;
            }

            var cam = Camera.main;
            if (cam == null || Mouse.current == null) { AimPoint = null; return; } // no pointer device to read a screen position from

            Vector3 screen = player.Aim.ReadValue<Vector2>();
            screen.z = -cam.transform.position.z;
            Vector2 aimPoint = cam.ScreenToWorldPoint(screen);
            AimPoint = aimPoint;
            Vector2 offset = aimPoint - (Vector2)transform.position;
            if (offset.sqrMagnitude > Mathf.Epsilon) SetAimDirection(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        }

        /// <summary>Degrees, GML's math convention (0 = +x, CCW positive).</summary>
        public void SetAimDirection(float degrees) => Send(new SetAim(Id, degrees));

        /// <summary>-1 (clockwise, D) .. +1 (counterclockwise, A), matching GML's direction sign.</summary>
        public void SetTurnInput(float value) => Send(new SetTurn(Id, value));

        /// <summary>GML: player_boost.</summary>
        public bool TryBoost() => Send(new Boost(Id)).Accepted;

        /// <summary>GML: player_brake.</summary>
        public bool TryBrake() => Send(new Brake(Id)).Accepted;

        /// <summary>GML: player_leap toward <paramref name="aimDirection"/> degrees (GML's math convention).</summary>
        public bool TryLeap(float aimDirection) => Send(new Leap(Id, aimDirection)).Accepted;

        /// <summary>Perching where the ship is, with no surface to ride (scripted landings, tests); <see cref="Step"/> lands on rocks itself.</summary>
        public void Land() => Send(new Land(Id));

        /// <summary>
        /// GML obj_player's par_rock / par_structure collision with <paramref name="surface"/>, which the flying ship's mask
        /// touches: unless it is a rock out of view, the ship turns to normal_detect's surface normal, is moved out of
        /// the surface along it, and perches (state_idle, set_ammo_cost) with, on a rock, distance_to_center and land_angle
        /// from where it ended up. Returns whether it landed.
        /// </summary>
        public bool LandOn(IPerchSurface surface)
        {
            if (surface == null || surface.IsGone || State != PlayerFlightState.Moving) return false;
            var flight = Core.Flight;
            if (surface.Kind == SurfaceKind.Rock && (flight.CurrentRock == surface.SurfaceId || !InView(surface.Position))) return false;
            if (surface.Kind == SurfaceKind.Structure && flight.CurrentStructure == surface.SurfaceId) return false;

            Vector2 at = transform.position;
            Vector2 contact = at;
            float arrivalFacing = flight.Facing;
            float normal = PerchProbe.DetectNormal(at, surface.Kind);
            at = PerchProbe.MoveOut(at, normal, surface.Mask);
            Vector2 toCentre = surface.Position - at;
            var landing = new LandingSurface(surface.Kind, surface.SurfaceId, normal,
                Mathf.Atan2(toCentre.y, toCentre.x) * Mathf.Rad2Deg, toCentre.magnitude, surface.SpeedPxPerStep, surface.SpinPerStep);
            if (!Send(new LandOn(Id, landing)).Accepted) return false;

            // par_rock's landing kicks up dirt (ef_firework, spawn_chips) 32 px along the image_angle it flew in at, from
            // where it touched, before normal_detect turns it and the move-out loop lifts it (U.HARE-T23).
            if (surface.Kind == SurfaceKind.Rock) Pickups.PickupDirector.KickUpDirt(contact, arrivalFacing);
            // par_structure's landing kicks up only the grey ef_firework, 32 px along image_angle, and no chips (U.HARE-T56).
            else Effects.GmlEffects.CreateAbove(Effects.GmlEffectKind.Firework,
                Enemies.CoreConvert.ToVector2(Pickups.PickupDirector.Field.FeetOf(Enemies.CoreConvert.ToVec2(contact), arrivalFacing)),
                0, Pickups.PickupDirector.Gray);

            Perch = surface;
            Place(at);
            return true;
        }

        /// <summary>The landing check the ship's Step makes while it flies: the first surface its mask touches that it lands on.</summary>
        bool TryLandOnTouchingSurface()
        {
            foreach (var surface in PerchProbe.Touching(transform.position, Facing))
                if (LandOn(surface)) return true;
            return false;
        }

        /// <summary>
        /// rock_destroyed and par_rock's leaving the room: <c>if(ID == player.current_rock) { fling_player(); current_rock =
        /// noone; }</c>. setup_level's clear (and move_rocks) only lets go of it.
        /// </summary>
        void OnRockRemoved(Rocks.Rock rock, Rocks.RockGone gone)
        {
            if (rock == null || !ReferenceEquals(Perch, rock)) return;
            if (Core.Flight.CurrentRock != ((IPerchSurface)rock).SurfaceId) { Perch = null; return; }
            Perch = null;
            if (gone.Cause == Rocks.RockRemoval.Cleared) Send(new LetGoOfRock(Id));
            else Send(new Fling(Id));
        }

        /// <summary>
        /// obj_surface_reflection's water line (U.HARE-T58): direction and image_angle set to <paramref name="direction"/>
        /// (0 or 180); the ship turns and, in flight, flies on along it at once.
        /// </summary>
        public void SkimSurface(float direction)
        {
            Send(new SkimSurface(Id, direction));
            var flight = Core.Flight;
            transform.rotation = Quaternion.Euler(0f, 0f, flight.Facing);
            if (body != null && flight.State == PlayerFlightState.Moving)
                body.linearVelocity = new Vector2(Mathf.Cos(flight.Direction * Mathf.Deg2Rad), Mathf.Sin(flight.Direction * Mathf.Deg2Rad)) * flight.Speed;
        }

        /// <summary>
        /// obj_level_results' Enter (U.HARE-T22): <c>player.state = state_leave; player.invulnerable = true</c>. The room
        /// steers it from then on (<see cref="AimExit"/>). Returns whether it took effect.
        /// </summary>
        public bool Leave() => Send(new LeaveLevel(Id)).Accepted;

        /// <summary>The leaving ship's exit_angle, degrees (GML's convention), as the room works it out each step.</summary>
        public bool AimExit(float degrees) => Send(new AimExit(Id, degrees)).Accepted;

        /// <summary>GML: fling_player (the ship's rock broke or left the room, or obj_rocket hit it there).</summary>
        public bool Fling() => Send(new Fling(Id)).Accepted;

        /// <summary>GML: player_shield. Only while perched (Idle) and with at least 33% energy.</summary>
        public bool TryShield() => Send(new RaiseShield(Id)).Accepted;

        /// <summary>GML: shield key release, leap, reset_player, or energy hitting zero.</summary>
        public void DropShield() => Send(new DropShield(Id));

        /// <summary>
        /// GML: player_hurt's "HP reached 0" flight half: the ship stops dead. Health reaches this through the
        /// rules; it is public for tests and scripted deaths.
        /// </summary>
        public void Die() => Send(new HaltShip(Id));

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>
        /// One physics step; public so EditMode tests can drive it (FixedUpdate never runs there). In obj_player's Step
        /// order: a ship on a rock turns and moves with it (land_angle and image_angle += the rock's spin, then x, y = the
        /// rock's centre minus lengthdir(distance_to_center, land_angle)); then the flight rules step; a flying ship lands
        /// on the first rock or structure its mask touches, or flies on along its heading, drawn at its facing.
        /// </summary>
        public void Step(float dt)
        {
            // obj_player's Step: if(state != state_leave) { if(!paused) { ... } else { speed = 0; } } (U.HARE-T22): while the
            // game is paused (obj_level_results up) the ship stands still; a leaving ship flies on whatever paused says.
            if (Gml.Paused && Core.Flight.State != PlayerFlightState.Leaving)
            {
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }

            RideRock(dt);
            Send(new StepFlight(Id, dt));

            // GameMaker moves an instance before its collision events, so a ship that has only just left its perch (a leap
            // or fling since the last step) takes off before it can land again.
            var flight = Core.Flight;
            if (flight.State == PlayerFlightState.Moving && flewLastStep) TryLandOnTouchingSurface();

            transform.rotation = Quaternion.Euler(0f, 0f, flight.Facing);
            flewLastStep = flight.State == PlayerFlightState.Moving;
            if (!flewLastStep && flight.State != PlayerFlightState.Leaving)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 heading = new Vector2(Mathf.Cos(flight.Direction * Mathf.Deg2Rad), Mathf.Sin(flight.Direction * Mathf.Deg2Rad));
            body.linearVelocity = heading * flight.Speed;
        }

        void RideRock(float dt)
        {
            var flight = Core.Flight;
            if (Perch == null) return;
            if ((Perch is UnityEngine.Object o && o == null) || Perch.IsGone || flight.State != PlayerFlightState.Idle)
            {
                Perch = null;
                return;
            }
            if (Perch.Kind != SurfaceKind.Rock || flight.CurrentRock != Perch.SurfaceId) return;

            Send(new RideRock(Id, Perch.SpinPerStep, Perch.SpeedPxPerStep, dt));
            float angle = flight.LandAngle * Mathf.Deg2Rad;
            Place(Perch.Position - new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * flight.DistanceToCenter);
        }

        void Place(Vector2 at)
        {
            transform.position = new Vector3(at.x, at.y, transform.position.z);
            if (body != null) body.position = at;
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case ShipLanded landed:
                    if (body != null) body.linearVelocity = Vector2.zero;
                    transform.rotation = Quaternion.Euler(0f, 0f, Facing);
                    if (landed.Surface == null) Perch = null;
                    Landed?.Invoke();
                    break;
                case ShipLeapt leapt:
                    // player_leap kicks up dirt at its feet on the perch before it turns to the aim (U.HARE-T23).
                    Pickups.PickupDirector.KickUpDirt(transform.position, leapt.PerchedFacing);
                    Perch = null;
                    transform.rotation = Quaternion.Euler(0f, 0f, Facing);
                    Leapt?.Invoke();
                    break;
                case ShipFlung _:
                    Perch = null;
                    Flung?.Invoke();
                    break;
                case ShipLeaving _:
                    Perch = null;
                    flewLastStep = false;
                    transform.rotation = Quaternion.Euler(0f, 0f, Facing);
                    break;
                case ShipHalted _:
                    Perch = null;
                    if (body != null) body.linearVelocity = Vector2.zero;
                    break;
                case ShieldRaised raised:
                    RaiseWall(raised.Radius);
                    break;
                case ShieldDropped _:
                    RemoveWall();
                    break;
                case PlayerRespawned respawned:
                    Perch = null;
                    transform.rotation = Quaternion.Euler(0f, 0f, respawned.Direction);
                    break;
            }
        }

        void RaiseWall(float radius)
        {
            RemoveWall();
            var go = new GameObject("ShieldWall");
            Shield = go.AddComponent<ShieldWall>();
            Shield.Init(this, radius);
        }

        void RemoveWall()
        {
            if (Shield == null) return;
            if (Application.isPlaying) Destroy(Shield.gameObject); else DestroyImmediate(Shield.gameObject);
            Shield = null;
        }

        protected override void OnDestroy()
        {
            Rocks.Rock.Removed -= OnRockRemoved;
            RemoveWall();
            if (controls != null)
            {
                controls.Player.Disable();
                controls.Helper.Disable();
                // Dispose() calls Object.Destroy(asset), which Unity refuses outside Play mode (e.g. an EditMode
                // test's DestroyImmediate on this ship, which still runs OnDestroy synchronously); DestroyImmediate
                // the underlying asset directly when there is no Play mode to defer the real destroy to.
                if (Application.isPlaying) controls.Dispose();
                else UnityEngine.Object.DestroyImmediate(controls.asset);
                controls = null;
            }
            base.OnDestroy();
        }
    }
}
