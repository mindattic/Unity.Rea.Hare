using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_wormhole_emitter (gmlHARE), the generator mode's pacer (gm_generator, from
    /// obj_btn_diff0/obj_btn_diff100/obj_btn_level09): setup_level runs
    /// generate_enemy_list(difficulty) and creates this emitter, which
    /// <list type="bullet">
    /// <item>Create: opens a wormhole for the first 10% of the list at once
    /// (repeat(enemy_count * 0.1), the fraction dropped), then sets Alarm 0 to
    /// 1 + room_speed * irandom_range(0, 4) steps;</item>
    /// <item>Alarm 0: opens a wormhole for the next enemy, or, once the list is spent, destroys
    /// itself and runs set_level_complete; then re-arms 1 + room_speed * irandom_range(0, 8).</item>
    /// </list>
    /// So after the opening burst one enemy arrives every 1 step to 8 s (4 s on average), each
    /// 4 s after its wormhole appears, and the level ends one alarm after the last wormhole opens.
    /// </summary>
    /// <remarks>
    /// setup_level calls instance_create(obj_wormhole_emitter, 0, 0) with its arguments swapped,
    /// and the globals enemy_count/current_enemy are never reset; the port creates the emitter
    /// properly and keeps its list and cursor per emitter.
    /// </remarks>
    public class WormholeEmitter : MonoBehaviour
    {
        public const float OpeningFraction = 0.1f;
        /// <summary>Create's rate = 1 + room_speed * irandom_range(0, 4).</summary>
        public const int FirstRateMaxSeconds = 4;
        /// <summary>Alarm 0's rate = 1 + room_speed * irandom_range(0, 8).</summary>
        public const int RateMaxSeconds = 8;

        [SerializeField] int difficulty = 100;
        [SerializeField] bool initOnStart;

        readonly List<EnemyKind> list = new List<EnemyKind>();
        readonly List<Wormhole> wormholes = new List<Wormhole>();
        float alarm = -1f;
        bool finished;
        Rect room;
        PlayerShipController player;

        /// <summary>GML enemy_list.</summary>
        public IReadOnlyList<EnemyKind> List => list;
        /// <summary>GML current_enemy: how many wormholes have opened.</summary>
        public int CurrentEnemy { get; private set; }
        /// <summary>Every wormhole this emitter opened, in order.</summary>
        public IReadOnlyList<Wormhole> Wormholes => wormholes;
        public float AlarmSteps => alarm;
        public bool Finished => finished;

        /// <summary>A wormhole just opened.</summary>
        public event Action<WormholeEmitter, Wormhole> WormholeOpened;
        /// <summary>The list is spent: the emitter is gone and set_level_complete ran.</summary>
        public event Action<WormholeEmitter> LevelCompleted;

        void Start()
        {
            if (initOnStart && list.Count == 0 && !finished) Init(EnemyList.Generate(difficulty));
        }

        /// <summary>setup_level's generator branch: generate_enemy_list(difficulty), then this emitter's Create.</summary>
        public static WormholeEmitter Create(int difficulty, Rect? room = null, PlayerShipController player = null)
        {
            var emitter = new GameObject("WormholeEmitter").AddComponent<WormholeEmitter>();
            emitter.Init(EnemyList.Generate(difficulty), room, player);
            return emitter;
        }

        /// <summary>GML Create over <paramref name="enemies"/>.</summary>
        public void Init(IEnumerable<EnemyKind> enemies, Rect? room = null, PlayerShipController player = null, int? firstRateSteps = null)
        {
            list.Clear();
            list.AddRange(enemies);
            wormholes.Clear();
            CurrentEnemy = 0;
            finished = false;
            this.room = room ?? Gml.Room;
            this.player = player;

            int opening = Mathf.FloorToInt(list.Count * OpeningFraction);
            for (int i = 0; i < opening; i++) OpenNext();

            alarm = firstRateSteps ?? Rate(FirstRateMaxSeconds);
        }

        static int Rate(int maxSeconds) => 1 + (int)Gml.StepsPerSecond * EnemySpawner.IRandom(0, maxSeconds);

        void OpenNext()
        {
            var wormhole = Wormhole.Create(list[CurrentEnemy], room, player);
            CurrentEnemy++;
            wormholes.Add(wormhole);
            WormholeOpened?.Invoke(this, wormhole);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (Alarm 0), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (finished || alarm < 0f) return;
            alarm -= dt * Gml.StepsPerSecond;
            if (alarm > 1e-3f) return;
            alarm = -1f;

            if (CurrentEnemy < list.Count)
            {
                OpenNext();
                alarm = Rate(RateMaxSeconds);
            }
            else
            {
                finished = true;
                // set_level_complete: level_complete = true, obj_level_results, paused = true (UH-T22 shows the results).
                Gml.Paused = true;
                LevelCompleted?.Invoke(this);
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }
    }
}
