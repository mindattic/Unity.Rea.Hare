using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Hare.Arena;
using Unity.Hare.Content;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// obj_flashbomb (U.HARE-T67), obj_player's F: the blinking white flash, the growing circles and the bomb's fade to
    /// destroy, outside Unity. What the sweep hits is presentation's (FlashbombSweep); Core only says when and how far.
    /// </summary>
    public class FlashbombTests
    {
        static readonly PlayerId Seat = new PlayerId(1);

        FlashbombField field;
        FlashbombDefinition d;

        [SetUp]
        public void SetUp()
        {
            d = new FlashbombDefinition();
            field = new FlashbombField(d);
        }

        static void Step(FlashbombField f, int steps)
        {
            for (int i = 0; i < steps; i++) f.Execute(new StepFlashbombs());
        }

        [Test]
        public void Defaults_MatchTheOriginal()
        {
            Assert.AreEqual("spr_white", d.Sprite);
            Assert.AreEqual(-1000, d.Depth);
            Assert.AreEqual(0.8f, d.FlashAlpha);
            Assert.AreEqual(12, d.FlashCount);
            Assert.AreEqual(6, d.FlashRateSteps);
            Assert.AreEqual(10f, d.RadiusGrowth);
            Assert.AreEqual(8f, d.Radius2Growth);
            Assert.AreEqual(6f, d.Radius3Growth);
            Assert.AreEqual(4f, d.Radius4);
            Assert.AreEqual(1f, d.BurstAlpha);
            Assert.AreEqual(0.01f, d.BurstFadePerStep);
            Assert.AreEqual("sfx_flashbomb", d.Sound);
        }

        [Test]
        public void ShippedContent_CarriesTheFlashbombSection()
        {
            var shipped = ContentRegistry.Default.Arena.Flashbomb;
            Assert.IsNotNull(shipped);
            Assert.AreEqual(12, shipped.FlashCount);
            Assert.AreEqual("sfx_flashbomb", shipped.Sound);
        }

        [Test]
        public void SetOff_MakesALitBombAndPlaysTheSound()
        {
            var result = field.Execute(new SetOffFlashbomb(Seat));

            Assert.IsTrue(result.Accepted);
            var setOff = result.Events.OfType<FlashbombSetOff>().Single();
            Assert.AreEqual("sfx_flashbomb", setOff.Sound);
            var bomb = setOff.Bomb;
            Assert.AreEqual(Seat, bomb.Owner);
            Assert.IsTrue(bomb.On);
            Assert.AreEqual(0, bomb.Count);
            Assert.AreEqual(0.8, bomb.ImageAlpha, 1e-6);
            Assert.AreEqual(1.0, bomb.BurstAlpha, 1e-6);
            Assert.AreEqual(6, bomb.Alarm);
            CollectionAssert(new double[] { 0, 0, 0, 0 }, bomb.Radii);
        }

        [Test]
        public void EveryPress_IsAnotherBomb_NoAmmoOrCooldown()
        {
            for (int i = 0; i < 5; i++) Assert.IsTrue(field.Execute(new SetOffFlashbomb(Seat)).Accepted);
            Assert.AreEqual(5, field.Bombs.Count);
            Assert.AreEqual(5, field.Bombs.Select(b => b.Id).Distinct().Count());
        }

        [Test]
        public void Step_GrowsTheCirclesAndSweepsAtTheOuterRadius()
        {
            field.Execute(new SetOffFlashbomb(Seat));
            var result = field.Execute(new StepFlashbombs());

            var bomb = field.Bombs[0];
            CollectionAssert(new double[] { 10, 8, 6, 4 }, bomb.Radii);
            Assert.AreEqual(10.0, result.Events.OfType<FlashbombSwept>().Single().Radius, 1e-9);

            field.Execute(new StepFlashbombs());
            CollectionAssert(new double[] { 20, 16, 12, 4 }, bomb.Radii);
        }

        [Test]
        public void Alarm_BlinksEverySixSteps_DarkPhasesCount()
        {
            field.Execute(new SetOffFlashbomb(Seat));
            var bomb = field.Bombs[0];

            Step(field, 5);
            Assert.AreEqual(0.8, bomb.ImageAlpha, 1e-6);
            Step(field, 1); // alarm 0 at step 6: on -> off, still lit
            Assert.IsFalse(bomb.On);
            Assert.AreEqual(0.8, bomb.ImageAlpha, 1e-6);
            Assert.AreEqual(0, bomb.Count);
            Step(field, 6); // step 12: dark
            Assert.IsTrue(bomb.On);
            Assert.AreEqual(0.0, bomb.ImageAlpha, 1e-6);
            Assert.AreEqual(1, bomb.Count);
            Step(field, 6); // step 18: lit again
            Assert.AreEqual(0.8, bomb.ImageAlpha, 1e-6);
        }

        [Test]
        public void BurstAlpha_FadesToDestroy_BeforeTheTwelfthBlink()
        {
            field.Execute(new SetOffFlashbomb(Seat));
            var bomb = field.Bombs[0];

            int steps = 0;
            FlashbombGone gone = null;
            while (gone == null && steps < 1000)
            {
                gone = field.Execute(new StepFlashbombs()).Events.OfType<FlashbombGone>().SingleOrDefault();
                steps++;
            }

            Assert.IsNotNull(gone);
            Assert.AreSame(bomb, gone.Bomb);
            Assert.IsTrue(bomb.IsGone);
            Assert.AreEqual(0, field.Bombs.Count);
            Assert.That(steps, Is.InRange(100, 102)); // burst_alpha 1 - 0.01 per step, gone the step after it is not above 0
            Assert.Less(bomb.Count, d.FlashCount); // the original's 12 flashes never finish: only 8 dark phases
            Assert.AreEqual(8, bomb.Count);
        }

        [Test]
        public void Step_WithNoBombs_IsRefused()
        {
            Assert.IsFalse(field.Execute(new StepFlashbombs()).Accepted);
        }

        [Test]
        public void Clear_DropsEveryBomb()
        {
            field.Execute(new SetOffFlashbomb(Seat));
            field.Execute(new SetOffFlashbomb(Seat));
            field.Clear();
            Assert.AreEqual(0, field.Bombs.Count);
        }

        [Test]
        public void Validate_RejectsBadNumbers()
        {
            var bad = new FlashbombDefinition { FlashAlpha = 0f, FlashCount = 0, FlashRateSteps = 0, RadiusGrowth = 0f, BurstFadePerStep = 0f, Sound = "" };
            var errors = new System.Collections.Generic.List<string>();
            bad.Validate(errors, "arena.Flashbomb");
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(6));

            errors.Clear();
            new FlashbombDefinition().Validate(errors, "arena.Flashbomb");
            Assert.IsEmpty(errors);
        }

        static void CollectionAssert(double[] expected, double[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], actual[i], 1e-9, "radius " + (i + 1));
        }
    }
}
