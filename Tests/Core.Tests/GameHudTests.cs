using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Hare.Arena;
using Unity.Hare.Content;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// U.HARE-T25: obj_healthbar, obj_weapon_gui, obj_energybar and obj_speedometer, laid out on the 1920x1080 GUI layer
    /// as their Create and Draw GUI events place them, and stepped as their Step and End Step events run.
    /// </summary>
    public class GameHudTests
    {
        static GameHud NewHud() =>
            new GameHud(ContentRegistry.Default.Arena.Hud, ContentRegistry.Default.Health, 1920, 1080);

        static HudReading Flying(float speed = 5f) => new HudReading
        {
            HasPlayer = true,
            State = PlayerFlightState.Moving,
            Speed = speed,
            Energy = 100f,
            MaxEnergy = 100f,
            Health = 6,
            MaxHealth = 6,
            Lives = 3,
            Coins = 0,
            Clock = "00:00",
            AirWeapon = AirWeapon.Rocket,
            GroundPrimaryAmmo = 150, GroundPrimaryMax = 150, GroundSecondaryAmmo = 12, GroundSecondaryMax = 12,
            AirPrimaryAmmo = 20, AirPrimaryMax = 20, AirSecondaryAmmo = 8, AirSecondaryMax = 8,
        };

        static HudReading Perched()
        {
            var r = Flying(0f);
            r.State = PlayerFlightState.Idle;
            return r;
        }

        // ---- obj_healthbar ----

        [Test]
        public void TheHealthBarDrawsLivesClockAndCoinsWhereTheOriginalDoes()
        {
            var r = Flying();
            r.Lives = 2;
            r.Coins = 37;
            r.Clock = "01:05";
            var frame = NewHud().Draw(r);

            var lives = frame.Sprite("extralives").Value;
            Assert.AreEqual("spr_extralives", lives.Sprite);
            Assert.AreEqual(0f, lives.X);
            Assert.AreEqual(1080f - 102f, lives.Y, "view_hview - sprite_get_height(spr_extralives)");

            var livesText = frame.Text("lives").Value;
            Assert.AreEqual("x 2", livesText.Text);
            Assert.AreEqual(64f, livesText.X);
            Assert.AreEqual(1080f - 108f, livesText.Y);
            Assert.AreEqual(HudFont.Score, livesText.Font);

            var clock = frame.Text("clock").Value;
            Assert.AreEqual("01:05", clock.Text, "obj_healthbar's strTime, the one clock");
            Assert.AreEqual(64f, clock.X);
            Assert.AreEqual(1030f, clock.Y);
            Assert.AreEqual(HudFont.Point, clock.Font);

            var coins = frame.Text("coins").Value;
            Assert.AreEqual("37", coins.Text);
            Assert.AreEqual(86f, coins.X);
            Assert.AreEqual(1053f, coins.Y);
            var coin = frame.Sprite("coin").Value;
            Assert.AreEqual("spr_coin_small", coin.Sprite);
            Assert.AreEqual(64f, coin.X);
            Assert.AreEqual(1057f, coin.Y);
        }

        [Test]
        public void OnePipPerPointOfMaxHpFullOrEmpty()
        {
            var r = Flying();
            r.Health = 4;
            var frame = NewHud().Draw(r);
            for (int i = 0; i < 6; i++)
            {
                var pip = frame.Sprite("pip" + i).Value;
                Assert.AreEqual(140f + i * 44f, pip.X, "column2 + i*width");
                Assert.AreEqual(1080f - 64f - 36f, pip.Y, "view_hview - height - 36");
                if (i < 4)
                {
                    Assert.AreEqual("spr_health_pip", pip.Sprite);
                    Assert.IsNull(frame.Sprite("pip" + i + "-back"));
                }
                else
                {
                    Assert.AreEqual("spr_health_pip_empty", pip.Sprite);
                    var back = frame.Sprite("pip" + i + "-back").Value;
                    Assert.AreEqual("spr_health_pip_back", back.Sprite);
                    Assert.AreEqual(0.95f, back.Alpha, 1e-6f);
                }
            }
            Assert.IsNull(frame.Sprite("pip6"));
        }

        [Test]
        public void WithNoShipOnlyTheShipFreePartsAreDrawn()
        {
            var frame = NewHud().Draw(new HudReading { Lives = 3, MaxEnergy = 100f, Clock = "00:00" });
            Assert.IsNotNull(frame.Text("lives"));
            Assert.IsNull(frame.Sprite("pip0"));
            Assert.IsNull(frame.Sprite("weapon-primary"));
            Assert.IsNull(frame.Text("speed"));
        }

        // ---- obj_weapon_gui ----

        [Test]
        public void PerchedTheGroundWeaponsAreShownWithTheSelectBoxOnThePrimary()
        {
            var r = Perched();
            r.GroundWeapon = GroundWeapon.Railgun;
            r.GroundPrimaryAmmo = 40;
            var frame = NewHud().Draw(r);

            var primary = frame.Sprite("weapon-primary").Value;
            Assert.AreEqual("spr_icon_railgun", primary.Sprite, "next_ground_weapon's ground_asset");
            Assert.AreEqual(1920f - 52 * 2 - 20 * 2, primary.X);
            Assert.AreEqual(1080f - 57 - 20, primary.Y);
            var secondary = frame.Sprite("weapon-secondary").Value;
            Assert.AreEqual("spr_icon_shotgun", secondary.Sprite);
            Assert.AreEqual(1920f - 52 - 20, secondary.X);
            Assert.AreEqual(primary.X, frame.Sprite("weapon-select").Value.X);

            var ammo = frame.Text("ammo-primary").Value;
            Assert.AreEqual("40 / 150", ammo.Text);
            Assert.AreEqual(1920f - 52 - 40, ammo.X);
            Assert.AreEqual(1060f, ammo.Y);
            Assert.AreEqual(ResultsAlign.Right, ammo.Align);
            Assert.AreEqual(HudFont.Small, ammo.Font);
            var ammo2 = frame.Text("ammo-secondary").Value;
            Assert.AreEqual("12 / 12", ammo2.Text);
            Assert.AreEqual(1900f, ammo2.X);
        }

        [Test]
        public void CtrlMovesTheSelectBoxToTheSecondary()
        {
            var r = Perched();
            r.ControlHeld = true;
            var frame = NewHud().Draw(r);
            Assert.AreEqual(frame.Sprite("weapon-secondary").Value.X, frame.Sprite("weapon-select").Value.X);
            Assert.AreEqual("spr_icon_vulcan", frame.Sprite("weapon-primary").Value.Sprite, "no gun held: Create's spr_icon_vulcan");
        }

        [Test]
        public void InFlightTheAirWeaponsAndPoolsAreShown()
        {
            var r = Flying();
            r.AirWeapon = AirWeapon.Mine;
            r.AirPrimaryAmmo = 7;
            r.AirSecondaryAmmo = 3;
            var frame = NewHud().Draw(r);
            Assert.AreEqual("spr_icon_mine", frame.Sprite("weapon-primary").Value.Sprite);
            Assert.AreEqual("spr_icon_mine", frame.Sprite("weapon-secondary").Value.Sprite);
            Assert.AreEqual("7 / 20", frame.Text("ammo-primary").Value.Text);
            Assert.AreEqual("3 / 8", frame.Text("ammo-secondary").Value.Text);
        }

        [Test]
        public void DeadOrLeavingTheWeaponGuiDrawsNothing()
        {
            foreach (var state in new[] { PlayerFlightState.Dead, PlayerFlightState.Leaving })
            {
                var r = Flying();
                r.State = state;
                var frame = NewHud().Draw(r);
                Assert.IsNull(frame.Sprite("weapon-primary"), state.ToString());
                Assert.IsNull(frame.Text("ammo-primary"), state.ToString());
            }
        }

        // ---- obj_energybar ----

        [Test]
        public void TheEnergyBarSitsAtColumn2AboveTheBottom()
        {
            var hud = NewHud();
            var frame = hud.Draw(Flying());
            var back = frame.Sprite("energy-back").Value;
            Assert.AreEqual(145f, back.X);
            Assert.AreEqual(1080f - 30f - 3f, back.Y);
            Assert.AreEqual(0.95f, back.Alpha, 1e-6f);
            Assert.AreEqual("#008000", frame.Sprite("energy-fill").Value.Color, "Create's c_green before the first End Step");
            var text = frame.Text("energy").Value;
            Assert.AreEqual("100%", text.Text);
            Assert.AreEqual(145f + 127f, text.X);
            Assert.AreEqual(1047f + 4f, text.Y);
            Assert.AreEqual(ResultsAlign.Center, text.Align);
        }

        [Test]
        public void TheFillScalesAndTurnsColourByThirds()
        {
            var hud = NewHud();
            var r = Flying();
            r.Energy = 80f;
            hud.Step(r);
            var frame = hud.Draw(r);
            Assert.AreEqual(254f * 0.8f, frame.Sprite("energy-fill").Value.Width, 1e-3f);
            Assert.AreEqual("#A89D04", frame.Sprite("energy-fill").Value.Color, "c_lightgreen, globals.gml's (168, 157, 4)");

            r.Energy = 66f;
            hud.Step(r);
            Assert.AreEqual(EnergyShade.Mid, hud.Energy.Shade);
            r.Energy = 33f;
            hud.Step(r);
            Assert.AreEqual(EnergyShade.Low, hud.Energy.Shade);
            Assert.AreEqual("33%", hud.Draw(r).Text("energy").Value.Text);
            r.Energy = 32.2f;
            Assert.AreEqual("33%", hud.Draw(r).Text("energy").Value.Text, "string(ceil(currentValue))");
        }

        [Test]
        public void ThePausedOrDeadShipLeavesTheFillAlone()
        {
            var hud = NewHud();
            var r = Flying();
            r.Energy = 50f;
            r.Paused = true;
            hud.Step(r);
            Assert.AreEqual(1f, hud.Energy.Fraction);
            r.Paused = false;
            r.State = PlayerFlightState.Dead;
            hud.Step(r);
            Assert.AreEqual(1f, hud.Energy.Fraction);
            Assert.AreEqual("50%", hud.Draw(r).Text("energy").Value.Text, "the Draw GUI reads currentValue whatever");
        }

        // ---- obj_speedometer ----

        [Test]
        public void TheSpeedometerShowsSpeedTimesTheCoefficient()
        {
            var hud = NewHud();
            hud.Step(Flying(5f));
            var frame = hud.Draw(Flying(5f));
            var speed = frame.Text("speed").Value;
            Assert.AreEqual("168.95", speed.Text, "5 * 33.79, as string() prints it");
            Assert.AreEqual(960f, speed.X);
            Assert.AreEqual(1020f, speed.Y);
            Assert.AreEqual(ResultsAlign.Right, speed.Align);
            Assert.AreEqual(HudFont.Score, speed.Font);
            var unit = frame.Text("speed-unit").Value;
            Assert.AreEqual(" mph", unit.Text);
            Assert.AreEqual(960f, unit.X);
            Assert.AreEqual(1055f, unit.Y);
            Assert.AreEqual(ResultsAlign.Left, unit.Align);
        }

        [Test]
        public void MetricDividesByTheMileInKilometres()
        {
            var hud = NewHud();
            var r = Flying(10f);
            r.UseMetric = true;
            hud.Step(r);
            Assert.AreEqual(10 * 33.79 / 0.62137, hud.Speedometer.CurrentSpeed, 1e-3);
            Assert.AreEqual(" kph", hud.Speedometer.Unit);
        }

        [Test]
        public void PerchedTheReadingRunsDownThenGoes()
        {
            var hud = NewHud();
            hud.Step(Flying(5f));
            hud.Step(Perched());
            Assert.AreEqual(168.95 * 0.95, hud.Speedometer.CurrentSpeed, 1e-3);
            int steps = 1;
            while (hud.Speedometer.Visible && steps < 1000)
            {
                hud.Step(Perched());
                steps++;
            }
            Assert.IsFalse(hud.Speedometer.Visible);
            Assert.AreEqual(0.0, hud.Speedometer.CurrentSpeed);
            Assert.AreEqual(102, steps, "0.95 a step from 168.95 until not above 1, then 0");
            Assert.IsNull(hud.Draw(Perched()).Text("speed"), "drawn only while CurrentSpeed > 0");
        }

        [Test]
        public void GmlStringPrintsWholeNumbersBareAndTheRestToTwoPlaces()
        {
            Assert.AreEqual("5", GmlText.Real(5));
            Assert.AreEqual("0", GmlText.Real(0));
            Assert.AreEqual("168.95", GmlText.Real(168.95));
            Assert.AreEqual("160.50", GmlText.Real(160.5025));
        }

        [Test]
        public void TheDefaultsMatchTheContentFile()
        {
            var errors = new System.Collections.Generic.List<string>();
            ContentRegistry.Default.Arena.Hud.Validate(errors, "hud");
            Assert.IsEmpty(errors);
        }
    }
}
