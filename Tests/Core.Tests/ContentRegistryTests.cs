using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Content;
using Unity.Hare.Enemies;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// U.HARE-T41: the content/tuning registry loads from a data file and validates on load with clear errors.
    /// </summary>
    public class ContentRegistryTests
    {
        [TearDown]
        public void TearDown() => GameContent.Current = ContentRegistry.Default;

        [Test]
        public void DefaultRegistryMatchesTheCompiledInNumbers()
        {
            var registry = ContentRegistry.Default;
            Assert.AreEqual(9, registry.Weapons.VulcanBaseRateSteps);
            Assert.AreEqual(150, registry.Ammo.GroundPrimary.Max);
            Assert.AreEqual(5f, registry.Flight.FlightSpeed);
            Assert.AreEqual(6, registry.Health.MaxHealth);
            Assert.AreEqual(8f, registry.WeaponPhysicals.Vulcan.BulletSpeed);
            Assert.AreEqual(240, registry.Emitters.FirstAlarmMaxSteps);
            Assert.AreEqual(240, registry.Wormholes.OneTickSteps);
            Assert.AreEqual(100, registry.Enemies.Get(EnemyKind.Worm).Points);
        }

        [Test]
        public void FromJsonOverlaysOnlyTheFieldsItMentions()
        {
            var registry = ContentRegistry.FromJson("{\"weapons\": {\"VulcanBaseRateSteps\": 15}}");

            Assert.AreEqual(15, registry.Weapons.VulcanBaseRateSteps);
            Assert.AreEqual(9, registry.Weapons.VulcanMinRateSteps, "an untouched field keeps its compiled-in default");
            Assert.AreEqual(150, registry.Ammo.GroundPrimary.Max, "a whole section the file omits keeps its defaults");
        }

        [Test]
        public void FromJsonOverlaysNestedFields()
        {
            var registry = ContentRegistry.FromJson("{\"ammo\": {\"GroundPrimary\": {\"Max\": 999}}}");

            Assert.AreEqual(999, registry.Ammo.GroundPrimary.Max);
            Assert.AreEqual(1, registry.Ammo.GroundPrimary.Cost, "a sibling field on the same nested object keeps its default");
        }

        [Test]
        public void FromJsonOverlaysAnEnemyEntry()
        {
            var registry = ContentRegistry.FromJson("{\"enemies\": {\"Worm\": {\"Points\": 500}}}");

            Assert.AreEqual(500, registry.Enemies.Get(EnemyKind.Worm).Points);
            Assert.AreEqual(10, registry.Enemies.Get(EnemyKind.Hydra).Points, "a different enemy's entry is untouched");
        }

        [Test]
        public void MalformedJsonThrowsWithAClearMessage()
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson("{ not json"));
            Assert.AreEqual(1, ex.Errors.Count);
            Assert.IsTrue(ex.Errors[0].Contains("not valid JSON"));
        }

        [Test]
        public void AnUnrecognisedFieldIsReportedByName()
        {
            var ex = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{\"weapons\": {\"NotARealField\": 1}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("NotARealField") && e.Contains("not a recognised field")));
        }

        [Test]
        public void AnUnrecognisedSectionIsReportedByName()
        {
            var ex = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{\"notASection\": {}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("notASection") && e.Contains("not a recognised section")));
        }

        [Test]
        public void WrongTypeIsReportedByPath()
        {
            var ex = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{\"weapons\": {\"VulcanBaseRateSteps\": \"nine\"}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("weapons.VulcanBaseRateSteps")));
        }

        [Test]
        public void OutOfRangeValuesAreCollectedNotJustTheFirstOne()
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{\"weapons\": {\"VulcanBaseRateSteps\": -1}, \"flight\": {\"FlightSpeed\": -1}}"));

            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("weapons.VulcanBaseRateSteps")));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("flight.FlightSpeed")));
        }

        [Test]
        public void MissingAnEnemyKindFailsValidation()
        {
            // enemies is populated via the generic binder onto EnemyCatalog's own fields, which always exist for
            // every EnemyKind; the catalog itself still checks the loaded value's shape (e.g. a bad DeathBurstSize).
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{\"enemies\": {\"Worm\": {\"DeathBurstSize\": 99}}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("enemies.Worm.DeathBurstSize")));
        }

        [Test]
        public void ContentCurrentDefaultsToTheCompiledInNumbers()
        {
            Assert.AreEqual(ContentRegistry.Default.Weapons.VulcanBaseRateSteps, GameContent.Current.Weapons.VulcanBaseRateSteps);
            Assert.AreEqual(ContentRegistry.Default.Flight.FlightSpeed, GameContent.Current.Flight.FlightSpeed);
        }

        [Test]
        public void DefaultIsAFreshInstanceEveryTime()
        {
            // Every section's fields are public and mutable (the binder overlays onto them), so Default must never
            // be a cached singleton: mutating one read must never leak into the next read or into another consumer.
            Assert.AreNotSame(ContentRegistry.Default, ContentRegistry.Default);
            Assert.AreNotSame(ContentRegistry.Default.Flight, ContentRegistry.Default.Flight);

            var first = ContentRegistry.Default;
            first.Weapons.VulcanBaseRateSteps = 999;
            Assert.AreEqual(9, ContentRegistry.Default.Weapons.VulcanBaseRateSteps, "mutating one Default must not affect the next read");
        }

        [Test]
        public void GroundAndAirCycleMustNotBeEmpty()
        {
            var ex = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{\"weapons\": {\"GroundCycle\": []}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("weapons.GroundCycle")));
        }

        /// <summary>
        /// The shipped data file is never otherwise parsed by anything that runs in CI: a bad field name, enum
        /// value or out-of-range number in it would only ever surface as a logged warning at runtime
        /// (<see cref="ContentBootstrap"/> falls back to the defaults rather than crashing). This proves it loads
        /// clean, and that loading it changes nothing: every number still matches the compiled-in default exactly.
        /// </summary>
        [Test]
        public void TheShippedGameContentJsonLoadsAndMatchesTheDefaults()
        {
            string json = File.ReadAllText(ShippedGameContentJsonPath());
            var fromFile = ContentRegistry.FromJson(json);
            var defaults = ContentRegistry.Default;

            Assert.AreEqual(defaults.Weapons.VulcanBaseRateSteps, fromFile.Weapons.VulcanBaseRateSteps);
            Assert.AreEqual(defaults.Weapons.VulcanMinRateSteps, fromFile.Weapons.VulcanMinRateSteps);
            Assert.AreEqual(defaults.Weapons.ShotgunIntervalSteps, fromFile.Weapons.ShotgunIntervalSteps);
            Assert.AreEqual(defaults.Weapons.RailgunIntervalSteps, fromFile.Weapons.RailgunIntervalSteps);
            CollectionAssert.AreEqual(defaults.Weapons.GroundCycle, fromFile.Weapons.GroundCycle);
            CollectionAssert.AreEqual(defaults.Weapons.AirCycle, fromFile.Weapons.AirCycle);

            Assert.AreEqual(defaults.Ammo.GroundPrimary.Max, fromFile.Ammo.GroundPrimary.Max);
            Assert.AreEqual(defaults.Ammo.AirSecondary.Regen, fromFile.Ammo.AirSecondary.Regen);

            Assert.AreEqual(defaults.Flight.FlightSpeed, fromFile.Flight.FlightSpeed);
            Assert.AreEqual(defaults.Flight.JumpArcDegrees, fromFile.Flight.JumpArcDegrees);

            Assert.AreEqual(defaults.Health.MaxHealth, fromFile.Health.MaxHealth);
            Assert.AreEqual(defaults.Health.DeathWaitSteps, fromFile.Health.DeathWaitSteps);

            Assert.AreEqual(defaults.WeaponPhysicals.Vulcan.BulletSpeed, fromFile.WeaponPhysicals.Vulcan.BulletSpeed);
            Assert.AreEqual(defaults.WeaponPhysicals.Shotgun.PelletCount, fromFile.WeaponPhysicals.Shotgun.PelletCount);
            Assert.AreEqual(defaults.WeaponPhysicals.Flamer.FlameLifetime, fromFile.WeaponPhysicals.Flamer.FlameLifetime, 1e-4f);
            Assert.AreEqual(defaults.WeaponPhysicals.Laser.BeamLengthPx, fromFile.WeaponPhysicals.Laser.BeamLengthPx);
            Assert.AreEqual(defaults.WeaponPhysicals.PerchedMuzzle.MuzzleDistancePx, fromFile.WeaponPhysicals.PerchedMuzzle.MuzzleDistancePx);

            Assert.AreEqual(defaults.Emitters.FirstAlarmMaxSteps, fromFile.Emitters.FirstAlarmMaxSteps);
            Assert.AreEqual(defaults.Wormholes.OneTickSteps, fromFile.Wormholes.OneTickSteps);

            Assert.AreEqual(defaults.Weather.CloudRateSteps, fromFile.Weather.CloudRateSteps);
            Assert.AreEqual(defaults.Weather.SnowZeroFall, fromFile.Weather.SnowZeroFall);
            Assert.AreEqual(defaults.Weather.FogCount, fromFile.Weather.FogCount);
            CollectionAssert.AreEqual(defaults.Weather.CloudSprites, fromFile.Weather.CloudSprites);
            for (int i = 0; i < defaults.Arena.Levels.Length; i++)
                CollectionAssert.AreEqual(defaults.Arena.Levels[i].Weather, fromFile.Arena.Levels[i].Weather);

            Assert.AreEqual(defaults.Dialogue.Lines.Length, fromFile.Dialogue.Lines.Length);
            for (int i = 0; i < defaults.Dialogue.Lines.Length; i++)
                Assert.AreEqual((defaults.Dialogue.Lines[i].Id, defaults.Dialogue.Lines[i].Speaker, defaults.Dialogue.Lines[i].Text),
                    (fromFile.Dialogue.Lines[i].Id, fromFile.Dialogue.Lines[i].Speaker, fromFile.Dialogue.Lines[i].Text));
            Assert.AreEqual(defaults.Story.Typewriter.SilentChars, fromFile.Story.Typewriter.SilentChars);
            Assert.AreEqual(defaults.Story.Typewriter.PauseDelaySteps, fromFile.Story.Typewriter.PauseDelaySteps);
            Assert.AreEqual(defaults.Story.Conversation.Typewriter.Sound, fromFile.Story.Conversation.Typewriter.Sound);
            Assert.AreEqual(defaults.Story.Conversation.SlideOutMin, fromFile.Story.Conversation.SlideOutMin);
            Assert.AreEqual(defaults.Story.ScrollDelaySteps, fromFile.Story.ScrollDelaySteps);
            Assert.AreEqual(defaults.Story.AutoSpeed, fromFile.Story.AutoSpeed);
            Assert.AreEqual(defaults.Story.PlainTypewriter.Pauses, fromFile.Story.PlainTypewriter.Pauses);
            for (int i = 0; i < defaults.Story.Scenarios.Length; i++)
                CollectionAssert.AreEqual(defaults.Story.Scenarios[i].Levels, fromFile.Story.Scenarios[i].Levels);

            // U.HARE-T19's map section.
            Assert.AreEqual(defaults.Map.TurnSpeed, fromFile.Map.TurnSpeed);
            Assert.AreEqual(defaults.Map.AutoSpeed, fromFile.Map.AutoSpeed);
            Assert.AreEqual(defaults.Map.ShipX, fromFile.Map.ShipX);
            Assert.AreEqual(defaults.Map.DestinationX, fromFile.Map.DestinationX);
            Assert.AreEqual(defaults.Map.ScannerSprite, fromFile.Map.ScannerSprite);
            Assert.AreEqual(defaults.Map.ScannerX, fromFile.Map.ScannerX);
            Assert.AreEqual(defaults.Map.ScannerLift, fromFile.Map.ScannerLift);
            Assert.AreEqual(defaults.Map.ScannerAlpha, fromFile.Map.ScannerAlpha);
            Assert.AreEqual(defaults.Map.ScannerSweepAmount, fromFile.Map.ScannerSweepAmount);
            Assert.AreEqual(defaults.Map.ScannerSweepSpeed, fromFile.Map.ScannerSweepSpeed);
            Assert.AreEqual(defaults.Map.ScannerAlphaUnit, fromFile.Map.ScannerAlphaUnit);
            Assert.AreEqual(defaults.Map.ScannerAlphaMin, fromFile.Map.ScannerAlphaMin);
            Assert.AreEqual(defaults.Map.ScannerAlphaMax, fromFile.Map.ScannerAlphaMax);
            Assert.AreEqual(defaults.Map.DotFadeSpeed, fromFile.Map.DotFadeSpeed);
            Assert.AreEqual(defaults.Map.LaunchRoom, fromFile.Map.LaunchRoom);
            Assert.AreEqual(defaults.Map.PanelText, fromFile.Map.PanelText);
            Assert.AreEqual(defaults.Map.LockedText, fromFile.Map.LockedText);
            Assert.AreEqual(defaults.Map.Upgrade.Message, fromFile.Map.Upgrade.Message);
            Assert.AreEqual(defaults.Map.Upgrade.Room, fromFile.Map.Upgrade.Room);
            Assert.AreEqual(defaults.Map.MarkerMask.Frames.Length, fromFile.Map.MarkerMask.Frames.Length, "the masks are compiled in");
            Assert.AreEqual(defaults.Helper.CallSound, fromFile.Helper.CallSound);
            Assert.AreEqual(defaults.Helper.StartSpeedPx, fromFile.Helper.StartSpeedPx);
            Assert.AreEqual(defaults.Helper.TurnSpeed, fromFile.Helper.TurnSpeed);
            Assert.AreEqual(defaults.Helper.SmokeEverySteps, fromFile.Helper.SmokeEverySteps);
            Assert.AreEqual(defaults.Helper.ReturnDistancePx, fromFile.Helper.ReturnDistancePx);
            CollectionAssert.AreEqual(defaults.Helper.Box, fromFile.Helper.Box);

            foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
            {
                var expected = defaults.Enemies.Get(kind);
                var actual = fromFile.Enemies.Get(kind);
                Assert.AreEqual(expected.Points, actual.Points, kind.ToString());
                Assert.AreEqual(expected.PartPoints, actual.PartPoints, kind.ToString());
                Assert.AreEqual(expected.DeathBurstSize, actual.DeathBurstSize, kind.ToString());
                Assert.AreEqual(expected.DifficultyPoints, actual.DifficultyPoints, kind.ToString());
                Assert.AreEqual(expected.EmitterScale, actual.EmitterScale, kind.ToString());
                Assert.AreEqual(expected.WormholeScale, actual.WormholeScale, kind.ToString());
                Assert.AreEqual(expected.BBoxHalfPx, actual.BBoxHalfPx, kind.ToString());
                Assert.AreEqual(expected.BBoxOffsetPx, actual.BBoxOffsetPx, kind.ToString());
                Assert.AreEqual(expected.PushesAway, actual.PushesAway, kind.ToString());
            }
        }

        /// <summary>This file's own path, at compile time, so the shipped data file resolves relative to the repo
        /// rather than to whatever directory `dotnet test` happens to run from.</summary>
        static string ShippedGameContentJsonPath([CallerFilePath] string here = "")
        {
            // here = .../Tests/Core.Tests/ContentRegistryTests.cs
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
            return Path.Combine(repoRoot, "Assets", "Unity.Rea.Hare", "Resources", "Hare", "Content", "GameContent.json");
        }
    }
}
