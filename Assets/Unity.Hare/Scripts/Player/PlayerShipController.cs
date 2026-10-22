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
    /// boost/brake gated by an energy resource, and leap-from-idle within a facing tolerance.
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

        Rigidbody2D body;
        float turnInput;

        public PlayerFlightState State { get; private set; } = PlayerFlightState.Moving;
        public bool IsBoosting { get; private set; }
        public bool IsBraking { get; private set; }
        public float Energy { get; private set; }
        public float Direction { get; private set; } = 270f;
        public float Speed { get; private set; }

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

            if (keyboard.wKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) TryBoost();
            if (keyboard.sKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame) TryBrake();
        }

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

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            TickEnergy(dt);

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
            if (IsBoosting || IsBraking)
                Energy = Mathf.Max(0f, Energy - energyDrainPerSecond * dt);
            else
                Energy = Mathf.Min(maxEnergy, Energy + energyFillPerSecond * dt);
        }
    }
}
