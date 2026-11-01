using System.Collections;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>UH-T40: the enemy world running under Unity's own loop, as in play (the director steps it each physics step).</summary>
    public class EnemyPlayTests
    {
        [SetUp] public void SetUp() { Gml.Paused = false; EnemyDirector.Reset(2468); }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Select(t => t.gameObject).Where(g => g != null && g.name != "EnemyDirector").ToList())
                if (go != null && go.transform.parent == null) Object.Destroy(go);
            Gml.Paused = false;
        }

        static EnemyComposition Composition() => new EnemyComposition()
            .With(EnemyKind.Hydra, 1, 1).With(EnemyKind.Phalanx, 1, 1).With(EnemyKind.Dagger, 1, 1).With(EnemyKind.Worm, 1, 1);

        static string Fingerprint()
        {
            var sb = new StringBuilder();
            foreach (var b in EnemyDirector.World.Enemies) sb.Append(b.Kind).Append(b.Part).Append(b.Position).Append(';');
            foreach (var v in EnemyDirector.World.Vortices) sb.Append(v.Mode).Append(v.Position).Append(';');
            return sb.ToString();
        }

        IEnumerator RunSeeded(int seed, System.Action<string> done)
        {
            EnemyDirector.Reset(seed);
            var ship = new GameObject("Ship").AddComponent<PlayerShipController>();
            ship.transform.position = new Vector3(2f, 1f, 0f);
            Composition().Place(Gml.Room, ship);
            for (int i = 0; i < 400; i++) yield return new WaitForFixedUpdate();
            done(Fingerprint());
            Object.Destroy(ship.gameObject);
        }

        [UnityTest]
        public IEnumerator ASeedPlaysTheSameRunAndAnotherDoesNot()
        {
            string a = null, b = null, c = null;
            yield return RunSeeded(11, f => a = f);
            Teardown();
            yield return RunSeeded(11, f => b = f);
            Teardown();
            yield return RunSeeded(12, f => c = f);
            Assert.IsNotEmpty(a);
            Assert.AreEqual(a, b, "the same seed through Unity's loop repeats the run");
            Assert.AreNotEqual(a, c);
        }

        static void Teardown()
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.parent == null && t.name != "EnemyDirector").ToList())
                if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        [UnityTest]
        public IEnumerator CompositionEmittersKeepTheirOwnBodiesAndEnemiesReachTheirTargetCount()
        {
            var ship = new GameObject("Ship").AddComponent<PlayerShipController>();
            var placed = Composition().Place(Gml.Room, ship);
            var cores = placed.Emitters.Select(e => e.Core).ToList();
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(cores.Count, EnemyDirector.World.Vortices.Count(), "Start did not re-initialise any emitter");
            for (int i = 0; i < cores.Count; i++) Assert.AreSame(cores[i], placed.Emitters[i].Core);
            // The worm emitter's own idle wait (200-500 steps) plus its 120-step open phase can take several
            // hundred physics steps before it releases anything, so poll instead of asserting after one tick.
            for (int i = 0; i < 400 && EnemyDirector.World.Count(EnemyKind.Worm) < 1; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, EnemyDirector.World.Count(EnemyKind.Worm), "the mimic-less composition placed its worm emitter's cap: one worm head at most");
        }

        [UnityTest]
        public IEnumerator EveryEnemyBodyHurtsTheShipOnContact()
        {
            var ship = new GameObject("Ship").AddComponent<PlayerShipController>();
            yield return null;
            foreach (var kind in new[] { EnemyKind.Hydra, EnemyKind.Phalanx, EnemyKind.Sawblade, EnemyKind.Dagger, EnemyKind.Meatball })
            {
                var view = (EnemyBodyView)EnemySpawner.Spawn(kind, new Vector2(100f, 100f), Gml.Room, ship);
                Assert.IsNotNull(view.GetComponent<PlayerDamageSource>(), kind + " is a par_enemy that hurts on contact");
            }
        }
    }
}
