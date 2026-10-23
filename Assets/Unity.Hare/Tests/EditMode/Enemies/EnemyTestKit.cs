using System.Linq;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>Shared fixtures for the UH-T14 set-piece enemy tests.</summary>
    static class EnemyTestKit
    {
        public const float Step = 1f / 120f;

        public static void DestroyAll<T>() where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        public static void Cleanup()
        {
            DestroyAll<SetPieceEnemy>();
            DestroyAll<EnemyMine>();
            DestroyAll<Bullet>();
            DestroyAll<Rocket>();
            DestroyAll<RocketExplosion>();
            DestroyAll<GrenadeExplosion>();
            DestroyAll<FadingDebris>();
            DestroyAll<TinyExplosion>();
            DestroyAll<GmlEffectBurst>();
            DestroyAll<DeathParticle>();
            DestroyAll<PlayerShipController>();
        }

        public static PlayerShipController Ship(Vector2 position)
        {
            var ship = new GameObject("Player").AddComponent<PlayerShipController>();
            ship.transform.position = position;
            return ship;
        }

        public static T Spawn<T>(string name = null) where T : SetPieceEnemy =>
            new GameObject(name ?? typeof(T).Name).AddComponent<T>();

        /// <summary>One of the ship's own rounds, heading <paramref name="direction"/>.</summary>
        public static Bullet ShipRound(PlayerShipController ship, Vector2 at, float direction = 0f)
        {
            var bullet = new GameObject("Bullet").AddComponent<Bullet>();
            bullet.transform.position = at;
            bullet.Init(ship.gameObject, direction, 1f, 1, 10f, 0.05f);
            return bullet;
        }

        public static Bullet EnemyRound(Vector2 at, float direction = 0f) =>
            EnemyRounds.Spawn(null, at, direction, 2f);

        public static Rocket FireRocket(Vector2 at, float direction = 0f)
        {
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(at, direction);
            return rocket;
        }

        public static void Steps(SetPieceEnemy enemy, int steps)
        {
            for (int i = 0; i < steps && enemy != null && !enemy.IsDead; i++) enemy.Tick(Step);
        }

        public static int Count<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;

        public static int EnemyRoundCount() =>
            Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Count(b => b.FromEnemy);

        public static int DebrisCount(DebrisKind kind) =>
            Object.FindObjectsByType<FadingDebris>(FindObjectsSortMode.None).Count(d => d.Kind == kind);

        public static Collider2D Hitbox(Component c) => c.GetComponent<Collider2D>();
    }
}
