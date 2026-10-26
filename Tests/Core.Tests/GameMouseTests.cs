using System.IO;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Pointer;

namespace Unity.Hare.Tests.Core
{
    /// <summary>U.HARE-T61: obj_mouse, as its Create, Step and the rooms' controllers and obj_player's Step set it.</summary>
    public class GameMouseTests
    {
        static MouseTuning Tuning => ContentRegistry.Default.Mouse;

        [Test]
        public void TheTitleMapAndUpgradeRoomsMakeAPointerAndThePlayRoomsTheBlueReticle()
        {
            var title = GameMouse.ForRoom(Tuning, RoomId.Title);
            Assert.That(title.Sprite, Is.EqualTo("spr_pointer"));
            Assert.That(title.Visible, Is.False, "obj_title: mouse.visible = false until obj_logo's Create");
            Assert.That(title.FollowsShip, Is.False);

            foreach (var room in new[] { RoomId.Map, RoomId.Upgrade })
            {
                var pointer = GameMouse.ForRoom(Tuning, room);
                Assert.That(pointer.Sprite, Is.EqualTo("spr_pointer"), room.ToString());
                Assert.That(pointer.Visible, Is.True, room.ToString());
            }

            foreach (var room in new[] { RoomId.GameScreen, RoomId.SideScroll, RoomId.Shaft })
            {
                var reticle = GameMouse.ForRoom(Tuning, room);
                Assert.That(reticle.Sprite, Is.EqualTo("spr_reticle_blue"), room.ToString());
                Assert.That(reticle.Visible, Is.True, room.ToString());
                Assert.That(reticle.FollowsShip, Is.True, room.ToString());
                Assert.That(reticle.Depth, Is.EqualTo(-5000));
                Assert.That(reticle.OnGui, Is.False, "Create: on_gui = false");
            }

            foreach (var room in new[] { RoomId.Startup, RoomId.Intro, RoomId.Story })
                Assert.That(GameMouse.ForRoom(Tuning, room), Is.Null, room + " has no obj_mouse");
            Assert.That(Tuning.ShowSystemCursor, Is.False, "option_showcursor False");
        }

        [Test]
        public void ItSitsOnTheMouseInTheRoomAndInGuiSpaceWhileOnGui()
        {
            var mouse = GameMouse.ForRoom(Tuning, RoomId.GameScreen);
            mouse.Step(1200, 900, 400, 300);
            Assert.That(mouse.X, Is.EqualTo(1200));
            Assert.That(mouse.Y, Is.EqualTo(900));

            mouse.SetOnGui(true); // obj_yesno_dialog's Create
            mouse.Step(1200, 900, 400, 300);
            Assert.That(mouse.X, Is.EqualTo(800), "mouse_x - view_xview");
            Assert.That(mouse.Y, Is.EqualTo(600), "mouse_y - view_yview");
        }

        [Test]
        public void ObjLogoShowsTheTitlesPointer()
        {
            var title = GameMouse.ForRoom(Tuning, RoomId.Title);
            title.SetVisible(true);
            Assert.That(title.Visible, Is.True);
        }

        [Test]
        public void TheShipRecoloursTheReticleGreenInFlightBlueInTheJumpArcRedOutsideIt()
        {
            var mouse = GameMouse.ForRoom(Tuning, RoomId.GameScreen);
            mouse.FollowShip(PlayerFlightState.Moving, withinJumpArc: true);
            Assert.That(mouse.Sprite, Is.EqualTo("spr_reticle_green"));
            mouse.FollowShip(PlayerFlightState.Idle, withinJumpArc: true);
            Assert.That(mouse.Sprite, Is.EqualTo("spr_reticle_blue"));
            mouse.FollowShip(PlayerFlightState.Idle, withinJumpArc: false);
            Assert.That(mouse.Sprite, Is.EqualTo("spr_reticle_red"));
            mouse.FollowShip(PlayerFlightState.Dead, withinJumpArc: true);
            Assert.That(mouse.Sprite, Is.EqualTo("spr_reticle_blue"), "state_dead is not state_moving");
            mouse.FollowShip(PlayerFlightState.Moving, withinJumpArc: false);
            mouse.FollowShip(PlayerFlightState.Leaving, withinJumpArc: false);
            Assert.That(mouse.Sprite, Is.EqualTo("spr_reticle_green"), "if(state != state_leave) skips the block");
        }

        [Test]
        public void TheJumpArcIsJumpArcDegreesEitherSideOfTheFacingAcrossZero()
        {
            float arc = ContentRegistry.Default.Flight.JumpArcDegrees;
            Assert.That(GameMouse.WithinJumpArc(90, 90 + arc, arc), Is.True);
            Assert.That(GameMouse.WithinJumpArc(90, 91 + arc, arc), Is.False);
            Assert.That(GameMouse.WithinJumpArc(10, 350, arc), Is.True, "abs(...) >= 360 - jump_arc");
        }

        [Test]
        public void TheShippedFileMatchesTheCompiledInDefaults()
        {
            var fromFile = ContentRegistry.FromJson(File.ReadAllText(ShippedGameContentJsonPath())).Mouse;
            var defaults = Tuning;
            Assert.That(fromFile.Sprite, Is.EqualTo(defaults.Sprite));
            Assert.That(fromFile.Depth, Is.EqualTo(defaults.Depth));
            Assert.That(fromFile.CanLeapSprite, Is.EqualTo(defaults.CanLeapSprite));
            Assert.That(fromFile.CannotLeapSprite, Is.EqualTo(defaults.CannotLeapSprite));
            Assert.That(fromFile.FlyingSprite, Is.EqualTo(defaults.FlyingSprite));
            Assert.That(fromFile.ShowSystemCursor, Is.EqualTo(defaults.ShowSystemCursor));
            Assert.That(fromFile.Rooms, Has.Length.EqualTo(defaults.Rooms.Length));
            for (int i = 0; i < defaults.Rooms.Length; i++)
            {
                Assert.That(fromFile.Rooms[i].Room, Is.EqualTo(defaults.Rooms[i].Room));
                Assert.That(fromFile.Rooms[i].Sprite, Is.EqualTo(defaults.Rooms[i].Sprite));
                Assert.That(fromFile.Rooms[i].Visible, Is.EqualTo(defaults.Rooms[i].Visible));
                Assert.That(fromFile.Rooms[i].FollowsShip, Is.EqualTo(defaults.Rooms[i].FollowsShip));
            }
        }

        [Test]
        public void ABadMouseSectionIsRefused()
        {
            var e = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{ \"mouse\": { \"Sprite\": \"\", \"Rooms\": [ { \"Room\": \"Map\" }, { \"Room\": \"Map\" } ] } }"));
            Assert.That(e.Message, Does.Contain("mouse.Sprite"));
            Assert.That(e.Message, Does.Contain("listed more than once"));
        }

        static string ShippedGameContentJsonPath([CallerFilePath] string here = "")
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
            return Path.Combine(repoRoot, "Assets", "Unity.Hare", "Resources", "Hare", "Content", "GameContent.json");
        }
    }
}
