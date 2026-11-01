using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    public class VulcanWeaponTests
    {
        const float Step = 1f / VulcanWeapon.StepsPerSecond;

        PlayerShipController ship;
        VulcanWeapon vulcan;
        List<VulcanSound> sounds;
        List<Bullet> fired;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Player");
            ship = go.AddComponent<PlayerShipController>();
            ship.ResetState(); // Awake doesn't run outside Play Mode
            vulcan = go.AddComponent<VulcanWeapon>();
            vulcan.ResetState();
            sounds = new List<VulcanSound>();
            fired = new List<Bullet>();
            vulcan.SoundPlayed += sounds.Add;
            vulcan.Fired += fired.Add;
            ship.Land(); // the vulcan is a ground weapon: fire_primary's state_idle branch
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var b in Object.FindObjectsByType<Bullet>())
                Object.DestroyImmediate(b.gameObject);
            foreach (var w in Object.FindObjectsByType<ShieldWall>())
                Object.DestroyImmediate(w.gameObject);
            if (ship != null) Object.DestroyImmediate(ship.gameObject);
        }

        /// <summary>Ticks one GML step at a time and returns how many bullets left the barrel.</summary>
        int RunSteps(int steps)
        {
            int before = fired.Count;
            for (int i = 0; i < steps; i++) vulcan.Tick(Step);
            return fired.Count - before;
        }

        [Test]
        public void HoldingTheTriggerFiresABulletAlongTheAim()
        {
            ship.SetAimDirection(0f);
            vulcan.PressTrigger();
            vulcan.Tick(Step);

            Assert.AreEqual(1, fired.Count);
            var bullet = fired[0];
            Assert.AreSame(ship.gameObject, bullet.Owner);
            Assert.AreEqual(0f, bullet.Direction, 0.001f);
            Assert.AreEqual(Gml.Px(8f) * Gml.StepsPerSecond, bullet.Velocity.x, 0.001f, "bullet.speed = 8 px a step (U.HARE-T88)");
            Assert.AreEqual(0f, bullet.Velocity.y, 0.001f);
            Assert.Greater(bullet.transform.position.x, ship.transform.position.x, "spawns at the muzzle, ahead of the ship");
            Assert.AreEqual(149, vulcan.Ammo, "subtract_ground_ammo: 1 round per shot");
            Assert.IsFalse(vulcan.CanShoot);
        }

        [Test]
        public void SpinUpPlaysOnceThenTheLoopRunsUntilRelease()
        {
            vulcan.PressTrigger();
            RunSteps(200);

            Assert.IsTrue(vulcan.IsSpinning);
            Assert.IsTrue(vulcan.IsLoopPlaying);
            CollectionAssert.AreEqual(new[] { VulcanSound.SpinUp, VulcanSound.LoopStart }, sounds);

            vulcan.ReleaseTrigger();

            Assert.IsFalse(vulcan.IsSpinning);
            Assert.IsFalse(vulcan.IsLoopPlaying);
            CollectionAssert.AreEqual(
                new[] { VulcanSound.SpinUp, VulcanSound.LoopStart, VulcanSound.LoopStop, VulcanSound.SpinDown }, sounds);
        }

        [Test]
        public void RunningDryClicksAndSpinsDownInsteadOfFiring()
        {
            vulcan.PressTrigger();
            int shots = RunSteps(150 * 30);

            Assert.AreEqual(150, shots, "one bullet per round, no more");
            Assert.AreEqual(0, vulcan.Ammo);
            Assert.IsFalse(vulcan.IsSpinning);
            Assert.IsFalse(vulcan.IsLoopPlaying);
            Assert.Contains(VulcanSound.EmptyClip, sounds);
            Assert.Contains(VulcanSound.SpinDown, sounds);
        }

        [Test]
        public void DyingMidBurstSpinsTheBarrelDown()
        {
            vulcan.PressTrigger();
            RunSteps(40);
            Assert.IsTrue(vulcan.IsSpinning);

            ship.Die();
            vulcan.Tick(Step);

            Assert.IsFalse(vulcan.IsSpinning);
            Assert.IsFalse(vulcan.IsLoopPlaying);
            Assert.AreEqual(VulcanSound.SpinDown, sounds[sounds.Count - 1]);
        }
    }

    public class BulletTests
    {
        class Target : MonoBehaviour, IProjectileTarget
        {
            public int Hits;
            public bool Consumes = true;
            public bool OnProjectileHit(Bullet bullet) { Hits += bullet.Damage; return Consumes; }
        }

        readonly List<GameObject> spawned = new List<GameObject>();

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            return go;
        }

        Bullet NewBullet(GameObject owner, float direction = 0f, float lifetime = 3f)
        {
            var bullet = Spawn("Bullet").AddComponent<Bullet>();
            bullet.Init(owner, direction, 8f, 1, lifetime, 0.05f);
            return bullet;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        [Test]
        public void BulletExpiresAfterItsLifetime()
        {
            var bullet = NewBullet(null, lifetime: 1f);
            bullet.Step(0.5f);
            Assert.IsTrue(bullet != null);
            bullet.Step(0.6f);
            Assert.IsTrue(bullet == null, "destroyed once out of play");
        }

        [Test]
        public void BulletDamagesATargetAndIsConsumed()
        {
            var target = Spawn("Enemy").AddComponent<Target>();
            var hitbox = target.gameObject.AddComponent<CircleCollider2D>();
            var bullet = NewBullet(null);

            Assert.IsTrue(bullet.TryHit(hitbox));
            Assert.AreEqual(1, target.Hits);
            Assert.IsTrue(bullet == null);
        }

        [Test]
        public void BulletIgnoresItsOwner()
        {
            var owner = Spawn("Owner");
            owner.AddComponent<Target>();
            var hitbox = owner.AddComponent<CircleCollider2D>();
            var bullet = NewBullet(owner);

            Assert.IsFalse(bullet.TryHit(hitbox));
            Assert.AreEqual(0, owner.GetComponent<Target>().Hits);
            Assert.IsTrue(bullet != null);
        }

        [Test]
        public void BulletBouncesOffAShieldAlongTheNormal()
        {
            var bullet = NewBullet(null, direction: 180f);
            bullet.OnShieldHit(null, Vector2.up);

            Assert.AreEqual(90f, bullet.Direction, 0.001f);
            Assert.AreEqual(0f, bullet.Velocity.x, 0.001f);
            Assert.AreEqual(8f, bullet.Velocity.y, 0.001f);
        }
    }
}
