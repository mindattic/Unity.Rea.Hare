using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T60: the ship's projectiles honour the global paused flag, as obj_bullet's, obj_rocket's and obj_mine's
    /// <c>if(!paused) { ... } else speed = 0</c> do: nothing moves, ages or homes while paused, and each carries on
    /// from where it stopped once play resumes (a rocket still ignites and a mine still arms, docs/BIBLE.md D22).
    /// </summary>
    public class WeaponPauseTests
    {
        const float Step = 1f / 120f;

        [SetUp]
        public void SetUp() => Gml.Paused = false;

        [TearDown]
        public void TearDown()
        {
            Gml.Paused = false;
            foreach (var c in Object.FindObjectsByType<Bullet>()) Object.DestroyImmediate(c.gameObject);
            foreach (var c in Object.FindObjectsByType<Rocket>()) Object.DestroyImmediate(c.gameObject);
            foreach (var c in Object.FindObjectsByType<Mine>()) Object.DestroyImmediate(c.gameObject);
            foreach (var c in Object.FindObjectsByType<RocketExplosion>()) Object.DestroyImmediate(c.gameObject);
        }

        static void Run(System.Action<float> tick, int steps)
        {
            for (int i = 0; i < steps; i++) tick(Step);
        }

        [Test]
        public void ABulletHoldsStillAndDoesNotAgeWhilePaused()
        {
            var bullet = new GameObject("Bullet").AddComponent<Bullet>();
            bullet.Init(null, 0f, 10f, 1, 1f, 0.05f);
            Run(bullet.Step, 10);
            float age = bullet.Age;
            Assert.That(bullet.Velocity.x, Is.EqualTo(10f).Within(1e-4f));

            Gml.Paused = true;
            Run(bullet.Step, 240); // two seconds: past its one-second lifetime, were it counting
            Assert.That(bullet != null, Is.True, "not aged out while paused");
            Assert.That(bullet.Velocity, Is.EqualTo(Vector2.zero), "speed = 0");
            Assert.That(bullet.Age, Is.EqualTo(age), "age += 1 is inside if(!paused)");

            Gml.Paused = false;
            bullet.Step(Step);
            Assert.That(bullet.Velocity.x, Is.EqualTo(10f).Within(1e-4f), "speed = Speed: on at the speed it had");
        }

        [Test]
        public void ARocketPausedAcrossItsIgnitionIgnitesOnceResumed()
        {
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);
            Run(rocket.Tick, 40);
            Assert.IsFalse(rocket.IsActive);

            Gml.Paused = true;
            Run(rocket.Tick, 120);
            Assert.IsFalse(rocket.IsActive, "no ignition while paused");
            Assert.That(rocket.Age, Is.EqualTo(40 * Step).Within(1e-5f), "its clock stops");

            Gml.Paused = false;
            Run(rocket.Tick, 11);
            Assert.IsTrue(rocket.IsActive, "the 50-step ignition, counted in unpaused steps");
            Run(rocket.Tick, 20);
            Vector2 at = rocket.Position;
            float speed = rocket.SpeedPxPerStep;

            Gml.Paused = true;
            Run(rocket.Tick, 60);
            Assert.That(rocket.Position, Is.EqualTo(at), "holds still");
            Assert.That(rocket.SpeedPxPerStep, Is.EqualTo(speed), "and does not speed up");
        }

        [Test]
        public void AMineDoesNotDriftArmOrDetonateWhilePaused()
        {
            var mine = new GameObject("Mine").AddComponent<Mine>();
            mine.Init(null, Vector2.zero, facing: 0f, detonateSeconds: 8);
            Run(mine.Tick, 60);
            Vector2 at = mine.transform.position;
            Assert.Greater(at.x, 0f, "drifting at 1 px a step");

            Gml.Paused = true;
            Run(mine.Tick, 10 * 120); // past both alarms, were they counting
            Assert.That(mine != null && !mine.IsGone, Is.True, "the fuse waits");
            Assert.IsFalse(mine.IsActive, "not armed while paused");
            Assert.That((Vector2)mine.transform.position, Is.EqualTo(at), "speed = 0");

            Gml.Paused = false;
            Run(mine.Tick, 61);
            Assert.IsTrue(mine.IsActive, "arms once room_speed unpaused steps have passed");
        }
    }
}
