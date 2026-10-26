using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Content;
using Unity.Hare.Pickups;
using Unity.Hare.Player;
using Unity.Hare.Session;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// gmlHARE's pickups and scraps run outside Unity: random_pickup's rolls, obj_carrot's hover, ping and heal,
    /// obj_coin's grades, fall, sparkle and COINS, the shared magnet, spawn_chips/obj_chip and obj_cartridge.
    /// </summary>
    public class PickupTests
    {
        const float Step = 1f / GameClock.StepsPerSecond;
        static float Px(float px) => GameClock.Px(px);

        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<int> next = new Queue<int>();
            public readonly List<(int Min, int Max)> Asked = new List<(int, int)>();
            public ScriptedRandom(params int[] values) => Queue(values);
            public void Queue(params int[] values) { foreach (var v in values) next.Enqueue(v); }
            public int IRandom(int min, int max)
            {
                Asked.Add((min, max));
                return next.Count > 0 ? next.Dequeue() : min;
            }
            public float Range(float min, float max) => min;
        }

        ScriptedRandom rng;
        PickupField field;
        readonly List<PickupEvent> events = new List<PickupEvent>();

        [SetUp]
        public void SetUp()
        {
            rng = new ScriptedRandom();
            field = new PickupField(rng, ContentRegistry.Default.Pickups);
            events.Clear();
        }

        PickupResult Do(PickupCommand command)
        {
            var result = field.Execute(command);
            events.AddRange(result.Events);
            return result;
        }

        void Run(int steps)
        {
            for (int i = 0; i < steps; i++) Do(new TickPickups(Step));
        }

        Pickup Drop(PickupKind kind, params int[] rolls)
        {
            rng.Queue(rolls);
            Do(new DropPickup(kind, Vec2.Zero));
            return field.Pickups.Last();
        }

        // ---- random_pickup ----

        [Test]
        public void RandomPickupRollsOneOrTwoThenACarrotOneInFour()
        {
            rng.Queue(1, 1);
            Assert.IsTrue(Do(new DropRandomPickup(Vec2.Zero)).Accepted);
            Assert.AreEqual(PickupKind.Carrot, field.Pickups.Single().Kind);
            CollectionAssert.AreEqual(new[] { (1, 2), (1, 4) }, rng.Asked);

            rng.Queue(1, 3);
            Assert.IsFalse(Do(new DropRandomPickup(Vec2.Zero)).Accepted, "a missed carrot roll drops nothing");
            Assert.AreEqual(1, field.Pickups.Count);
        }

        [Test]
        public void RandomPickupsSecondRollIsAlwaysACoin()
        {
            rng.Queue(2, 50);
            Assert.IsTrue(Do(new DropRandomPickup(Vec2.Zero)).Accepted);
            Assert.AreEqual(PickupKind.Coin, field.Pickups.Single().Kind);
            Assert.AreEqual((1, 100), rng.Asked.Last(), "the coin's own Create roll");
        }

        [TestCase(1, CoinGrade.Blue, 5, "spr_coin_blue")]
        [TestCase(75, CoinGrade.Blue, 5, "spr_coin_blue")]
        [TestCase(76, CoinGrade.Silver, 10, "spr_coin_silver")]
        [TestCase(97, CoinGrade.Silver, 10, "spr_coin_silver")]
        [TestCase(98, CoinGrade.Gold, 20, "spr_coin_gold")]
        [TestCase(100, CoinGrade.Gold, 20, "spr_coin_gold")]
        public void CoinGradeAndValueFollowItsRoll(int roll, CoinGrade grade, int value, string sprite)
        {
            var coin = Drop(PickupKind.Coin, roll);
            Assert.AreEqual(grade, coin.Grade);
            Assert.AreEqual(value, coin.Value);
            Assert.AreEqual(sprite, coin.SpriteName);
        }

        // ---- obj_carrot ----

        [Test]
        public void CarrotHoversEveryEightStepsSwingingBetweenMinusAndPlusFour()
        {
            var carrot = Drop(PickupKind.Carrot);
            var seen = new List<float>();
            for (int hover = 0; hover < 20; hover++)
            {
                float before = carrot.Position.Y;
                Run(8);
                seen.Add((before - carrot.Position.Y) * GameClock.PixelsPerUnit); // GML y += hoverY (down)
            }
            var expected = new float[] { 1, 2, 3, 4, 4, 3, 2, 1, 0, -1, -2, -3, -4, -4, -3, -2, -1, 0, 1, 2 };
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], seen[i], 1e-3f, "hover " + i);
        }

        [Test]
        public void CarrotPingsARedRingEvery120StepsAndNeverFalls()
        {
            var carrot = Drop(PickupKind.Carrot);
            Run(119);
            Assert.AreEqual(0, events.OfType<CarrotPinged>().Count());
            Run(1);
            Assert.AreEqual(1, events.OfType<CarrotPinged>().Count());
            Run(120);
            Assert.AreEqual(2, events.OfType<CarrotPinged>().Count());
            Assert.AreEqual(0f, carrot.Position.X, 1e-6f);
            Assert.AreEqual(0f, carrot.SpeedPxPerStep, "its y += 1 is commented out");
        }

        [Test]
        public void TheMagnetPullsAtFourPxAStepWithin48PxOfTheBoxAndKeepsTheSpeedOutOfRange()
        {
            var carrot = Drop(PickupKind.Carrot);
            // The carrot's box reaches 6 px right of its origin: the ship 54 px away is 48 px from the box, out of range.
            Do(new SetPickupTarget(new PlayerId(1), new Vec2(Px(54f), 0f)));
            Run(1);
            Assert.AreEqual(0f, carrot.SpeedPxPerStep);

            Do(new SetPickupTarget(new PlayerId(1), new Vec2(Px(53f), 0f)));
            Run(1);
            Assert.AreEqual(4f, carrot.SpeedPxPerStep);
            Assert.AreEqual(0f, carrot.Direction, 1e-3f);
            Assert.AreEqual(Px(4f), carrot.Position.X, 1e-6f);

            Do(new ClearPickupTarget(new PlayerId(1)));
            Run(1);
            Assert.AreEqual(Px(8f), carrot.Position.X, 1e-6f, "GameMaker keeps the speed move_towards_point set");
        }

        [Test]
        public void TheMagnetPullsTowardTheNearestShip()
        {
            var coin = Drop(PickupKind.Coin, 1);
            Do(new SetPickupTarget(new PlayerId(1), new Vec2(Px(40f), 0f)));
            Do(new SetPickupTarget(new PlayerId(2), new Vec2(0f, Px(30f))));
            Run(1);
            Assert.AreEqual(90f, coin.Direction, 1e-3f);
        }

        [Test]
        public void ACarrotHealsTheShipThatTakesItOneHpUpToMax()
        {
            var carrot = Drop(PickupKind.Carrot);
            Assert.IsTrue(Do(new CollectPickup(new PlayerId(7), carrot.Id)).Accepted);
            var taken = events.OfType<PickupCollected>().Single();
            Assert.AreEqual(7, taken.By.Value);
            Assert.AreEqual(1, taken.Pickup.Value, "one HP");
            Assert.IsEmpty(field.Pickups);
            Assert.IsFalse(Do(new CollectPickup(new PlayerId(7), carrot.Id)).Accepted, "gone");

            var player = new PlayerRig();
            Assert.IsFalse(player.Do(new RestoreHealth(player.Id)).Accepted, "already at MAXHP");
            player.Do(new TakeHit(player.Id, ignoresSpawnProtection: true));
            Assert.AreEqual(5, player.Health.Health);
            Assert.IsTrue(player.Do(new RestoreHealth(player.Id, taken.Pickup.Value)).Accepted);
            Assert.AreEqual(6, player.Health.Health);
            Assert.AreEqual(6, player.Seen<PlayerHealed>().Single().Health);
        }

        // ---- obj_coin ----

        [Test]
        public void ACoinFallsOnePxAStepAndIsGonePastTheRoomsBottomPlusItsHeight()
        {
            // The bottom is 100.5 px down, so the coin is gone once it is below 132.5 px (room_height + 32).
            Do(new SetPickupRoom(new RoomBounds(-10f, -1.005f, 10f, 1f)));
            var coin = Drop(PickupKind.Coin, 1);
            Run(10);
            Assert.AreEqual(-Px(10f), coin.Position.Y, 1e-5f);

            Run(123); // 133 px down: the Step checks before it falls, and saw 132
            Assert.IsFalse(coin.IsGone);
            Run(1);
            Assert.IsTrue(coin.IsGone);
            Assert.AreSame(coin, events.OfType<PickupRemoved>().Single().Pickup);
        }

        [Test]
        public void ACoinSparklesEveryFiveStepsInAmberPinkOrCornflower()
        {
            var coin = Drop(PickupKind.Coin, 1);
            rng.Queue(2, 2, -2);
            Run(5);
            var spark = events.OfType<CoinSparkled>().Single();
            Assert.AreEqual(SparkleColor.Pink, spark.Color);
            // x - hspeed + rnd_x, y - vspeed + rnd_y (GML y down): no speed yet; the alarm ran before the fifth step's fall.
            Assert.AreEqual(Px(2f), spark.At.X, 1e-6f);
            Assert.AreEqual(coin.Position.Y + Px(1f) + Px(2f), spark.At.Y, 1e-6f);
        }

        [Test]
        public void CoinsCountAndTheHundredthIsAnExtraLife()
        {
            var session = new GameSession(ContentRegistry.Default);
            int lives = session.Lives;
            for (int i = 0; i < 4; i++) session.Execute(new CollectCoin(new PlayerId(1), 20));
            Assert.AreEqual(80, session.Coins);
            var result = session.Execute(new CollectCoin(new PlayerId(1), 20));
            Assert.AreEqual(0, session.Coins);
            Assert.AreEqual(lives + 1, session.Lives);
            Assert.IsTrue(result.Events.OfType<ExtraLifeEarned>().Any());
        }

        [Test]
        public void SetupLevelClearsThePickupsButNotTheScraps()
        {
            Drop(PickupKind.Coin, 1);
            Drop(PickupKind.Carrot);
            Do(new EjectCasing(ScrapKind.Cartridge, Vec2.Zero));
            Assert.IsTrue(Do(new ClearPickups()).Accepted);
            Assert.IsEmpty(field.Pickups);
            Assert.AreEqual(2, events.OfType<PickupRemoved>().Count());
            Assert.AreEqual(1, field.Scraps.Count);
        }

        [Test]
        public void PausedStopsTheHoverAndFallButNotTheMagnetsSpeed()
        {
            var coin = Drop(PickupKind.Coin, 1);
            Do(new SetPickupTarget(new PlayerId(1), new Vec2(Px(30f), 0f)));
            Run(1);
            Do(new ClearPickupTarget(new PlayerId(1)));
            Do(new SetPickupsPaused(true));
            float x = coin.Position.X, y = coin.Position.Y;
            Run(3);
            Assert.AreEqual(x + Px(12f), coin.Position.X, 1e-5f, "built-in motion runs while paused");
            Assert.AreEqual(y, coin.Position.Y, 1e-5f, "the Step's y += 1 is gated");
        }

        // ---- spawn_chips and obj_chip ----

        [Test]
        public void SpawnChipsThrowsSixToTwelveAtTheFeetInTheOriginalsDrawOrder()
        {
            // count 2; chip 1: jitter 3, sprite 4, direction +10, angle 200, spin 0 (-> 3), vx -4, vy 5, spurt roll 2;
            // chip 2: jitter -1, sprite 1, direction -80, angle 0, spin -6, vx 4, vy 2, spurt 1 -> vy 16.
            rng.Queue(2, 3, 4, 10, 200, 0, -4, 5, 2, -1, 1, -80, 0, -6, 4, 2, 1, 16);
            Do(new KickUpChips(Vec2.Zero, 90f));
            Assert.AreEqual((6, 12), rng.Asked[0]);
            Assert.AreEqual(2, field.Scraps.Count);
            var a = field.Scraps[0];
            Assert.AreEqual(ScrapKind.Chip, a.Kind);
            Assert.AreEqual("spr_chip4", a.SpriteName);
            Assert.AreEqual(0.5f, a.Scale);
            Assert.AreEqual(6f, a.HeightPx, "12 px tall at half scale");
            Assert.AreEqual(0f, a.Position.X, 1e-5f);
            Assert.AreEqual(Px(32f) - Px(3f), a.Position.Y, 1e-5f, "32 px along image_angle 90, then 3 px down");
            Assert.AreEqual(100f, a.Direction);
            Assert.AreEqual(200f, a.ImageAngle);
            Assert.AreEqual(3f, a.Spin);
            Assert.AreEqual(-4f, a.VelocityXPx);
            Assert.AreEqual(5f, a.VelocityYPx);
            var b = field.Scraps[1];
            Assert.AreEqual("spr_chip1", b.SpriteName);
            Assert.AreEqual(-6f, b.Spin);
            Assert.AreEqual(16f, b.VelocityYPx, "the 1-in-32 huge spurt");
            Assert.AreEqual((8, 16), rng.Asked.Last());
        }

        [Test]
        public void AChipMovesEveryThreeStepsRisingThenFallingAndSpinsEveryStep()
        {
            rng.Queue(1, 0, 1, 0, 10, 2, 3, 6, 2);
            Do(new KickUpChips(Vec2.Zero, 0f));
            var chip = field.Scraps.Single();
            Vec2 start = chip.Position;
            Run(2);
            Assert.AreEqual(start, chip.Position, "Alarm 0 has not fired");
            Assert.AreEqual(14f, chip.ImageAngle, 1e-4f);
            Run(1);
            // x += 3; y -= 6; velocityY -= 1; y += 1: 5 px up.
            Assert.AreEqual(start.X + Px(3f), chip.Position.X, 1e-5f);
            Assert.AreEqual(start.Y + Px(5f), chip.Position.Y, 1e-5f);
            Assert.AreEqual(5f, chip.VelocityYPx);
            Run(3);
            Assert.AreEqual(start.Y + Px(9f), chip.Position.Y, 1e-5f, "then 4 px");
        }

        [Test]
        public void ScrapsFallOutOfTheRoom()
        {
            Do(new SetPickupRoom(new RoomBounds(-1f, -0.1f, 1f, 0.1f)));
            Do(new EjectCasing(ScrapKind.Shell, Vec2.Zero));
            for (int i = 0; i < 2000 && field.Scraps.Count > 0; i++) Run(1);
            Assert.IsEmpty(field.Scraps);
            Assert.AreEqual(ScrapKind.Shell, events.OfType<ScrapRemoved>().Single().Scrap.Kind);
        }

        [Test]
        public void ACasingIsThrownWithItsOwnRanges()
        {
            rng.Queue(-5, 12, 0);
            Do(new EjectCasing(ScrapKind.Cartridge, new Vec2(1f, 2f)));
            CollectionAssert.AreEqual(new[] { (-5, 5), (2, 12), (-3, 3) }, rng.Asked);
            var casing = field.Scraps.Single();
            Assert.AreEqual("spr_ammo_cartridge", casing.SpriteName);
            Assert.AreEqual(1f, casing.Scale);
            Assert.AreEqual(0f, casing.ImageAngle);
            Assert.AreEqual(3f, casing.Spin, "a 0 spin becomes 3");
            Run(2);
            Assert.AreEqual(1f - Px(5f), casing.Position.X, 1e-5f, "it moves every 2 steps");
            Assert.IsFalse(Do(new EjectCasing(ScrapKind.Chip, Vec2.Zero)).Accepted);
        }

        [Test]
        public void ASeedRepeatsTheDrops()
        {
            string Run(int seed)
            {
                var f = new PickupField(new SeededRandom(seed), ContentRegistry.Default.Pickups);
                for (int i = 0; i < 40; i++) f.Execute(new DropRandomPickup(Vec2.Zero));
                f.Execute(new KickUpChips(Vec2.Zero, 45f));
                return string.Join(",", f.Pickups.Select(p => p.SpriteName)) + "|" + string.Join(",", f.Scraps.Select(s => s.SpriteName + s.VelocityYPx));
            }
            Assert.AreEqual(Run(5), Run(5));
        }

        [Test]
        public void TheContentFileCarriesThePickups()
        {
            var registry = ContentRegistry.FromJson("{\"pickups\": {\"MagnetRangePx\": 64}}");
            Assert.AreEqual(64f, registry.Pickups.MagnetRangePx);
            Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson("{\"pickups\": {\"CoinBox\": [1, 2]}}"));
        }
    }
}
