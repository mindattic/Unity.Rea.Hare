using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Enemies
{
    /// <summary>Core's engine-free types and Unity's, converted at the seam between the rules and what is drawn.</summary>
    public static class CoreConvert
    {
        public static Vector2 ToVector2(this Vec2 v) => new Vector2(v.X, v.Y);
        public static Vec2 ToVec2(this Vector2 v) => new Vec2(v.x, v.y);
        public static Rect ToRect(this RoomBounds r) => Rect.MinMaxRect(r.XMin, r.YMin, r.XMax, r.YMax);
        public static RoomBounds ToBounds(this Rect r) => new RoomBounds(r.xMin, r.yMin, r.xMax, r.yMax);
    }

    /// <summary>Something that plays the events its Core body raises.</summary>
    public interface IEnemyEventSink
    {
        void OnEnemyEvent(EnemyEvent e);
    }

    /// <summary>
    /// The scene's one authority over the enemy world: it owns the Core <see cref="EnemyWorld"/>, runs every command the
    /// views send and plays the events they raise (effects, rounds, new views). Views never change enemy state
    /// themselves. The world is dealt by one seeded random source, so a seed reproduces a run (see <see cref="Seed"/>).
    /// </summary>
    public static class EnemyDirector
    {
        static EnemyWorld world;
        static int seed = Environment.TickCount;
        static readonly Dictionary<int, Object> views = new Dictionary<int, Object>();

        /// <summary>The seed the current world was dealt from; set it (or call <see cref="Reset"/>) before play to repeat a run.</summary>
        public static int Seed => seed;

        public static EnemyWorld World => world ?? (world = new EnemyWorld(new SeededRandom(seed)));

        /// <summary>Starts a fresh world (and optionally a new seed): nothing survives, every view is forgotten.</summary>
        public static void Reset(int? newSeed = null)
        {
            if (newSeed.HasValue) seed = newSeed.Value;
            world = null;
            views.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad() => Reset();

        /// <summary>Runs a command and plays its events. <paramref name="adopter"/> is the view of whatever the command makes.</summary>
        public static EnemyResult Execute(EnemyCommand command, Object adopter = null)
        {
            var w = World;
            if (w.Paused != Gml.Paused) w.Execute(new SetPaused(Gml.Paused));
            if (command is TickVortex) Prune(); // a vortex counts live enemies, so views destroyed from outside must not
            var result = w.Execute(command);
            if (adopter != null) Adopt(result, adopter);
            Play(result);
            return result;
        }

        static void Adopt(EnemyResult result, Object view)
        {
            if (result.Body != null) views[result.Body.Id] = view;
            else if (result.Vortex != null) views[result.Vortex.Id] = view;
            else if (result.Insect != null) views[result.Insect.Id] = view;
            else if (result.Emitter != null) views[result.Emitter.Id] = view;
        }

        /// <summary>Registers the view of a Core body (an enemy, an insect, a vortex or a wormhole emitter).</summary>
        public static void Register(int id, Object view) => views[id] = view;

        public static void Forget(int id) => views.Remove(id);

        /// <summary>The live view of a Core id, or null.</summary>
        public static T ViewOf<T>(int id) where T : Object =>
            views.TryGetValue(id, out var v) && v != null ? v as T : null;

        /// <summary>GML instance_number(instance): live instances of that object (views destroyed from outside are taken out first).</summary>
        public static int Count(EnemyKind kind)
        {
            Prune();
            return World.Count(kind);
        }

        /// <summary>Takes out of the world every body whose view was destroyed from outside (no death, no events).</summary>
        static void Prune()
        {
            var w = World;
            List<int> gone = null;
            foreach (var body in w.Enemies)
            {
                if (views.TryGetValue(body.Id, out var v) && v == null) (gone ?? (gone = new List<int>())).Add(body.Id);
            }
            if (gone != null)
                foreach (var id in gone)
                {
                    w.Execute(new RemoveBody(id));
                    views.Remove(id);
                }
        }

        /// <summary>Where the player's and the enemies' hit rules meet: hurts <paramref name="health"/>'s ship through Core.</summary>
        public static bool Hurt(PlayerHealth health, int? enemyId)
        {
            var seat = PlayerSeat.Of(health.gameObject);
            var result = Execute(new HurtPlayer(seat.World, seat.Id, enemyId));
            if (result.PlayerResult.HasValue) seat.Play(result.PlayerResult.Value.Events);
            return result.Accepted;
        }

        // ---- Playing events ----

        /// <summary>Plays every event of a result, in order.</summary>
        public static void Play(EnemyResult result)
        {
            for (int i = 0; i < result.Events.Count; i++) PlayEvent(result.Events[i]);
        }

        static IEnemyEventSink SinkOf(int id) =>
            views.TryGetValue(id, out var v) && v != null ? v as IEnemyEventSink : null;

        static void PlayEvent(EnemyEvent e)
        {
            switch (e)
            {
                case EnemySpawned spawned:
                    if (!views.ContainsKey(spawned.Body.Id)) EnemyViews.Create(spawned.Body);
                    return;
                case VortexSpawned vortex:
                    if (!views.ContainsKey(vortex.Vortex.Id)) EnemyViews.Create(vortex.Vortex);
                    return;
                case RoundFired fired:
                {
                    var owner = ViewOf<Component>(fired.SourceId);
                    var bullet = EnemyRounds.Spawn(owner != null ? owner.gameObject : null, fired.Origin.ToVector2(), fired.Direction, fired.SpeedPxPerStep);
                    (SinkOf(fired.SourceId) as EnemyBodyView)?.OnRound(fired, bullet);
                    return;
                }
                case TinyExplosionRaised tiny: TinyExplosion.Spawn(tiny.At.ToVector2()); return;
                case DeathBurstRaised burst: DeathEffects.Burst(burst.At.ToVector2(), burst.Size); return;
                case GrenadeExplosionRaised grenade: GrenadeExplosion.Spawn(grenade.At.ToVector2()); return;
                case RocketExplosionRaised blast:
                    new GameObject("RocketExplosion").AddComponent<RocketExplosion>().Init(blast.At.ToVector2());
                    return;
                case DebrisRaised debris: FadingDebris.Spawn(debris.Spec); return;
                case LevelCompleted _:
                    // set_level_complete: level_complete = true, obj_level_results, paused = true (the results ticket shows the results).
                    Gml.Paused = true;
                    break;
            }
            SinkOf(e.SourceId)?.OnEnemyEvent(e);
            if (e is EnemyKilled) views.Remove(e.SourceId);
        }
    }

    /// <summary>Makes the view for a Core body that has none yet (a part made with its owner, an enemy a vortex released).</summary>
    public static class EnemyViews
    {
        public static EnemyBodyView Create(EnemyBody body)
        {
            var go = new GameObject(NameOf(body));
            go.transform.position = new Vector3(body.Position.X, body.Position.Y, 0f);
            EnemyBodyView view;
            switch (body)
            {
                case DaggerEnemy _: view = go.AddComponent<Dagger>(); break;
                case SawbladeEnemy _: view = go.AddComponent<Sawblade>(); break;
                case MimicEnemy _: view = go.AddComponent<Mimic>(); break;
                case MimicIllusionBody _: view = go.AddComponent<MimicIllusion>(); break;
                case WraithEnemy _: view = go.AddComponent<Wraith>(); break;
                case MeatballEnemy _: view = go.AddComponent<Meatball>(); break;
                case PhalanxEnemy _: view = go.AddComponent<Phalanx>(); break;
                case PhalanxShieldBody _: view = go.AddComponent<PhalanxShield>(); break;
                case WormEnemy _: view = go.AddComponent<Worm>(); break;
                case WormSegmentBody _: view = go.AddComponent<WormSegment>(); break;
                case TrilobyteEnemy _: view = go.AddComponent<Trilobyte>(); break;
                case EnemyMineBody _: view = go.AddComponent<EnemyMine>(); break;
                default: view = go.AddComponent<Hydra>(); break;
            }
            view.Attach(body, null);
            if (body is EnemyMineBody mine && mine.Owner != null)
                ViewOfOwner(mine.Owner.Id)?.OnMineLaid((EnemyMine)view);
            return view;
        }

        static Trilobyte ViewOfOwner(int id) => EnemyDirector.ViewOf<Trilobyte>(id);

        public static Vortex Create(VortexBody vortex)
        {
            var go = new GameObject(vortex is WormholeBody ? "Wormhole" : vortex.Instance + "Emitter");
            Vortex view = vortex is WormholeBody ? (Vortex)go.AddComponent<Wormhole>() : go.AddComponent<EnemyEmitter>();
            view.Attach(vortex, null);
            return view;
        }

        static string NameOf(EnemyBody body)
        {
            switch (body.Part)
            {
                case BodyPart.Shield: return "PhalanxShield";
                case BodyPart.Illusion: return "MimicIllusion";
                case BodyPart.Segment: return "WormSegment";
                case BodyPart.Mine: return "EnemyMine";
                default: return body.Kind.ToString();
            }
        }
    }
}
