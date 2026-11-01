using UnityEngine;

namespace Unity.Rea.Hare.Rendering
{
    /// <summary>
    /// Keeps the game camera at the original's 1920x1080 reference aspect whatever the window is resized to
    /// (U.HARE-T44): pillarbox bars at the sides on a wider window, letterbox bars top and bottom on a
    /// narrower one, computed into the camera's viewport <see cref="Camera.rect"/> so Unity scales the
    /// rendered image itself. <see cref="CameraRig"/> pairs this with a full-screen "bars" camera so the area
    /// outside the rect is always solid black rather than whatever the previous frame left there. Plain
    /// <see cref="UnityEngine.Camera"/> API only, so this has nothing to do with which render pipeline is
    /// active; URP's own per-camera setup is the Editor-only seam in <c>Unity.Rea.Hare.EditorTools.Rendering</c>.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class LetterboxCamera : MonoBehaviour
    {
        /// <summary>room_title/room_map/the other menu rooms' own resolution (docs/RECONSTRUCTION.md).</summary>
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;
        public const float ReferenceAspect = ReferenceWidth / ReferenceHeight;

        Camera targetCamera;
        int lastScreenWidth = -1, lastScreenHeight = -1;
        Rect? port;

        /// <summary>
        /// The view port this camera draws into, in the 1920x1080 reference window's pixels with y from the top (a room
        /// view's <c>xport</c>/<c>yport</c>/<c>wport</c>/<c>hport</c>); null, the default, for the whole window.
        /// </summary>
        public Rect? Port => port;

        void Awake() => targetCamera = GetComponent<Camera>();

        /// <summary>Draws this camera into <paramref name="portPixels"/> of the letterboxed window from now on (the minimap's view 1).</summary>
        public void SetPort(Rect portPixels)
        {
            port = portPixels;
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            Apply(force: true);
        }

        void OnEnable() => Apply(force: true);

        void Update() => Apply(force: false);

        void Apply(bool force)
        {
            if (!force && Screen.width == lastScreenWidth && Screen.height == lastScreenHeight) return;
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            var rect = ComputeViewportRect(lastScreenWidth, lastScreenHeight, ReferenceAspect);
            if (port.HasValue) rect = ComputePortRect(rect, port.Value);
            targetCamera.rect = rect;
        }

        /// <summary>
        /// The viewport rect (0..1) of a view port <paramref name="portPixels"/> (reference pixels, y from the top, as
        /// GameMaker places a view's port) inside the letterboxed <paramref name="viewport"/>: Unity's viewport y grows
        /// upward from the bottom. Pure, like <see cref="ComputeViewportRect"/>.
        /// </summary>
        public static Rect ComputePortRect(Rect viewport, Rect portPixels)
        {
            float x = viewport.x + viewport.width * (portPixels.x / ReferenceWidth);
            float y = viewport.y + viewport.height * ((ReferenceHeight - portPixels.y - portPixels.height) / ReferenceHeight);
            return new Rect(x, y, viewport.width * (portPixels.width / ReferenceWidth), viewport.height * (portPixels.height / ReferenceHeight));
        }

        /// <summary>
        /// The viewport rect (0..1) that centres a <paramref name="referenceAspect"/> composition inside a
        /// <paramref name="screenWidth"/> x <paramref name="screenHeight"/> window: pillarbox (narrower rect,
        /// full height) when the window is wider than the reference, letterbox (full width, shorter rect) when
        /// it is narrower, and the whole screen when the aspect already matches. Pure and side-effect free so
        /// it is testable without a live <see cref="Screen"/> or <see cref="Camera"/>.
        /// </summary>
        public static Rect ComputeViewportRect(float screenWidth, float screenHeight, float referenceAspect)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || referenceAspect <= 0f) return new Rect(0f, 0f, 1f, 1f);
            float windowAspect = screenWidth / screenHeight;
            if (Mathf.Approximately(windowAspect, referenceAspect)) return new Rect(0f, 0f, 1f, 1f);

            if (windowAspect > referenceAspect)
            {
                float widthFraction = referenceAspect / windowAspect;
                return new Rect((1f - widthFraction) * 0.5f, 0f, widthFraction, 1f);
            }

            float heightFraction = windowAspect / referenceAspect;
            return new Rect(0f, (1f - heightFraction) * 0.5f, 1f, heightFraction);
        }
    }
}
