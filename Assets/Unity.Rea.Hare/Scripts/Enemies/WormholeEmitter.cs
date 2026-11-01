using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="WormholeEmitterBody"/> (obj_wormhole_emitter, gmlHARE), the generator mode's pacer. Core
    /// keeps the enemy list, the opening burst and the alarm that opens one wormhole after another; this sends each
    /// step, gives every wormhole it opens a view and, once the list is spent, runs set_level_complete (pauses the room)
    /// and removes itself.
    /// </summary>
    public class WormholeEmitter : MonoBehaviour, IEnemyEventSink
    {
        public static float OpeningFraction => WormholeEmitterBody.OpeningFraction;
        public static int FirstRateMaxSeconds => WormholeEmitterBody.FirstRateMaxSeconds;
        public static int RateMaxSeconds => WormholeEmitterBody.RateMaxSeconds;

        [SerializeField] int difficulty = 100;
        [SerializeField] bool initOnStart;

        readonly List<Wormhole> wormholes = new List<Wormhole>();
        PlayerShipController player;
        bool initialised;

        /// <summary>The Core state this view shows (read-only for presentation).</summary>
        public WormholeEmitterBody Core { get; private set; }

        /// <summary>GML enemy_list.</summary>
        public IReadOnlyList<EnemyKind> List => Core != null ? Core.List : Array.Empty<EnemyKind>();
        /// <summary>GML current_enemy: how many wormholes have opened.</summary>
        public int CurrentEnemy => Core != null ? Core.CurrentEnemy : 0;
        /// <summary>Every wormhole this emitter opened, in order.</summary>
        public IReadOnlyList<Wormhole> Wormholes => wormholes;
        public float AlarmSteps => Core != null ? Core.AlarmSteps : -1f;
        public bool Finished => Core != null && Core.Finished;

        /// <summary>A wormhole just opened.</summary>
        public event Action<WormholeEmitter, Wormhole> WormholeOpened;
        /// <summary>The list is spent: the emitter is gone and set_level_complete ran.</summary>
        public event Action<WormholeEmitter> LevelCompleted;

        void Start()
        {
            if (initOnStart && !initialised && Core == null) Begin(new AddWormholeEmitter(difficulty), null);
        }

        /// <summary>setup_level's generator branch: generate_enemy_list(difficulty), then this emitter's Create.</summary>
        public static WormholeEmitter Create(int difficulty, Rect? room = null, PlayerShipController player = null)
        {
            var emitter = new GameObject("WormholeEmitter").AddComponent<WormholeEmitter>();
            emitter.Begin(new AddWormholeEmitter(difficulty, room?.ToBounds()), player);
            return emitter;
        }

        /// <summary>GML Create over <paramref name="enemies"/>.</summary>
        public void Init(IEnumerable<EnemyKind> enemies, Rect? room = null, PlayerShipController player = null, int? firstRateSteps = null) =>
            Begin(new AddWormholeEmitter(enemies, room?.ToBounds(), firstRateSteps), player);

        void Begin(AddWormholeEmitter command, PlayerShipController ship)
        {
            initialised = true;
            player = ship;
            wormholes.Clear();
            var result = EnemyDirector.Execute(command, this);
            Core = result.Emitter;
            EnemyDirector.Register(Core.Id, this);
            // The opening burst's wormholes were made (and announced) during Create, before this view knew its Core.
            foreach (var w in Core.Wormholes) Claim(w);
        }

        void Claim(WormholeBody body)
        {
            var view = EnemyDirector.ViewOf<Wormhole>(body.Id);
            if (view == null || wormholes.Contains(view)) return;
            if (player != null) view.Player = player;
            wormholes.Add(view);
            WormholeOpened?.Invoke(this, view);
        }

        void OnDestroy() => EnemyDirector.Dispose(Core, this);

        /// <summary>One GML step (Alarm 0), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (Core == null || Core.Finished) return;
            EnemyDirector.Execute(new TickWormholeEmitter(Core.Id, dt));
        }

        public void OnEnemyEvent(EnemyEvent e)
        {
            switch (e)
            {
                case WormholeOpened opened:
                    if (Core != null) Claim(opened.Wormhole);
                    break;
                case LevelCompleted _:
                    LevelCompleted?.Invoke(this);
                    if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
                    break;
            }
        }
    }
}
