using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Hare.Flow;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// The one teardown every PlayMode class that may load a room shares: destroys the room flow, parks the
    /// active scene on a single empty scene and unloads everything else. Two traps it avoids: creating the empty
    /// scene twice throws (a skipped test never loaded a room over the last teardown's), and the test runner's own
    /// InitTestScene never finishes unloading (a test that loaded no room has not replaced it), which hung the run.
    /// </summary>
    public static class PlayModeRooms
    {
        const string EmptyName = "PlayModeRooms.Empty";

        public static IEnumerator TearDown()
        {
            if (RoomFlowDirector.Active != null) Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.GetSceneByName(EmptyName);
            if (!empty.IsValid()) empty = SceneManager.CreateScene(EmptyName);
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && !scene.name.StartsWith("InitTestScene")) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
