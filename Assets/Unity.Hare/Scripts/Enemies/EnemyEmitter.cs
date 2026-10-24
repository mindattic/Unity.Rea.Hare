using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="EmitterBody"/> (par_enemy_emitter, gmlHARE), the looping vortex setup_level places per
    /// enemy type. Core runs its idle, open, spawn and close phases, the instance_max cap and the random placement;
    /// this draws the vortex and shows the enemy each spawn phase released.
    /// </summary>
    public class EnemyEmitter : Vortex
    {
        public const int FirstAlarmMaxSteps = EmitterBody.FirstAlarmMaxSteps;
        public const int PhaseSteps = EmitterBody.PhaseSteps;
        public const int DefaultWaitMinSteps = EmitterBody.DefaultWaitMinSteps;
        public const int DefaultWaitMaxSteps = EmitterBody.DefaultWaitMaxSteps;

        [SerializeField] EnemyKind instance = EnemyKind.Hydra;
        [SerializeField] int instanceMin = 1;
        [SerializeField] int instanceMax = 2;
        [SerializeField] float scale = 0.25f;
        [SerializeField] bool initOnStart = true;

        bool initialised;

        EmitterBody Rules => (EmitterBody)Core;

        /// <summary>GML instance_min: set by setup_level but never read by the emitter.</summary>
        public int InstanceMin => Rules.InstanceMin;
        /// <summary>GML instance_max: no spawn while this many of the object are alive.</summary>
        public int InstanceMax => Rules.InstanceMax;
        public int WaitMinSteps => Rules.WaitMinSteps;
        public int WaitMaxSteps => Rules.WaitMaxSteps;
        /// <summary>GML buffer = sprite_get_width(spr_vortex) * scale.</summary>
        public float BufferPx => Rules.BufferPx;

        /// <summary>The enemy released by the latest spawn phase, if any.</summary>
        public Component LastSpawned => Rules.LastSpawned == null ? null : EnemyDirector.ViewOf<Component>(Rules.LastSpawned.Id);

        void Start()
        {
            if (initOnStart && !initialised && Core == null)
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
            var result = EnemyDirector.Execute(new AddEmitter(kind, position.ToVec2(), instanceMin, instanceMax, scale,
                room?.ToBounds(), waitMinSteps, waitMaxSteps, firstAlarmSteps), this);
            Attach(result.Vortex, player);
        }
    }
}
