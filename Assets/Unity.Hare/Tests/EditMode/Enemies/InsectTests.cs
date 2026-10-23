using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T13: obj_butterfly/obj_firefly/obj_fly, their ai_* scripts and emitters.</summary>
    public class InsectTests
    {
        const float Step = 1f / 120f;
        static readonly Rect Room = Gml.Room;

        [SetUp]
        public void SetUp() => Random.InitState(1234);

        [TearDown]
        public void TearDown()
        {
            foreach (var c in Object.FindObjectsByType<Insect>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        static T Spawn<T>(Vector2 position) where T : Insect
        {
            var insect = new GameObject(typeof(T).Name).AddComponent<T>();
            insect.Init(Room, position);
            return insect;
        }

        // Points inside the room (world units), named by where they sit in GML terms.
        static Vector2 LowerMiddle => new Vector2(Room.center.x, Room.center.y - 3f);
        static Vector2 UpperMiddle => new Vector2(Room.center.x, Room.center.y + 3f);
        static Vector2 LowerLeftWall => new Vector2(Room.xMin + Gml.Px(5f), Room.center.y - 3f);
        static Vector2 LowerRightWall => new Vector2(Room.xMax - Gml.Px(5f), Room.center.y - 3f);
        static Vector2 NearFloor => new Vector2(Room.center.x, Room.yMin + Gml.Px(60f));

        [Test]
        public void CreatePlacesInsectsInTheLowerHalfMovingOneByOne()
        {
            for (int i = 0; i < 50; i++)
            {
                var fly = (Fly)InsectEmitter.Spawn(InsectKind.Fly, Room);
                Vector2 p = fly.transform.position;
                Assert.That(p.x, Is.InRange(Room.xMin, Room.xMax));
                Assert.That(p.y, Is.InRange(Room.yMin, Room.center.y), "y = random_range(room_height/2, room_height)");
                Assert.AreEqual(1f, fly.DX);
                Assert.AreEqual(1f, fly.DY);
                Assert.That(fly.ThinkCountdownSteps, Is.InRange(5f, 30f), "alarm[0] = irandom_range(5,30)");
            }
        }

        [Test]
        public void StepMovesByDxDyPixelsPerStepWithGmlYDown()
        {
            var fly = Spawn<Fly>(LowerMiddle);
            Vector2 start = fly.transform.position;
            fly.Tick(Step); // dX = dY = 1 until the first think
            Vector2 moved = (Vector2)fly.transform.position - start;
            Assert.AreEqual(Gml.Px(1f), moved.x, 1e-5f);
            Assert.AreEqual(-Gml.Px(1f), moved.y, 1e-5f, "GML y += 1 moves down the screen");
        }

        [Test]
        public void AlarmRunsTheAiScriptAndReArmsWithinItsRange()
        {
            var butterfly = Spawn<Butterfly>(LowerMiddle);
            int thinks = 0;
            float dx = butterfly.DX;
            for (int i = 0; i < 600; i++)
            {
                butterfly.Tick(Step);
                if (butterfly.DX != dx) { thinks++; dx = butterfly.DX; }
                Assert.That(butterfly.ThinkCountdownSteps, Is.LessThanOrEqualTo(Butterfly.ThinkMaxStepsValue));
                butterfly.transform.position = LowerMiddle; // keep it clear of the bounds rules
            }
            // 600 steps at one think every 5..60 steps.
            Assert.That(thinks, Is.InRange(600 / 61, 600 / 5));
        }

        [Test]
        public void ButterflyStaysInTheLowerHalfAndFacesItsHeading()
        {
            var butterfly = Spawn<Butterfly>(LowerMiddle);
            for (int i = 0; i < 100; i++)
            {
                butterfly.Think();
                Assert.That(butterfly.DX, Is.InRange(-2f, 2f));
                Assert.That(butterfly.DY, Is.InRange(-2f, 2f));
                Assert.AreEqual(butterfly.DX > 0f, butterfly.FacingRight);
                Assert.AreEqual(butterfly.FacingRight ? 1f : -1f, Mathf.Sign(butterfly.transform.localScale.x));
            }

            butterfly.transform.position = UpperMiddle;
            butterfly.Think();
            Assert.AreEqual(2f, butterfly.DY, "upper half: dY = 2 (down)");

            butterfly.transform.position = NearFloor;
            butterfly.Think();
            Assert.AreEqual(-2f, butterfly.DY, "within 120 px of the floor: dY = -2 (up)");

            butterfly.transform.position = LowerLeftWall;
            butterfly.Think();
            Assert.AreEqual(2f, butterfly.DX);
            Assert.IsTrue(butterfly.FacingRight);

            butterfly.transform.position = LowerRightWall;
            butterfly.Think();
            Assert.AreEqual(-2f, butterfly.DX);
            Assert.IsFalse(butterfly.FacingRight);
        }

        [Test]
        public void FireflyDriftsSlowlyVerticallyAndHasNoFloorRule()
        {
            var firefly = Spawn<Firefly>(LowerMiddle);
            for (int i = 0; i < 100; i++)
            {
                firefly.Think();
                Assert.That(firefly.DX, Is.InRange(-3f, 3f));
                Assert.That(firefly.DY, Is.InRange(-1f, 1f));
            }

            firefly.transform.position = UpperMiddle;
            firefly.Think();
            Assert.AreEqual(1f, firefly.DY);

            firefly.transform.position = LowerLeftWall;
            firefly.Think();
            Assert.AreEqual(3f, firefly.DX);
            firefly.transform.position = LowerRightWall;
            firefly.Think();
            Assert.AreEqual(-3f, firefly.DX);

            bool sawUpward = false, sawDownward = false;
            for (int i = 0; i < 200; i++)
            {
                firefly.transform.position = NearFloor;
                firefly.Think();
                sawUpward |= firefly.DY < 0f;
                sawDownward |= firefly.DY > 0f;
            }
            Assert.IsTrue(sawUpward && sawDownward, "ai_firefly never forces dY near the floor");
        }

        [Test]
        public void FireflyBlinksOnAboutOneThinkInFive()
        {
            var firefly = Spawn<Firefly>(LowerMiddle);
            Assert.IsFalse(firefly.IsDark);
            int toggles = 0;
            bool dark = firefly.IsDark;
            for (int i = 0; i < 2000; i++)
            {
                firefly.transform.position = LowerMiddle;
                firefly.Think();
                if (firefly.IsDark != dark) { toggles++; dark = firefly.IsDark; }
            }
            Assert.That(toggles, Is.InRange(300, 500), "irandom(4) == 4");
        }

        [Test]
        public void FlyDartsFastestAndSinksAtOnePixelInTheUpperHalf()
        {
            var fly = Spawn<Fly>(LowerMiddle);
            for (int i = 0; i < 100; i++)
            {
                fly.Think();
                Assert.That(fly.DX, Is.InRange(-4f, 4f));
                Assert.That(fly.DY, Is.InRange(-2f, 2f));
            }

            fly.transform.position = UpperMiddle;
            fly.Think();
            Assert.AreEqual(1f, fly.DY);

            fly.transform.position = LowerLeftWall;
            fly.Think();
            Assert.AreEqual(4f, fly.DX);
            fly.transform.position = LowerRightWall;
            fly.Think();
            Assert.AreEqual(-4f, fly.DX);
        }

        [Test]
        public void EmittersCreateTheOriginalSwarmSizes()
        {
            var host = new GameObject("Emitter");
            var emitter = host.AddComponent<InsectEmitter>();
            try
            {
                emitter.Kind = InsectKind.Butterfly;
                Assert.AreEqual(100, emitter.Emit(Room).Count);
                emitter.Kind = InsectKind.Firefly;
                Assert.AreEqual(28, emitter.Emit(Room).Count);
                emitter.Kind = InsectKind.Fly;
                Assert.AreEqual(32, emitter.Emit(Room).Count);

                Assert.AreEqual(100, Object.FindObjectsByType<Butterfly>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(28, Object.FindObjectsByType<Firefly>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(32, Object.FindObjectsByType<Fly>(FindObjectsSortMode.None).Length);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void InsectsAreNotEnemiesTheWeaponsCanTarget()
        {
            Spawn<Butterfly>(LowerMiddle);
            Spawn<Firefly>(LowerMiddle);
            Spawn<Fly>(LowerMiddle);
            Assert.IsNull(Rocket.FindNearestEnemy(LowerMiddle), "no parent, no collisions: not par_enemy");
        }
    }
}
