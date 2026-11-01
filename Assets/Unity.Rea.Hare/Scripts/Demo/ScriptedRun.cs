using System.Collections;
using MindAttic.Capture.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Session;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Demo
{
    /// <summary>
    /// U.HARE-T70: plays the whole game once through the same commands the rooms use (the flow's GoToRoom, the
    /// session's ChooseCampaign, CompleteLevel...) and takes a named screenshot of every room and mode: startup,
    /// title, story, map, intro, a level, a kill, the level-complete results, the side scroll, the shaft and the
    /// garage. Exits with code 1 when a room never loads or a shot fails, so a script can gate on it. Without
    /// <c>-capture</c> it still plays, it is just not recorded.
    /// </summary>
    public sealed class ScriptedRun : MonoBehaviour
    {
        const float RoomTimeout = 20f;
        bool failed;

        IEnumerator Start()
        {
            bool capturing = CaptureUnity.Args != null && CaptureUnity.Args.Any;
            if (capturing)
            {
                float wait = 0f;
                while (!CaptureUnity.Engine.Enabled && wait < 15f)
                {
                    wait += Time.unscaledDeltaTime;
                    yield return null;
                }
                capturing = CaptureUnity.Engine.Enabled;
                if (!capturing)
                {
                    Debug.LogError("[Unity.Rea.Hare] MindAttic.Capture did not start; the run will not be captured.");
                    failed = true;
                }
            }

            float until = Time.realtimeSinceStartup + RoomTimeout;
            while (RoomFlowDirector.Active == null || RoomFlowDirector.Active.Flow.Current == null)
            {
                if (Time.realtimeSinceStartup > until) { Fail("the bootstrap never started the room flow"); Application.Quit(1); yield break; }
                yield return null;
            }
            var director = RoomFlowDirector.Active;

            yield return Settle(director, RoomId.Startup);
            yield return Shot(capturing, "01-startup");

            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Title));
            yield return Settle(director, RoomId.Title);
            yield return new WaitForSecondsRealtime(1.5f); // the menu rises once the logo is up
            yield return Shot(capturing, "02-title");

            director.ExecuteSession(new ChooseCampaign(RoomFlowDirector.Seat));
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Story));
            yield return Settle(director, RoomId.Story);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot(capturing, "03-story");

            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Map));
            yield return Settle(director, RoomId.Map);
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot(capturing, "04-map");

            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Intro));
            yield return Settle(director, RoomId.Intro);
            yield return new WaitForSecondsRealtime(3f);
            yield return Shot(capturing, "05-intro");

            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.GameScreen));
            yield return Settle(director, RoomId.GameScreen);
            var arena = FindAnyObjectByType<ArenaRoom>();
            until = Time.realtimeSinceStartup + RoomTimeout;
            while (arena == null || arena.State == null || !arena.State.Created || arena.Player == null)
            {
                if (Time.realtimeSinceStartup > until) { Fail("the level never set up"); Application.Quit(1); yield break; }
                arena = FindAnyObjectByType<ArenaRoom>();
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot(capturing, "06-level-start");

            int kills = director.Session.Kills;
            var spawned = EnemyDirector.Execute(new SpawnEnemy(EnemyKind.Dagger, new Vec2(0f, 0f)));
            if (spawned.Body != null) EnemyDirector.Execute(new StrikeBeam(spawned.Body.Id, BeamKind.Laser));
            yield return new WaitForSecondsRealtime(0.2f);
            if (director.Session.Kills != kills + 1) Fail("a laser kill did not score");
            yield return Shot(capturing, "07-kill");

            director.ExecuteSession(new CompleteLevel(RoomFlowDirector.Seat));
            yield return new WaitForSecondsRealtime(0.5f);
            if (arena.Results == null) Fail("completing the level did not show the results");
            yield return Shot(capturing, "08-level-results");
            Gml.Paused = false;

            foreach (var room in new[] { RoomId.SideScroll, RoomId.Shaft, RoomId.Upgrade })
            {
                director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
                yield return Settle(director, room);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot(capturing, "09-" + RoomList.GmlName(room));
            }

            Debug.Log("[Unity.Rea.Hare] ScriptedRun finished" + (failed ? " with failures." : "."));
            Application.Quit(failed ? 1 : 0);
        }

        // Waits for the flow to reach the room, its scene to load and its fade from black to finish.
        IEnumerator Settle(RoomFlowDirector director, RoomId room)
        {
            float until = Time.realtimeSinceStartup + RoomTimeout;
            string name = RoomList.GmlName(room);
            while (director.Flow.Current != room || SceneManager.GetActiveScene().name != name
                || (director.Flow.Fade != null && director.Flow.Fade.Mode != FadeMode.Off))
            {
                if (Time.realtimeSinceStartup > until) { Fail(name + " never came up"); yield break; }
                yield return null;
            }
            yield return null;
        }

        IEnumerator Shot(bool capturing, string tag)
        {
            if (!capturing) yield break;
            var shot = CaptureUnity.Engine.ScreenshotAsync(tag);
            while (!shot.IsCompleted) yield return null;
            if (shot.Result.Succeeded) Debug.Log("[Unity.Rea.Hare] Captured " + shot.Result.PrimaryFile);
            else Fail("the " + tag + " shot failed");
        }

        void Fail(string why)
        {
            failed = true;
            Debug.LogError("[Unity.Rea.Hare] ScriptedRun: " + why);
        }
    }
}
