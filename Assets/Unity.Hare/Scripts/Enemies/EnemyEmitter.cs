using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports par_enemy_emitter (gmlHARE), the looping vortex setup_level places per enemy type:
    /// <list type="bullet">
    /// <item>Create: Alarm 0 = irandom_range(1, room_speed * 2), idle.</item>
    /// <item>Idle (first pass): jump to a random point at least sprite width x scale inside the
    /// room, randomise both angles, wait irandom_range(instance_wait_min, instance_wait_max).</item>
    /// <item>Open: 120 steps, growing only while unpaused and instance_number(instance) &lt;
    /// instance_max.</item>
    /// <item>Spawn (first pass): one enemy if unpaused and still under instance_max; 120 steps.</item>
    /// <item>Close: 120 steps, shrinking to nothing; then idle again.</item>
    /// </list>
    /// instance_max caps the live count of that enemy object across the whole room, so a level
    /// keeps topping its population back up to the cap as the ship kills them.
    /// </summary>
    public class EnemyEmitter : Vortex
    {
        /// <summary>Create's alarm[0] = irandom_range(1, room_speed * 2).</summary>
        public const int FirstAlarmMaxSteps = 240;
        /// <summary>Open, spawn and close each last alarm[0] = 120 steps.</summary>
        public const int PhaseSteps = 120;
        /// <summary>setup_level's instance_wait_min/max for every emitter it places.</summary>
        public const int DefaultWaitMinSteps = 200;
        public const int DefaultWaitMaxSteps = 500;

        [SerializeField] EnemyKind instance = EnemyKind.Hydra;
        [SerializeField] int instanceMin = 1;
        [SerializeField] int instanceMax = 2;
        [SerializeField] float scale = 0.25f;
        [SerializeField] bool initOnStart = true;

        bool initialised;

        /// <summary>GML instance_min: set by setup_level but never read by the emitter.</summary>
        public int InstanceMin { get; set; } = 1;
        /// <summary>GML instance_max: no spawn while this many of the object are alive.</summary>
        public int InstanceMax { get; set; } = 2;
        public int WaitMinSteps { get; set; } = DefaultWaitMinSteps;
        public int WaitMaxSteps { get; set; } = DefaultWaitMaxSteps;
        /// <summary>GML buffer = sprite_get_width(spr_vortex) * scale.</summary>
        public float BufferPx => SpriteWidthPx * Scale;

        void Start()
        {
            if (initOnStart && !initialised)
                Init(instance, transform.position, instanceMin, instanceMax, scale);
        }

        /// <summary>
        /// GML instance_create(x, y, par_enemy_emitter) followed by setup_level's field writes.
        /// <paramref name="firstAlarmSteps"/> pins Create's random first alarm (tests).
        /// </summary>
        public void Init(EnemyKind kind, Vector2 position, int instanceMin, int instanceMax, float scale,
            Rect? room = null, PlayerShipController player = null,
            int waitMinSteps = DefaultWaitMinSteps, int waitMaxSteps = DefaultWaitMaxSteps, int? firstAlarmSteps = null)
        {
            initialised = true;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            Instance = kind;
            InstanceMin = instanceMin;
            InstanceMax = instanceMax;
            Scale = scale;
            WaitMinSteps = waitMinSteps;
            WaitMaxSteps = waitMaxSteps;
            Player = player;
            SetupVortex(room);
            SetAlarm(firstAlarmSteps ?? Random.Range(1, FirstAlarmMaxSteps + 1));
        }

        bool BelowCap => EnemySpawner.Count(Instance) < InstanceMax;

        /// <summary>The enemy released by the latest spawn phase, if any.</summary>
        public Component LastSpawned { get; private set; }

        protected override void StepEvent(float steps)
        {
            switch (Mode)
            {
                case VortexMode.Idle:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Vector2 p = EnemySpawner.RandomPointInside(Room, BufferPx);
                        transform.position = new Vector3(p.x, p.y, transform.position.z);
                        SetAngles(Random.Range(0, 360), Random.Range(0, 360));
                        SetAlarm(EnemySpawner.IRandom(WaitMinSteps, WaitMaxSteps));
                    }
                    break;

                case VortexMode.Open:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        SetAlarm(PhaseSteps);
                    }
                    if (!Gml.Paused && BelowCap) Grow(steps);
                    break;

                case VortexMode.Spawn:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        LastSpawned = !Gml.Paused && BelowCap ? Release() : null;
                        SetAlarm(PhaseSteps);
                    }
                    break;

                case VortexMode.Close:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        SetAlarm(PhaseSteps);
                    }
                    Shrink(steps);
                    break;
            }
        }
    }
}
