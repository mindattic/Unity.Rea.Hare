using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Hare.Enemies;

namespace Unity.Hare.Tests.Core
{
    /// <summary>One enemy world on a seeded random, with the commands the views send as one-liners and every event kept.</summary>
    internal sealed class EnemyRig
    {
        public const float Step = 1f / GameClock.StepsPerSecond;

        public readonly EnemyWorld World;
        public readonly List<EnemyEvent> Events = new List<EnemyEvent>();

        public EnemyRig(int seed = 1) => World = new EnemyWorld(new SeededRandom(seed));

        public RoomBounds Room => World.Room;

        public EnemyResult Do(EnemyCommand command)
        {
            var result = World.Execute(command);
            Events.AddRange(result.Events);
            return result;
        }

        public T Spawn<T>(EnemyKind kind, Vec2 at, EnemyOptions options = null) where T : EnemyBody =>
            (T)Do(new SpawnEnemy(kind, at, null, options)).Body;

        /// <summary>A ship at <paramref name="at"/> (GML's player), moving at <paramref name="velocityPxPerStep"/>.</summary>
        public void Ship(Vec2 at, Vec2 velocityPxPerStep = default, int seat = 1) =>
            Do(new SetTarget(new PlayerId(seat), at, velocityPxPerStep));

        /// <summary>Ticks one enemy a GML step at a time; stops when it is gone.</summary>
        public void Run(EnemyBody body, int steps)
        {
            for (int i = 0; i < steps && !body.IsDead; i++) Do(new TickEnemy(body.Id, Step));
        }

        public IEnumerable<T> Seen<T>() where T : EnemyEvent => Events.OfType<T>();

        public EnemyKill Kill(EnemyBody body) => Seen<EnemyKilled>().Single(k => k.SourceId == body.Id).Kill;

        public bool Died(EnemyBody body) => Seen<EnemyKilled>().Any(k => k.SourceId == body.Id);

        public EnemyResult ShipBullet(EnemyBody target, Vec2? at = null, float direction = 0f, float speed = 1f, int key = 11) =>
            Do(new StrikeBullet(target.Id, new BulletShot(false, true, at ?? target.Position, direction, speed, key)));

        public EnemyResult EnemyBullet(EnemyBody target, Vec2? at = null, float direction = 0f, int key = 12) =>
            Do(new StrikeBullet(target.Id, new BulletShot(true, false, at ?? target.Position, direction, 1f, key)));

        public EnemyResult Rocket(EnemyBody target, Vec2? at = null) => Do(new StrikeRocket(target.Id, at ?? target.Position));

        public EnemyResult Beam(EnemyBody target, BeamKind kind) => Do(new StrikeBeam(target.Id, kind));

        public static Vec2 Px(float x, float y) => new Vec2(GameClock.Px(x), GameClock.Px(y));
    }
}
