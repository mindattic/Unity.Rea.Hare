using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Helper;
using Unity.Rea.Hare.Pickups;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>
    /// U.HARE-T66: gmlHARE's create_helper and obj_helper run outside Unity: one helper a ship, called at the ship along its
    /// image_angle at 4 px a step, the held arrows' 2 degrees a step, Up and Down's speed steps, Page Down's solo mouse
    /// follow, the return once out of view, the effects on their alarms and Delete's rocket explosion.
    /// </summary>
    public class HelperTests
    {
        const float Step = 1f / GameClock.StepsPerSecond;
        static float Px(float px) => GameClock.Px(px);
        static readonly PlayerId P1 = new PlayerId(1);

        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<int> next = new Queue<int>();
            public ScriptedRandom(params int[] values) { foreach (var v in values) next.Enqueue(v); }
            public void Queue(params int[] values) { foreach (var v in values) next.Enqueue(v); }
            public int IRandom(int min, int max) => next.Count > 0 ? next.Dequeue() : min;
            public float Range(float min, float max) => min;
        }

        ScriptedRandom rng;
        HelperField field;
        readonly List<HelperEvent> events = new List<HelperEvent>();

        [SetUp]
        public void SetUp()
        {
            rng = new ScriptedRandom();
            field = new HelperField(rng, ContentRegistry.Default.Helper);
            events.Clear();
        }

        HelperResult Do(HelperCommand command)
        {
            var result = field.Execute(command);
            events.AddRange(result.Events);
            return result;
        }

        void Run(int steps)
        {
            for (int i = 0; i < steps; i++) Do(new TickHelpers(Step));
        }

        HelperHawk Call(float imageAngle = 0f, Vec2? at = null)
        {
            Assert.IsTrue(Do(new CallHelper(P1, at ?? Vec2.Zero, imageAngle)).Accepted);
            return field.HelperOf(P1);
        }

        static RoomBounds BoxAt(Vec2 at, float halfPx = 10f) =>
            new RoomBounds(at.X - Px(halfPx), at.Y - Px(halfPx), at.X + Px(halfPx), at.Y + Px(halfPx));

        void Owner(Vec2 at, float speedPx = 0f, Vec2? mouse = null) =>
            Do(new SetHelperOwner(P1, new HelperOwner(at, speedPx, BoxAt(at), mouse)));

        [Test]
        public void CreateHelperMakesOneAtTheShipAlongItsImageAngle()
        {
            var h = Call(30f, new Vec2(1f, 2f));
            Assert.IsInstanceOf<HelperCalled>(events.Single(), "Create's sfx_hawk");
            Assert.AreEqual(new Vec2(1f, 2f), h.Position);
            Assert.AreEqual(30f, h.Direction);
            Assert.AreEqual(30f, h.ImageAngle);
            Assert.AreEqual(4f, h.SpeedPx);
            Assert.AreEqual(4f, h.MaxSpeed);
            Assert.AreEqual(2f, h.TurnSpeed);
            Assert.AreEqual(HelperState.Free, h.State);
            Assert.IsFalse(h.Solo);

            Assert.IsFalse(Do(new CallHelper(P1, Vec2.Zero, 90f)).Accepted, "if (!instance_exists(obj_helper))");
            Assert.AreEqual(1, field.Helpers.Count);
            Assert.IsTrue(Do(new CallHelper(new PlayerId(2), Vec2.Zero, 90f)).Accepted, "another ship has its own");
        }

        [Test]
        public void ItFliesFourPxAStepAndRingsOnItsFirstStep()
        {
            var h = Call(0f);
            Run(1);
            Assert.AreEqual(Px(4f), h.Position.X, 1e-6f);
            var ring = events.OfType<HelperRinged>().Single();
            Assert.AreEqual(Vec2.Zero, ring.At, "the ring is where it was before it moved");
            Assert.AreEqual(1, events.OfType<HelperSmoked>().Count(s => !s.Returning), "a gold ef_smoke every step in view");
            Run(9);
            Assert.AreEqual(Px(40f), h.Position.X, 1e-5f);
            Assert.AreEqual(1, events.OfType<HelperRinged>().Count(), "only the first pass");
            Assert.AreEqual(10, events.OfType<HelperSmoked>().Count());
        }

        [Test]
        public void TheHeldArrowsTurnItTwoDegreesAStepAndReleasingStopsTheTurn()
        {
            var h = Call(0f);
            Do(new HoldHelperTurn(P1, counterclockwise: true, clockwise: false));
            Run(10);
            Assert.AreEqual(20f, h.Direction, 1e-4f, "vk_left: direction += turn_speed");
            Assert.AreEqual(20f, h.ImageAngle, 1e-4f);
            Assert.IsTrue(h.Turning);

            Do(new HoldHelperTurn(P1, false, false));
            Assert.IsFalse(h.Turning, "key release: turning = false");
            Do(new HoldHelperTurn(P1, false, true));
            Run(15);
            Assert.AreEqual(350f, h.Direction, 1e-3f, "vk_right: direction -= turn_speed, wrapped as GameMaker wraps it");
            Assert.AreEqual(-10f, h.ImageAngle, 1e-3f, "image_angle is not wrapped");
        }

        [Test]
        public void ItSparksEveryFiveStepsOnlyWhileTurning()
        {
            Call(0f);
            Run(5);
            Assert.IsEmpty(events.OfType<HelperSparked>().ToList(), "not turning");

            Do(new HoldHelperTurn(P1, true, false));
            rng.Queue(2, 1, -1); // rndcolor 2 (c_pink), rnd_x 1, rnd_y -1
            Run(5);
            var spark = events.OfType<HelperSparked>().Single();
            Assert.AreEqual(SparkleColor.Pink, spark.Color);
        }

        [Test]
        public void ItTrailsSmokeEveryFortySteps()
        {
            Call(0f);
            Run(39);
            Assert.IsEmpty(events.OfType<HelperTrailed>().ToList());
            Run(1);
            var trail = events.OfType<HelperTrailed>().Single();
            Assert.AreEqual(Px(39f * 4f - 4f), trail.At.X, 1e-4f, "x - hspeed, before the 40th step's motion");
            Run(40);
            Assert.AreEqual(2, events.OfType<HelperTrailed>().Count());
        }

        [Test]
        public void UpAndDownStepTheSpeedBetweenOneAndMax()
        {
            var h = Call(0f);
            Assert.IsFalse(Do(new PressHelperFaster(P1)).Accepted, "already at max_speed");
            for (int i = 0; i < 5; i++) Do(new PressHelperSlower(P1));
            Assert.AreEqual(1f, h.SpeedPx, "if (speed > 1) speed -= 1");
            Run(1);
            Assert.AreEqual(Px(1f), h.Position.X, 1e-6f, "the Step keeps Speed");
            Do(new PressHelperFaster(P1));
            Assert.AreEqual(2f, h.SpeedPx);
        }

        [Test]
        public void PausedItStopsButKeepsItsSpeed()
        {
            var h = Call(0f);
            Do(new SetHelpersPaused(true));
            Do(new HoldHelperTurn(P1, true, false));
            Run(10);
            Assert.AreEqual(Vec2.Zero, h.Position, "speed = 0 while paused");
            Assert.AreEqual(0f, h.Direction, "the held arrows wait");
            Assert.IsFalse(Do(new PressHelperSlower(P1)).Accepted);
            Assert.IsTrue(Do(new ToggleHelperSolo(P1)).Accepted, "Page Down has no pause check");

            Do(new SetHelpersPaused(false));
            Do(new HoldHelperTurn(P1, false, false));
            Run(1);
            Assert.AreEqual(Px(4f), h.Position.Magnitude, 1e-6f, "speed = Speed again");
        }

        [Test]
        public void SoloSteersTwoDegreesAStepTowardTheMouseUntilWithinFour()
        {
            var h = Call(0f);
            Owner(Vec2.Zero, mouse: new Vec2(0f, 1000f)); // far straight up: about 90 degrees
            Do(new ToggleHelperSolo(P1));
            Assert.IsTrue(h.Solo);
            Run(1);
            Assert.AreEqual(2f, h.Direction, 1e-4f, "check_rotate_clockwise says turn up: direction += 2");
            Run(50);
            Assert.That(h.Direction, Is.InRange(85f, 94f), "a_near_b(direction, mouse_direction, 4) stops it");
        }

        [Test]
        public void OutOfViewItReturnsToTheShipAndRejoinsWithinFourHundredPx()
        {
            var h = Call(0f);
            Owner(Vec2.Zero);
            Do(new SetHelperView(new RoomBounds(-Px(100f), -Px(100f), Px(100f), Px(100f))));
            Do(new PressHelperSlower(P1)); // 3 px a step
            Run(35); // in view at 99 px, out once at 102 px
            Assert.AreEqual(HelperState.Return, h.State);
            Assert.AreEqual(3f, h.LastSpeed, "last_speed = speed");
            Assert.AreEqual(1, events.OfType<HelperReturning>().Count());

            // Within 400 px it rejoins at once with its old speed and rings again on its next free step.
            Run(1);
            Assert.AreEqual(HelperState.Free, h.State);
            Assert.AreEqual(3f, h.SpeedPx);
            Assert.IsTrue(h.FirstPass);
        }

        [Test]
        public void FarAwayItHeadsStraightForTheShipAtMaxSpeedOrTheShipsIfFaster()
        {
            var h = Call(0f, new Vec2(Px(1000f), 0f));
            Owner(Vec2.Zero, speedPx: 6f);
            Do(new SetHelperView(new RoomBounds(-Px(100f), -Px(100f), Px(100f), Px(100f))));
            Run(1); // out of view: return
            Assert.AreEqual(HelperState.Return, h.State);
            events.Clear();
            Run(1);
            Assert.AreEqual(180f, h.Direction, 1e-3f, "direction = point_direction(x, y, player.x, player.y)");
            Assert.AreEqual(6f, h.SpeedPx, "player.speed > max_speed");
            Assert.IsTrue(events.OfType<HelperSmoked>().Single().Returning, "c_orange on the way back");

            Owner(Vec2.Zero, speedPx: 0f);
            Run(1);
            Assert.AreEqual(4f, h.SpeedPx, "max_speed");
            Assert.IsFalse(Do(new PressHelperSlower(P1)).Accepted, "the arrows only work free");
        }

        [Test]
        public void DistanceToObjectIsBoxToBox()
        {
            var h = Call(0f, new Vec2(Px(100f), 0f));
            float gap = field.DistanceToObjectPx(h, BoxAt(Vec2.Zero));
            Assert.AreEqual(100f - 16f - 10f, gap, 1e-3f, "spr_helper's 32 px box to the ship's");
        }

        [Test]
        public void DeleteSetsItOffWhereItIs()
        {
            var h = Call(0f);
            Run(3);
            events.Clear();
            Do(new SetHelpersPaused(true));
            Assert.IsTrue(Do(new DetonateHelper(P1)).Accepted, "no pause check");
            var blast = events.OfType<HelperExploded>().Single();
            Assert.AreEqual(h.Position, blast.At);
            Assert.IsTrue(h.IsGone);
            Assert.IsNull(field.HelperOf(P1));
            Assert.IsTrue(Do(new CallHelper(P1, Vec2.Zero, 0f)).Accepted, "a new one can be called");
        }

        [Test]
        public void TheContentSectionValidates()
        {
            var bad = ContentRegistry.Default.Helper;
            bad.MinSpeedPx = 9f;
            bad.Box = new float[] { 1f };
            var errors = new List<string>();
            typeof(HelperTuning).GetMethod("Validate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(bad, new object[] { errors, "helper" });
            Assert.AreEqual(2, errors.Count, string.Join("; ", errors));
        }
    }
}
