using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Hare.Arena;
using Unity.Hare.Audio;
using Unity.Hare.Flow;
using Unity.Hare.Session;
using Unity.Hare.Weapons;
using Unity.Shared.Core;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T60, played with real key presses through HareControls' Meta actions: obj_game's P (pause, not once the
    /// level is complete), R (reset_player and setup_level), U (units, saved), F11 (+100 score), F12 (debug overlay),
    /// the held keypad +/- (master volume in 0.01 steps, saved on release; silent and never saving the forced mute
    /// while the CLI's session mute is on) and the room flow's Esc (to the title).
    /// </summary>
    public class GameKeysPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        string root;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            root = Path.Combine(Path.GetTempPath(), "UnityHareGameKeysTest_" + Guid.NewGuid().ToString("N"));
            Profiles.UseRoot(root);
            Profiles.SessionMuted = false;
            Gml.Paused = false;
        }

        public override void TearDown()
        {
            Profiles.SessionMuted = false;
            HareAudio.MasterVolume = 1f;
            var mixer = HareAudio.Manifest != null ? HareAudio.Manifest.Mixer : null;
            if (mixer != null) mixer.ClearFloat(HareAudioManifest.MasterVolumeParameter);
            Profiles.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Gml.Paused = false;
            base.TearDown();
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.GetSceneByName("GameKeysPlayTests.Empty");
            if (!empty.IsValid()) empty = SceneManager.CreateScene("GameKeysPlayTests.Empty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty) yield return SceneManager.UnloadSceneAsync(scene);
            }
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

        static ArenaRoom Arena() => UnityEngine.Object.FindAnyObjectByType<ArenaRoom>();

        static RoomFlowDirector Director => RoomFlowDirector.Active;

        static IEnumerator Enter(RoomId room)
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
            string name = RoomList.GmlName(room);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
        }

        IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PPausesAndResumes_ButNotOnceTheLevelIsComplete()
        {
            yield return Enter(RoomId.GameScreen);
            var session = Director.Session;
            Assert.That(session.IsPaused, Is.False);

            yield return Tap(keyboard.pKey);
            Assert.That(session.IsPaused, Is.True, "paused = !paused");
            Assert.That(Gml.Paused, Is.True, "every moving object reads the runtime's paused flag");
            var ship = Arena().Player.transform.position;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Arena().Player.transform.position, Is.EqualTo(ship), "the ship holds still while paused");

            yield return Tap(keyboard.pKey);
            Assert.That(session.IsPaused, Is.False);
            Assert.That(Gml.Paused, Is.False);

            // set_level_complete pauses under the results panel: P leaves it paused (only Enter goes on).
            Director.ExecuteSession(new CompleteLevel(RoomFlowDirector.Seat));
            Assert.That(Arena().Results, Is.Not.Null);
            Assert.That(Gml.Paused, Is.True);
            yield return Tap(keyboard.pKey);
            Assert.That(session.IsPaused, Is.True, "if(!level_complete)");
            Assert.That(Gml.Paused, Is.True);
            Assert.That(Arena().Results, Is.Not.Null, "the results panel is still up");
        }

        [UnityTest]
        public IEnumerator RResetsThePlayerAndSetsTheLevelUpAgain()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            int level = Director.Session.Level;
            arena.Player.Land();
            var world = arena.ToWorld(3000f, 1800f);
            arena.Player.transform.position = new Vector3(world.x, world.y, arena.Player.transform.position.z);
            arena.Player.GetComponent<Rigidbody2D>().position = world;
            yield return Tap(keyboard.pKey); // R also lifts a pause: setup_level's paused = false
            Assert.That(Gml.Paused, Is.True);
            var oldState = arena.State;

            yield return Tap(keyboard.rKey);
            Assert.That(arena.State, Is.Not.SameAs(oldState), "setup_level built the level again");
            Assert.That(Director.Session.Level, Is.EqualTo(level), "the same level");
            Assert.That(Gml.Paused, Is.False, "setup_level: paused = false");
            Assert.That(arena.Banner, Is.Not.Null, "a new obj_levelstart");
            var at = arena.PlayerRoomPosition();
            Assert.That(at.x, Is.EqualTo(1920f).Within(60f), "reset_player: x = room_width / 2");
            Assert.That(at.y, Is.LessThan(100f), "dropping in from just above the room");
        }

        [UnityTest]
        public IEnumerator UF11AndF12_SwitchUnits_Add100_AndShowTheDebugOverlay()
        {
            yield return Enter(RoomId.GameScreen);
            var session = Director.Session;
            Assert.That(session.UseMetric, Is.False, "a new profile reads imperial");

            yield return Tap(keyboard.uKey);
            Assert.That(session.UseMetric, Is.True, "use_metric = !use_metric");
            Assert.That(new HareSettings(new ProfileStore(root).Current.Settings.Game).UseMetric, Is.True, "saved to the profile");
            Assert.That(Arena().HudView, Is.Not.Null);

            int score = session.Score;
            yield return Tap(keyboard.f11Key);
            Assert.That(session.Score, Is.EqualTo(score + 100), "current_score += 100");

            Assert.That(session.Debug, Is.False);
            yield return Tap(keyboard.f12Key);
            Assert.That(session.Debug, Is.True, "debug = !debug");
            yield return Tap(keyboard.f12Key);
            Assert.That(session.Debug, Is.False);

            yield return Tap(keyboard.uKey);
            Assert.That(session.UseMetric, Is.False);
        }

        [UnityTest]
        public IEnumerator TheKeypadMovesTheMasterVolume_AndSavesItOnRelease()
        {
            yield return Enter(RoomId.GameScreen);
            var session = Director.Session;
            Assert.That(session.MasterVolume, Is.EqualTo(1f), "a new profile's master volume");

            Press(keyboard.numpadMinusKey, queueEventOnly: true);
            yield return WaitFor(() => session.MasterVolume <= 0.9f, "master_volume -= 0.01 every held step");
            Assert.That(HareAudio.MasterVolume, Is.EqualTo(session.MasterVolume), "sound_global_volume(master_volume)");
            Assert.That(Level(), Is.EqualTo(MixerLevels.ToDecibels(HareAudio.MasterVolume)).Within(1e-3f));
            Release(keyboard.numpadMinusKey, queueEventOnly: true);
            yield return null;
            yield return null;
            float down = session.MasterVolume;
            Assert.That(Saved().MasterVolume, Is.EqualTo(down).Within(1e-4f), "saved once the key is let go");
            Assert.That(Math.Round(down * 100) / 100, Is.EqualTo(down).Within(1e-4f), "in 0.01 steps");

            Press(keyboard.numpadPlusKey, queueEventOnly: true);
            yield return WaitFor(() => session.MasterVolume >= 1f, "master_volume += 0.01 up to 1");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(session.MasterVolume, Is.EqualTo(1f), "held within 0..1");
            Release(keyboard.numpadPlusKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Assert.That(Saved().MasterVolume, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator UnderTheCliMuteTheGameStaysSilent_AndNeverSavesTheForcedMute()
        {
            // The profile's real values, saved before the muted run.
            var profile = ActiveProfile.Current;
            profile.Settings.Common.MusicVolume = 0.5f;
            profile.Settings.Common.SfxVolume = 0.25f;
            profile.Settings.Common.MuteMusic = false;
            profile.Settings.Common.MuteSfx = false;
            new HareSettings(profile.Settings.Game).MasterVolume = 0.8f;
            profile.SaveSettings();

            Profiles.SessionMuted = true; // tools/play.ps1's -muted
            yield return Enter(RoomId.GameScreen);
            var session = Director.Session;
            Assert.That(session.MasterVolume, Is.EqualTo(0.8f).Within(1e-4f), "the saved master volume, not the mute's 0");
            HareAudio.ApplyProfile();
            Assert.That(Level(), Is.EqualTo(MixerLevels.SilentDecibels).Within(1e-3f), "silent while muted");

            Press(keyboard.numpadPlusKey, queueEventOnly: true);
            yield return WaitFor(() => session.MasterVolume >= 0.85f, "the keypad still moves master_volume");
            Assert.That(Level(), Is.EqualTo(MixerLevels.SilentDecibels).Within(1e-3f), "still silent while muted");
            Release(keyboard.numpadPlusKey, queueEventOnly: true);
            yield return null;
            yield return null;

            var saved = new ProfileStore(root).Current.Settings;
            Assert.That(new HareSettings(saved.Game).MasterVolume, Is.EqualTo(session.MasterVolume).Within(1e-4f),
                "the player's own master volume moved from its real value");
            Assert.That(saved.Common.Data.GetFloat(CommonSettings.Keys.MusicVolume, -1f), Is.EqualTo(0.5f).Within(1e-4f), "no forced 0 music volume saved");
            Assert.That(saved.Common.Data.GetFloat(CommonSettings.Keys.SfxVolume, -1f), Is.EqualTo(0.25f).Within(1e-4f), "no forced 0 sfx volume saved");
            Assert.That(saved.Common.Data.GetBool(CommonSettings.Keys.MuteMusic, true), Is.False, "no forced music mute saved");
            Assert.That(saved.Common.Data.GetBool(CommonSettings.Keys.MuteSfx, true), Is.False, "no forced sfx mute saved");

            Profiles.SessionMuted = false;
            HareAudio.ApplyProfile();
            Assert.That(Level(), Is.EqualTo(MixerLevels.ToDecibels(session.MasterVolume)).Within(1e-3f), "audible again once unmuted");
        }

        [UnityTest]
        public IEnumerator EscGoesToTheTitle()
        {
            yield return Enter(RoomId.GameScreen);
            yield return Tap(keyboard.escapeKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == RoomList.GmlName(RoomId.Title), "room_goto(room_title)");
        }

        HareSettings Saved() => new HareSettings(new ProfileStore(root).Current.Settings.Game);

        static float Level()
        {
            Assert.IsTrue(HareAudio.Manifest.Mixer.GetFloat(HareAudioManifest.MasterVolumeParameter, out float db));
            return db;
        }
    }
}
