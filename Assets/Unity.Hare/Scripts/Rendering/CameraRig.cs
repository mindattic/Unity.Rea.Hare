using UnityEngine;

namespace Unity.Hare.Rendering
{
    /// <summary>
    /// The 2D camera rig (U.HARE-T44): a full-screen "bars" camera that clears the whole window solid black so
    /// <see cref="LetterboxCamera"/>'s pillarbox/letterbox bars on the game camera never show stale pixels, the
    /// game camera itself (orthographic, constrained to the original's 1920x1080 reference aspect), and the
    /// screen-fade/flash overlay (<see cref="ScreenOverlayLayer"/>) riding along with it. Built in code like
    /// every other object <c>ProjectSetup</c> composes; nothing here references URP, so a pipeline change never
    /// touches this file. URP's own per-camera data is added separately by the Editor-only seam in
    /// <c>Unity.Hare.EditorTools.Rendering</c>, the only code that references UnityEngine.Rendering.Universal.
    /// </summary>
    public static class CameraRig
    {
        /// <summary>The rig's two cameras, in render order (<see cref="Bars"/> first, <see cref="Main"/> on top).</summary>
        public readonly struct Cameras
        {
            public readonly Camera Bars, Main;
            public Cameras(Camera bars, Camera main) { Bars = bars; Main = main; }
        }

        /// <summary>
        /// Builds the rig at <paramref name="position"/> (the game camera's own transform; the bars camera sits
        /// alongside it) with the game camera's <paramref name="orthographicSize"/> and
        /// <paramref name="backdrop"/> clear colour, tagged <c>MainCamera</c> and carrying the project's one
        /// <see cref="AudioListener"/> as before this ticket.
        /// </summary>
        public static Cameras Build(Vector3 position, float orthographicSize, Color backdrop)
        {
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.position = Vector3.zero;

            var barsGo = new GameObject("Letterbox Bars");
            barsGo.transform.SetParent(rigGo.transform, false);
            var bars = barsGo.AddComponent<Camera>();
            bars.orthographic = true;
            bars.clearFlags = CameraClearFlags.SolidColor;
            bars.backgroundColor = Color.black;
            bars.cullingMask = 0;
            bars.depth = -100;

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetParent(rigGo.transform, false);
            cameraGo.transform.SetPositionAndRotation(position, Quaternion.identity);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backdrop;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            camera.depth = 0;
            cameraGo.AddComponent<LetterboxCamera>();
            cameraGo.AddComponent<AudioListener>();
            ScreenOverlayLayer.Create(cameraGo.transform);

            return new Cameras(bars, camera);
        }
    }
}
