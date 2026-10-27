using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Map;
using Unity.Hare.Popups;
using Unity.Hare.Session;
using Unity.Hare.Story;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T19, played: room_map comes up with obj_map over bg_nightsky, the earth, the ship, a marker for each of the
    /// ten missions (the locked boss's grey), the Upgrades button and the "Mission Command" tip; D turns the earth to the
    /// next mission, which shows its panel and slides the comm box in; any key sends the white dot to obj_destination
    /// and its arrival takes the campaign to room_intro with that mission kept; Esc goes to room_title; and a won mission
    /// is saved to campaign.json. With a graphics device the map is rendered to Screenshots/map.png.
    /// </summary>
    public class MapRoomPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        Func<bool> getVisible;
        Action<bool> setVisible;
        string campaignFile;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            getVisible = SystemCursor.GetVisible;
            setVisible = SystemCursor.SetVisible;
            bool cursor = true;
            SystemCursor.GetVisible = () => cursor;
            SystemCursor.SetVisible = value => cursor = value;
            campaignFile = Path.Combine(Path.GetTempPath(), "hare-campaign-" + Guid.NewGuid().ToString("N") + ".json");
            CampaignStore.PathOverride = campaignFile; // never the player's own profile
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            yield return null;
            SystemCursor.Restore();
            SystemCursor.GetVisible = getVisible;
            SystemCursor.SetVisible = setVisible;
            CampaignStore.PathOverride = null;
            if (File.Exists(campaignFile)) File.Delete(campaignFile);
            yield return PlayModeRooms.TearDown();
        }

        static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 5f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        static MapRoom Map() => UnityEngine.Object.FindAnyObjectByType<MapRoom>();

        static IEnumerator EnterMap()
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Map));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_map" && Map() != null && Map().State.Created,
                "room_map and its Create");
        }

        static string Drawn(VisualElement outline)
        {
            var label = outline.Q<Label>();
            return label != null ? label.text : null;
        }

        [UnityTest]
        public IEnumerator TheMapComesUpWithTenMissionsTheButtonAndTheTip()
        {
            yield return EnterMap();
            var map = Map();
            Assert.That(map.name, Is.EqualTo(MapRoom.ObjectName));
            Assert.That(map.Background.sprite.texture.name, Is.EqualTo("bg_nightsky"));
            Assert.That(map.Earth.sprite.name, Does.StartWith("spr_earthball"));
            Assert.That(map.Ship.sprite.name, Does.StartWith("spr_uss_warren"));
            Assert.That(map.Scanner.sprite.name, Does.StartWith("spr_scanner_beam"), "obj_scanner (U.HARE-T63)");
            Assert.That(map.Scanner.sortingOrder, Is.EqualTo(map.Earth.sortingOrder), "depth 0, as the earth");
            Assert.That(map.Scanner.transform.position.z, Is.LessThan(map.Earth.transform.position.z), "made after the earth, drawn over it");
            var beam = map.ToRoom(map.Scanner.transform.position);
            Assert.That(beam.x, Is.EqualTo(1045f).Within(0.01f));
            Assert.That(beam.y, Is.EqualTo(388f).Within(0.01f));
            Assert.That(map.Scanner.color.a, Is.InRange(0.16f - 1e-4f, 0.24f + 1e-4f), "alpha 0.2, then the 0.16-0.24 flicker");
            Assert.That(map.Markers.Count, Is.EqualTo(10), "a marker per original level (BIBLE V42)");
            var boss = map.Markers[7];
            Assert.That(boss.Marker.ScenarioId, Is.EqualTo(8));
            Assert.That(boss.Renderer.color.r, Is.LessThan(0.6f), "the locked boss is drawn grey");
            Assert.That(map.Markers[0].Renderer.color, Is.EqualTo(Color.white));
            Assert.That(map.WhiteDot.enabled, Is.False);
            Assert.That(Drawn(map.UpgradeCaption), Is.EqualTo("Upgrades"));
            Assert.That(map.UpgradeMessage.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(map.Panel.style.display.value, Is.EqualTo(DisplayStyle.None), "obj_scenario_details starts hidden");
            Assert.That(map.Tip, Is.Not.Null, "obj_map's Create makes the tip");
            Assert.That(map.Tip.Tip.Text, Does.StartWith("Mission Command#"));
            Assert.That(map.Conversation.Box.Mode, Is.EqualTo(ConversationMode.Off));
            Assert.That(map.Pointer, Is.Not.Null, "obj_map's Create makes obj_mouse with spr_pointer");
            Assert.That(RoomFlowDirector.Active.Session.Scenario, Is.EqualTo(-1), "D24: no old scenario");

            yield return WaitFor(() => map.State.EarthAngle > 0d, "the earth's slow turn");
            Assert.That(map.Earth.transform.eulerAngles.z, Is.EqualTo((float)map.State.EarthAngle).Within(0.01f));

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                yield return TitleRoomPlayTests.Render(map, 1920, 1080, "map.png", (world, ui) =>
                {
                    Assert.That(TitleRoomPlayTests.Count(world, 1920, 1080, 500, 1400, 600, 1080, p => p.b > 80 || p.g > 80), Is.GreaterThan(100000),
                        "the earth along the bottom");
                    Assert.That(TitleRoomPlayTests.Count(ui, 1920, 1080, 30, 170, 25, 55, p => p.a > 200), Is.GreaterThan(100), "\"Upgrades\" at 100,30");
                });
        }

        [UnityTest]
        public IEnumerator DTurnsToTheNextMission_ShowingItsPanelAndTheCommBox()
        {
            yield return EnterMap();
            var map = Map();
            var director = RoomFlowDirector.Active;
            yield return Tap(keyboard.dKey);
            Assert.That(map.State.Mode, Is.EqualTo(MapMode.Moving));
            yield return WaitFor(() => map.State.CurrentScenario == 6, "the 83 degree mission under obj_destination");
            Assert.That(map.State.Mode, Is.EqualTo(MapMode.Idle));
            Assert.That(director.Session.Scenario, Is.EqualTo(6), "current_scenario = other.scenario_id");
            yield return null;
            Assert.That(map.Panel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Drawn(map.PanelTextElement), Is.EqualTo("Scenario 6\nWraiths"));
            yield return WaitFor(() => map.Conversation.Box.Mode == ConversationMode.Idle, "the comm box sliding in");
        }

        [UnityTest]
        public IEnumerator AKeySendsTheWhiteDotAndItsArrivalLaunchesTheMissionInRoomIntro()
        {
            yield return EnterMap();
            var map = Map();
            var director = RoomFlowDirector.Active;
            yield return Tap(keyboard.dKey);
            yield return WaitFor(() => map.State.CurrentScenario == 6 && map.State.Mode == MapMode.Idle, "a mission");
            yield return Tap(keyboard.spaceKey);
            Assert.That(map.State.Dot, Is.Not.Null, "obj_white_dot leaves the ship");
            yield return null;
            Assert.That(map.WhiteDot.enabled, Is.True);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_intro", "the dot's arrival: room_goto(room_intro)", 10f);
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Campaign));
            Assert.That(director.Session.Scenario, Is.EqualTo(6));
            Assert.That(director.Session.MissionLevel, Is.EqualTo(6), "the mission is kept (BIBLE V42)");
            var intro = UnityEngine.Object.FindAnyObjectByType<Unity.Hare.Rooms.IntroRoom>();
            yield return WaitFor(() => intro != null && intro.State != null, "obj_intro");
            Assert.That(intro.State.MissionScenario, Is.EqualTo(6));
            Assert.That(intro.State.MissionLevel, Is.EqualTo(6));
        }

        [UnityTest]
        public IEnumerator EscGoesToTheTitle()
        {
            yield return EnterMap();
            yield return Tap(keyboard.escapeKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "obj_map's Esc: room_goto(room_title)");
        }

        [UnityTest]
        public IEnumerator AWonMissionIsSavedToCampaignJson()
        {
            yield return EnterMap();
            var director = RoomFlowDirector.Active;
            Assert.That(director.Campaign.Won, Is.Empty, "a new save");
            director.ExecuteSession(new ChooseMission(RoomFlowDirector.Seat, 3, 3));
            director.ExecuteSession(new CompleteLevel(RoomFlowDirector.Seat));
            Assert.That(director.Campaign.HasWon(3), Is.True);
            Assert.That(File.Exists(campaignFile), Is.True);
            Assert.That(CampaignProgress.FromJson(File.ReadAllText(campaignFile)).Won.ToArray(), Is.EqualTo(new[] { 3 }));
            Assert.That(CampaignStore.Load().HasWon(3), Is.True, "read back as the next run would");
        }
    }
}
