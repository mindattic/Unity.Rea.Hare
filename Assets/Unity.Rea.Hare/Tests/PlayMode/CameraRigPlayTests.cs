using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Rendering;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T44 (Opus review): proves the main scene's camera rig actually behaves correctly once Unity's own
    /// loop is running, not just that it is wired up at edit time. In particular, a <see cref="SpriteRenderer"/>
    /// added by script defaults to URP's lit sprite material, which would tint or black out the screen
    /// overlay's fade/flash under any Light2D that doesn't reach the Overlay sorting layer; this proves
    /// <c>ScreenOverlayLayer</c> corrects that live, every time it wakes (not just in the committed scene file).
    /// </summary>
    public class CameraRigPlayTests
    {
        const string MainScene = "Assets/Unity.Rea.Hare/Scenes/Rooms/room_gamescreen.unity"; // the arena room (U.HARE-T50: Main.unity is the bootstrap only)

        [UnityTest]
        public IEnumerator MainCameraIsLetterboxedToTheReferenceResolution()
        {
            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(MainScene);
            yield return null; // let Awake/OnEnable/the first LateUpdate run

            var camera = Camera.main;
            Assert.IsNotNull(camera, "Camera.main resolves through the rig's MainCamera tag");
            var letterbox = camera.GetComponent<LetterboxCamera>();
            Assert.IsNotNull(letterbox);

            var expected = LetterboxCamera.ComputeViewportRect(Screen.width, Screen.height, LetterboxCamera.ReferenceAspect);
            Assert.AreEqual(expected, camera.rect, "the live rect matches the pure viewport calculation for this run's screen size");

            var overlay = camera.GetComponentInChildren<ScreenOverlayLayer>();
            Assert.IsNotNull(overlay);
            var renderer = overlay.GetComponent<SpriteRenderer>();
            Assert.AreEqual(HareSortingLayers.Overlay, renderer.sortingLayerName);
            Assert.AreEqual(0f, overlay.Color.a, "idle: fully transparent");
            Assert.IsNotNull(renderer.sharedMaterial);
            StringAssert.DoesNotContain("Lit", renderer.sharedMaterial.shader.name,
                "a screen fade/flash must read its own colour, not one a Light2D can tint or black out");

            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
