using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Hare.Player
{
    /// <summary>Mirrors obj_player's two top-level states (GML: state_idle / state_moving).</summary>
    public enum PlayerFlightState
    {
        Idle,
        Moving
    }

    /// <summary>
    /// Ports obj_player's flight state machine (gmlHARE, Create/Step/Alarm events): continuous
    /// forward flight along <see cref="Direction"/> at <see cref="Speed"/>, A/D rotation,
    /// boost/brake gated by an energy resource, leap-from-idle within a facing tolerance, and the
    /// Idle-only shield (player_shield/obj_shieldwall) that drains the same energy.
    /// GML's per-step deltas (room_gamescreen runs at 120 steps/sec) are expressed here as
    /// per-second rates. Exact speed/energy numbers are the original's ratios (brake is half of
    /// flight speed, boost is double); absolute values are placeholders to retune once real
    /// sprites set the project's world scale (UH-T2 note). Energy drain/fill rates are this
    /// controller's own placeholders until obj_energybar is ported (UH-T15).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerShipController : MonoBehaviour
    {
        [Header("Flight (GML: flight_speed, brake_speed, turn_speed)")]
        [SerializeField] float flightSpeed = 5f;
        [SerializeField] float brakeSpeed = 2.5f;
        [SerializeField] float turnSpeedDegreesPerSecond = 120f;
        [SerializeField] float brakeTurnSpeedDegreesPerSecond = 240f;
        [SerializeField] float speedChangeRatePerSecond = 6f;

        [Header("Boost / brake energy (GML: obj_energybar gate at 33%)")]
        [SerializeField] float maxEnergy = 100f;
        [SerializeField, Range(0f, 1f)] float minEnergyFractionToStart = 0.33f;
        [SerializeField] float energyDrainPerSecond = 40f;
        [SerializeField] float energyFillPerSecond = 25f;

        [Header("Leap (GML: jump_arc, only from Idle)")]
        [SerializeField] float jumpArcDegrees = 100f;

        [Header("Shield (GML: player_shield / obj_shieldwall, only from Idle)")]
        [SerializeField] float shieldRadius = 0.75f;

        Rigidbody2D body;
        float turnInput;

        public PlayerFlightState State { get; private set; } = PlayerFlightState.Moving;
        public bool IsBoosting { get; private set; }
        public bool IsBraking { get; private set; }
        public float Energy { get; private set; }
        public float Direction { get; private set; } = 270f;
        public float Speed { get; private set; }

        /// <summary>GML: shielding. While true, weapons hold fire (fire_primary/fire_secondary) and incoming hits are blocked.</summary>
        public bool IsShielding { get; private set; }

        /// <summary>The live obj_shieldwall instance while <see cref="IsShielding"/>, else null.</summary>
        public ShieldWall Shield { get; private set; }

        /// <summary>GML: mouse_angle, degrees in GML's math convention. The shield wall faces this way.</summary>
        public float AimDirection { get; private set; } = 270f;

        /// <summary>
        /// The damage gate the health ticket (UH-T6, player_hurt) consults: a raised shield absorbs
        /// the hit instead of costing HP.
        /// </summary>
        public bool BlocksDamage => IsShielding;

        void Awake() => ResetState();

        /// <summary>
        /// Everything Awake would otherwise do, pulled out so EditMode tests can call it directly:
        /// Awake (and the rest of the MonoBehaviour lifecycle) never runs in EditMode, only in Play
        /// Mode or a build, so a test that only calls AddComponent never sees body get cached or
        /// Energy/Speed get initialized.
        /// </summary>
        public void ResetState()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            DropShield(); // GML reset_player: shielding = false; with(obj_shieldwall) instance_destroy()
            State = PlayerFlightState.Moving;
            IsBoosting = false;
            IsBraking = false;
            Direction = 270f;
            Speed = flightSpeed;
            Energy = maxEnergy;
            transform.rotation = Quaternion.Euler(0f, 0f, Direction);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            float turn = 0f;
            if (keyboard.aKey.isPressed) turn += 1f; // GML rotate_counterclockwise: direction += turn_speed
            if (keyboard.dKey.isPressed) turn -= 1f; // GML rotate_clockwise: direction -= turn_speed
            SetTurnInput(turn);

            UpdateAimFromMouse();

            // GML key-press events: the boost and brake keys raise the shield instead while Idle.
            if (keyboard.wKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                if (State == PlayerFlightState.Moving) TryBoost(); else TryShield();
            }
            if (keyboard.sKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame)
            {
                if (State == PlayerFlightState.Moving) TryBrake(); else TryShield();
            }

            // GML key-release events (Idle only): drop the shield and let energy refill.
            bool shieldKeyReleased = keyboard.wKey.wasReleasedThisFrame || keyboard.spaceKey.wasReleasedThisFrame
                || keyboard.sKey.wasReleasedThisFrame || keyboard.leftShiftKey.wasReleasedThisFrame;
            if (shieldKeyReleased && State == PlayerFlightState.Idle) DropShield();
        }

        void UpdateAimFromMouse()
        {
            var mouse = Mouse.current;
            var cam = Camera.main;
            if (mouse == null || cam == null) return;

            Vector3 screen = mouse.position.ReadValue();
            screen.z = -cam.transform.position.z;
            Vector2 offset = (Vector2)cam.ScreenToWorldPoint(screen) - (Vector2)transform.position;
            if (offset.sqrMagnitude > Mathf.Epsilon) SetAimDirection(Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        }

        /// <summary>Degrees, GML's math convention (0 = +x, CCW positive).</summary>
        public void SetAimDirection(float degrees) => AimDirection = Mathf.Repeat(degrees, 360f);

        /// <summary>-1 (clockwise, D) .. +1 (counterclockwise, A), matching GML's direction sign.</summary>
        public void SetTurnInput(float value) => turnInput = Mathf.Clamp(value, -1f, 1f);

        /// <summary>GML: player_boost. Instant speed jump, held while energy lasts.</summary>
        public bool TryBoost()
        {
            if (State != PlayerFlightState.Moving || IsBraking) return false;
            if (Energy < maxEnergy * minEnergyFractionToStart) return false;

            IsBoosting = true;
            Speed = flightSpeed * 2f;
            return true;
        }

        /// <summary>GML: player_brake. Speed eases down to brake_speed over the following steps.</summary>
        public bool TryBrake()
        {
            if (State != PlayerFlightState.Moving || IsBoosting) return false;
            if (Energy < maxEnergy * minEnergyFractionToStart) return false;

            IsBraking = true;
            return true;
        }

        /// <summary>
        /// GML: player_leap. Only launches from Idle, and only within jump_arc of the current
        /// facing; aimDirection is degrees, GML's math convention (0 = +x, CCW positive).
        /// </summary>
        public bool TryLeap(float aimDirection)
        {
            if (State != PlayerFlightState.Idle) return false;
            if (Mathf.Abs(Mathf.DeltaAngle(Direction, aimDirection)) > jumpArcDegrees) return false;

            DropShield(); // GML player_leap: shielding = false; with(obj_shieldwall) instance_destroy()
            Direction = aimDirection;
            Speed = flightSpeed;
            IsBoosting = false;
            IsBraking = false;
            State = PlayerFlightState.Moving;
            return true;
        }

        /// <summary>Perching on a rock/structure (par_rock/par_structure); the Rocks epic (UH-T19/UH-T20) calls this.</summary>
        public void Land()
        {
            State = PlayerFlightState.Idle;
            IsBoosting = false;
            IsBraking = false;
            Speed = 0f;
            body.linearVelocity = Vector2.zero;
        }

        /// <summary>
        /// GML: player_shield. Only while perched (Idle) and with at least 33% energy; spawns the
        /// obj_shieldwall hitbox, which drains energy until released or empty.
        /// </summary>
        public bool TryShield()
        {
            if (State != PlayerFlightState.Idle || IsShielding) return false;
            if (Energy < maxEnergy * minEnergyFractionToStart) return false; // GML: sfx_disabled

            IsShielding = true;
            var go = new GameObject("ShieldWall");
            Shield = go.AddComponent<ShieldWall>();
            Shield.Init(this, shieldRadius);
            return true;
        }

        /// <summary>GML: shield key release, leap, reset_player, or energy hitting zero.</summary>
        public void DropShield()
        {
            IsShielding = false;
            if (Shield == null) return;
            if (Application.isPlaying) Destroy(Shield.gameObject); else DestroyImmediate(Shield.gameObject);
            Shield = null;
        }

        void OnDestroy() => DropShield();

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>One physics step; public so EditMode tests can drive it (FixedUpdate never runs there).</summary>
        public void Step(float dt)
        {
            TickEnergy(dt);

            // GML obj_player Step "Shielding": the wall collapses once the energy bar is empty.
            if (IsShielding && Energy <= 0f) DropShield();

            if (State != PlayerFlightState.Moving)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }

            float turnSpeed = IsBraking ? brakeTurnSpeedDegreesPerSecond : turnSpeedDegreesPerSecond;
            Direction += turnInput * turnSpeed * dt;

            if (IsBoosting && Energy <= 0f)
            {
                Speed = Mathf.MoveTowards(Speed, flightSpeed, speedChangeRatePerSecond * dt);
                if (Mathf.Approximately(Speed, flightSpeed)) IsBoosting = false;
            }
            else if (IsBraking)
            {
                Speed = Mathf.MoveTowards(Speed, brakeSpeed, speedChangeRatePerSecond * dt);
                if (Energy <= 0f)
                {
                    IsBraking = false;
                    Speed = flightSpeed;
                }
            }

            transform.rotation = Quaternion.Euler(0f, 0f, Direction);
            Vector2 heading = new Vector2(Mathf.Cos(Direction * Mathf.Deg2Rad), Mathf.Sin(Direction * Mathf.Deg2Rad));
            body.linearVelocity = heading * Speed;
        }

        void TickEnergy(float dt)
        {
            if (IsBoosting || IsBraking || IsShielding)
                Energy = Mathf.Max(0f, Energy - energyDrainPerSecond * dt);
            else
                Energy = Mathf.Min(maxEnergy, Energy + energyFillPerSecond * dt);
        }
    }
}
