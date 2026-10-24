using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
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
        public void ContentCurrentDefaultsToTheCompiledInRegistry()
        {
            Assert.AreSame(ContentRegistry.Default, GameContent.Current);
        }

        [Test]
        public void GroundAndAirCycleMustNotBeEmpty()
        {
            var ex = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{\"weapons\": {\"GroundCycle\": []}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("weapons.GroundCycle")));
        }
    }
}
