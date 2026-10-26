using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Content;
using Unity.Hare.Input;

namespace Unity.Hare.Player
{
    /// <summary>
    /// The view on obj_player's flight state machine (gmlHARE, Create/Step/Alarm events). The rules (continuous
    /// forward flight, boost and brake gated by energy, leap-from-idle within a facing tolerance, the Idle-only
    /// shield) live in Core's <see cref="ShipFlight"/>; this component turns Input System actions into commands,
    /// moves the body from the flight state each physics step and draws the shield wall on the events.
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

        /// <summary>GML: spin, degrees a step the ship turns while it flies spinning (0 unless flung, U.HARE-T53).</summary>
        public float Spin => Core.Flight.Spin;

        /// <summary>GML: can_jump: perched with the aim within jump_arc of the ship's facing.</summary>
        public bool CanLeap => Core.Flight.CanLeap(AimDirection);

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
            transform.rotation = Quaternion.Euler(0f, 0f, Direction);
        }

        void Update()
        {
            var player = Controls.Player;

            // GML rotate_counterclockwise/rotate_clockwise: A (and the left arrow) is +turn_speed, D (and the
            // right arrow) is -turn_speed; both held cancels to 0, same as the two separate key checks before it.
            SetTurnInput(player.Rotate.ReadValue<float>());

            UpdateAim();

            // GML key-press events: the boost and brake keys raise the shield instead while Idle (Core's
            // PressBoostKey/PressBrakeKey, the same commands an online player's NetEntity sends its server).
            if (player.Boost.WasPressedThisFrame()) Send(new PressBoostKey(Id));
            if (player.Brake.WasPressedThisFrame()) Send(new PressBrakeKey(Id));

            // GML key-release events (Idle only): drop the shield and let energy refill.
            if (player.Boost.WasReleasedThisFrame() || player.Brake.WasReleasedThisFrame()) Send(new ReleaseShieldKey(Id));
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

        /// <summary>Perching on a rock/structure (par_rock/par_structure); the Rocks epic (UH-T19/UH-T20) calls this.</summary>
        public void Land() => Send(new Land(Id));

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

        /// <summary>One physics step; public so EditMode tests can drive it (FixedUpdate never runs there).</summary>
        public void Step(float dt)
        {
            Send(new StepFlight(Id, dt));

            var flight = Core.Flight;
            if (flight.State != PlayerFlightState.Moving)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            transform.rotation = Quaternion.Euler(0f, 0f, flight.Direction);
            Vector2 heading = new Vector2(Mathf.Cos(flight.Direction * Mathf.Deg2Rad), Mathf.Sin(flight.Direction * Mathf.Deg2Rad));
            body.linearVelocity = heading * flight.Speed;
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case ShipLanded _:
                    if (body != null) body.linearVelocity = Vector2.zero;
                    Landed?.Invoke();
                    break;
                case ShipLeapt _:
                    Leapt?.Invoke();
                    break;
                case ShipHalted _:
                    if (body != null) body.linearVelocity = Vector2.zero;
                    break;
                case ShieldRaised raised:
                    RaiseWall(raised.Radius);
                    break;
                case ShieldDropped _:
                    RemoveWall();
                    break;
                case PlayerRespawned respawned:
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
            RemoveWall();
            if (controls != null)
            {
                controls.Player.Disable();
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
