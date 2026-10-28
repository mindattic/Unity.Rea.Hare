using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Content;
using Unity.Hare.Session;
using Unity.Hare.Upgrade;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// room_upgrade's garage (U.HARE-T24, docs/BIBLE.md V49, D27): the shipped pips, hovering and buying them with
    /// SKILLPOINTS into the campaign, the points a won mission adds, the save keeping them and what they do to a ship.
    /// </summary>
    public class UpgradeTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        static ContentRegistry Shipped => ContentRegistry.FromJson(File.ReadAllText(ShippedGameContentJsonPath()));

        static UpgradeScreen Garage(CampaignProgress progress, out UpgradeCatalog catalog)
        {
            var content = Shipped;
            catalog = content.Upgrades;
            var screen = new UpgradeScreen(catalog, progress, content.Session.StartingSkillPoints);
            Assert.IsTrue(screen.Execute(new CreateUpgrade(P1)).Accepted);
            return screen;
        }

        /// <summary>The middle of a pip's box.</summary>
        static (double x, double y) Centre(UpgradeScreenTuning screen, UpgradeCost pip) =>
            (pip.X + (screen.PipMask.Left + screen.PipMask.Right) / 2.0, pip.Y + (screen.PipMask.Top + screen.PipMask.Bottom) / 2.0);

        [Test]
        public void TheShippedGarageHasTheOriginalsRowsAndFillsTheirGaps()
        {
            var catalog = Shipped.Upgrades;
            Assert.AreEqual(12, catalog.Upgrades.Length);
            foreach (UpgradeTrack track in System.Enum.GetValues(typeof(UpgradeTrack)))
                Assert.AreEqual(3, catalog.Upgrades.Count(u => u.Track == track), track.ToString());
            Assert.IsTrue(catalog.Upgrades.All(u => u.Cost == 1), "a pip costs one skill point, as in the original");
            var first = catalog.Find("vulcan.rate.1");
            Assert.AreEqual(528, first.X);
            Assert.AreEqual(352, first.Y);
            Assert.AreEqual("Increase Vulcan Cannon's fire rate by 5%", first.Message);
            Assert.AreEqual(3, catalog.Screen.Icons.Length);
            Assert.AreEqual(4, catalog.Screen.Labels.Length);
        }

        [Test]
        public void APipIsBoughtOnceWithASkillPointAndSaved()
        {
            var progress = new CampaignProgress();
            var screen = Garage(progress, out var catalog);
            Assert.AreEqual(5, screen.SkillPoints, "globals.gml's SKILLPOINTS = 5");

            var pip = catalog.Find("shotgun.spread.1");
            var (x, y) = Centre(catalog.Screen, pip);
            var hover = screen.Execute(new PointUpgrade(P1, x, y));
            Assert.IsTrue(hover.Accepted);
            Assert.AreEqual(pip.Message, hover.Events.OfType<UpgradeMessageChanged>().Single().Message);
            Assert.AreEqual("sfx_hover", hover.Events.OfType<UpgradeSoundRequested>().Single().Sound);

            var bought = screen.Execute(new PressUpgrade(P1, x, y));
            Assert.IsTrue(bought.Accepted);
            Assert.AreEqual(4, bought.Events.OfType<UpgradeBought>().Single().SkillPoints);
            Assert.IsTrue(screen.Owns(pip.Id));
            Assert.IsFalse(screen.Execute(new PressUpgrade(P1, x, y)).Accepted, "a filled pip is not bought again");
            Assert.AreEqual(4, screen.SkillPoints);

            var back = CampaignProgress.FromJson(progress.ToJson());
            CollectionAssert.AreEqual(new[] { pip.Id }, back.Upgrades.ToArray());
            Assert.AreEqual(4, back.SkillPoints(5, catalog));
        }

        [Test]
        public void PointsRunOutAndAWonMissionAddsOne()
        {
            var progress = new CampaignProgress();
            var screen = Garage(progress, out var catalog);
            foreach (var pip in catalog.Upgrades.Take(5))
            {
                var (x, y) = Centre(catalog.Screen, pip);
                Assert.IsTrue(screen.Execute(new PressUpgrade(P1, x, y)).Accepted, pip.Id);
            }
            Assert.AreEqual(0, screen.SkillPoints);
            var sixth = catalog.Upgrades[5];
            var at = Centre(catalog.Screen, sixth);
            Assert.IsFalse(screen.Execute(new PressUpgrade(P1, at.x, at.y)).Accepted, "no points left");

            progress.RecordWin(2);
            progress.RecordWin(2);
            Assert.AreEqual(1, screen.SkillPoints, "a mission's first win only");
            Assert.IsTrue(screen.Execute(new PressUpgrade(P1, at.x, at.y)).Accepted);
            Assert.IsFalse(screen.Execute(new PressUpgrade(P1, 10, 1000)).Accepted, "off every pip");
        }

        [Test]
        public void BoughtPipsScaleTheShipsWeapons()
        {
            var catalog = Shipped.Upgrades;
            var none = catalog.EffectsOf(new string[0]);
            Assert.AreEqual(1f, none.VulcanIntervalScale);
            Assert.AreEqual(1f, none.ShotgunIntervalScale);
            Assert.AreEqual(1f, none.ShotgunSpreadScale);
            Assert.AreEqual(1f, none.MissileRadiusScale);

            var all = catalog.EffectsOf(catalog.Upgrades.Select(u => u.Id));
            Assert.AreEqual(1f / 1.15f, all.VulcanIntervalScale, 1e-5f);
            Assert.AreEqual(0.85f, all.ShotgunIntervalScale, 1e-5f);
            Assert.AreEqual(0.85f, all.ShotgunSpreadScale, 1e-5f);
            Assert.AreEqual(1.3f, all.MissileRadiusScale, 1e-5f);
            Assert.AreEqual(all.ShotgunIntervalScale, all.IntervalScale(GunType.Shotgun));
            Assert.AreEqual(all.VulcanIntervalScale, all.IntervalScale(GunType.Vulcan));
        }

        static string ShippedGameContentJsonPath([CallerFilePath] string here = "")
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
            return Path.Combine(repoRoot, "Assets", "Unity.Hare", "Resources", "Hare", "Content", "GameContent.json");
        }
    }
}
