using System;
using System.Collections.Generic;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>Values that pin the random ones in an enemy's GML Create (tests and level scripts); null leaves it random.</summary>
    public sealed class EnemyOptions
    {
        public float? Direction;
        public float? ImageAngle;
        public float? SpeedPxPerStep;
        public float? Spin;
        public int? Arms;
        public int? RotationDirection;
        public int? SegmentCount;
        public int? FuseSeconds;
    }

    /// <summary>What an enemy command did: whether it took effect, what it made or struck, and the events it raised in order.</summary>
    public sealed class EnemyResult
    {
        static readonly IReadOnlyList<EnemyEvent> None = Array.Empty<EnemyEvent>();

        internal EnemyResult(IReadOnlyList<EnemyEvent> events) => Events = events ?? None;

        /// <summary>For a strike: the shot was spent (a bullet) or the rocket explodes here. For a spawn or removal: it happened.</summary>
        public bool Accepted { get; internal set; }
        public IReadOnlyList<EnemyEvent> Events { get; }
        /// <summary>The enemy a spawn made, or the one a strike hit.</summary>
        public EnemyBody Body { get; internal set; }
        public InsectBody Insect { get; internal set; }
        public VortexBody Vortex { get; internal set; }
        public WormholeEmitterBody Emitter { get; internal set; }
        /// <summary>A phalanx shield bounced the shot to this heading, degrees.</summary>
        public float? Deflection { get; internal set; }
        /// <summary>What a <see cref="HurtPlayer"/> command did to the ship, with the events its seat plays.</summary>
        public CommandResult? PlayerResult { get; internal set; }
        /// <summary>What a <see cref="PlaceComposition"/> command created.</summary>
        public CompositionPlacement Placement { get; internal set; }

        public T Find<T>() where T : EnemyEvent
        {
            foreach (var e in Events)
                if (e is T match) return match;
            return null;
        }

        public bool Has<T>() where T : EnemyEvent => Find<T>() != null;
    }

    /// <summary>
    /// The state of every enemy, insect, vortex and wormhole emitter in a room. One authority holds it; commands go
    /// in and the events they raise come back; presentation reads state and plays the events. Every random draw comes
    /// from the <see cref="IRandomSource"/> the world is built with, so a seed reproduces a run.
    /// </summary>
    public sealed class EnemyWorld
    {
        readonly List<EnemyBody> live = new List<EnemyBody>();
        readonly Dictionary<int, EnemyBody> bodies = new Dictionary<int, EnemyBody>();
        readonly Dictionary<int, InsectBody> insects = new Dictionary<int, InsectBody>();
        readonly Dictionary<int, VortexBody> vortices = new Dictionary<int, VortexBody>();
        readonly Dictionary<int, WormholeEmitterBody> emitters = new Dictionary<int, WormholeEmitterBody>();
        readonly Dictionary<int, PlayerTarget> targets = new Dictionary<int, PlayerTarget>();
        int nextId;

        public EnemyWorld(IRandomSource random, RoomBounds? room = null)
        {
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Room = room ?? RoomBounds.Gamescreen;
        }

        public IRandomSource Random { get; }
        public RoomBounds Room { get; }

        /// <summary>GML globals paused: set while a level is complete or the menu is up.</summary>
        public bool Paused { get; internal set; }

        public IReadOnlyList<EnemyBody> Enemies => live;
        public IEnumerable<InsectBody> Insects => insects.Values;
        public IEnumerable<VortexBody> Vortices => vortices.Values;

        internal EnemyEventSink Sink { get; private set; } = new EnemyEventSink();

        public EnemyBody Get(int id) => bodies.TryGetValue(id, out var b) ? b : null;

        /// <summary>GML instance_number(instance): live instances of that object (a worm counts its head only, parts never count).</summary>
        public int Count(EnemyKind kind)
        {
            int n = 0;
            foreach (var b in live)
                if (b.Kind == kind && b.Part == BodyPart.Main) n++;
            return n;
        }

        /// <summary>GML instance_nearest(x, y, par_enemy) among bodies that are par_enemy.</summary>
        public EnemyBody NearestEnemy(Vec2 from)
        {
            EnemyBody best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (var b in live)
            {
                if (!b.IsParEnemy) continue;
                float d = (b.Position - from).SqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = b; }
            }
            return best;
        }

        /// <summary>The seat whose ship is nearest <paramref name="from"/>, or null with none seated (GML's player, one per seat).</summary>
        public PlayerTarget NearestTarget(Vec2 from)
        {
            PlayerTarget best = null;
            float bestSqr = float.PositiveInfinity;
            foreach (var t in targets.Values)
            {
                float d = (t.Position - from).SqrMagnitude;
                if (d < bestSqr || (d == bestSqr && best != null && t.Id.Value < best.Id.Value)) { bestSqr = d; best = t; }
            }
            return best;
        }

        public PlayerTarget TargetOf(PlayerId id) => targets.TryGetValue(id.Value, out var t) ? t : null;

        /// <summary>Runs a command and returns what it did.</summary>
        public EnemyResult Execute(EnemyCommand command)
        {
            Sink = new EnemyEventSink();
            var result = new EnemyResult(Sink.Events);
            result.Accepted = command.Apply(this, result);
            return result;
        }

        // ---- Internals the commands use ----

        internal int NewId() => ++nextId;

        internal EnemyBody Spawn(EnemyKind kind, Vec2 position, RoomBounds? room, EnemyOptions options)
        {
            EnemyBody body;
            switch (kind)
            {
                case EnemyKind.Dagger: body = new DaggerEnemy(); break;
                case EnemyKind.Sawblade: body = new SawbladeEnemy(); break;
                case EnemyKind.Mimic: body = new MimicEnemy(); break;
                case EnemyKind.Wraith: body = new WraithEnemy(); break;
                case EnemyKind.Meatball: body = new MeatballEnemy(); break;
                case EnemyKind.Phalanx: body = new PhalanxEnemy(); break;
                case EnemyKind.Worm: body = new WormEnemy(); break;
                case EnemyKind.Trilobyte: body = new TrilobyteEnemy(); break;
                default: body = new HydraEnemy(); break;
            }
            Add(body, position, room ?? Room, options ?? new EnemyOptions());
            return body;
        }

        /// <summary>A part made with its owner (a shield, an illusion, a segment, a laid mine): it starts where the owner is.</summary>
        internal void AddChild(EnemyBody parent, EnemyBody child) => Add(child, parent.Position, parent.Room, new EnemyOptions());

        /// <summary>A body made outside any owner's Create (a mine laid on its own).</summary>
        internal void AddAt(EnemyBody body, Vec2 position, RoomBounds? room) => Add(body, position, room ?? Room, new EnemyOptions());

        void Add(EnemyBody body, Vec2 position, RoomBounds room, EnemyOptions options)
        {
            body.Attach(this, NewId(), position, room);
            live.Add(body);
            bodies.Add(body.Id, body);
            Sink.Emit(body.Id, new EnemySpawned { Body = body });
            body.Create(options);
        }

        internal void Retire(EnemyBody body)
        {
            live.Remove(body);
            bodies.Remove(body.Id);
        }

        internal InsectBody AddInsect(InsectKind kind, RoomBounds? room, Vec2? position)
        {
            InsectBody insect;
            switch (kind)
            {
                case InsectKind.Butterfly: insect = new ButterflyBody(); break;
                case InsectKind.Firefly: insect = new FireflyBody(); break;
                default: insect = new FlyBody(); break;
            }
            insect.Attach(Random, NewId(), room ?? Room);
            insect.Create(position);
            insects.Add(insect.Id, insect);
            return insect;
        }

        internal InsectBody GetInsect(int id) => insects.TryGetValue(id, out var i) ? i : null;
        internal bool RemoveInsect(int id) => insects.Remove(id);

        internal T AddVortex<T>(T vortex) where T : VortexBody
        {
            vortex.Attach(this, NewId());
            vortices.Add(vortex.Id, vortex);
            Sink.Emit(vortex.Id, new VortexSpawned { Vortex = vortex });
            return vortex;
        }

        internal VortexBody GetVortex(int id) => vortices.TryGetValue(id, out var v) ? v : null;
        internal void RemoveVortex(VortexBody vortex) => vortices.Remove(vortex.Id);

        internal WormholeEmitterBody AddWormholeEmitter(WormholeEmitterBody emitter)
        {
            emitter.Attach(this, NewId());
            emitters.Add(emitter.Id, emitter);
            return emitter;
        }

        internal WormholeEmitterBody GetWormholeEmitter(int id) => emitters.TryGetValue(id, out var e) ? e : null;
        internal void RemoveWormholeEmitter(WormholeEmitterBody emitter) => emitters.Remove(emitter.Id);

        internal void SetTarget(PlayerId id, Vec2 position, Vec2 velocityPxPerStep)
        {
            if (!targets.TryGetValue(id.Value, out var t)) targets[id.Value] = t = new PlayerTarget(id);
            t.Position = position;
            t.VelocityPxPerStep = velocityPxPerStep;
        }

        internal bool ClearTarget(PlayerId id) => targets.Remove(id.Value);

        internal IEnumerable<EnemyBody> Snapshot() => live.ToArray();
        internal IEnumerable<InsectBody> InsectSnapshot() => new List<InsectBody>(insects.Values);
        internal IEnumerable<VortexBody> VortexSnapshot() => new List<VortexBody>(vortices.Values);
        internal IEnumerable<WormholeEmitterBody> EmitterSnapshot() => new List<WormholeEmitterBody>(emitters.Values);
    }
}
