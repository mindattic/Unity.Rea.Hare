using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Rendering;

namespace Unity.Hare.UI
{
    /// <summary>
    /// Keeps a <see cref="HarePanel.BuildRoot"/> stage confined to the screen's visible, non-letterboxed area:
    /// the PanelSettings' MatchWidthOrHeight axis is locked to whichever one <see cref="LetterboxCamera"/> is also
    /// matching (match height, 1, when the window is wider than 16:9 and pillarboxed; match width, 0, when it is
    /// narrower and letterboxed), and the stage — always exactly 1920x1080 logical pixels — is re-centred inside
    /// whatever larger logical area that match axis produces on the limiting axis. Both calculations
    /// (<see cref="ComputeMatch"/>, <see cref="ComputeStageRect"/>) are pure and testable, the same way
    /// <see cref="LetterboxCamera.ComputeViewportRect"/> is for the game camera. Attach alongside the
    /// <see cref="UIDocument"/> the panel belongs to, after <see cref="HarePanel.BuildRoot"/> has run.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HareUiFit : MonoBehaviour
    {
        UIDocument document;
        VisualElement stage;
        int lastWidth = -1, lastHeight = -1;

        void Awake() => document = GetComponent<UIDocument>();

        void OnEnable() => Apply(force: true);

        void Update() => Apply(force: false);

        void Apply(bool force)
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) return;

            // BuildRoot's stage may not exist yet the first time this runs (nothing stops a caller attaching this
            // component before calling BuildRoot, whatever the doc comment asks for): keep looking every frame
            // until it's found, rather than giving up for good the moment this Apply happens to run too early.
            // Finding it for the first time counts as "something changed", same as a screen resize, so the stage
            // gets positioned immediately instead of waiting for the next resize to notice it now exists.
            bool stageJustFound = false;
            if (stage == null)
            {
                stage = document.rootVisualElement?.Q(name: HarePanel.StageName);
                stageJustFound = stage != null;
            }

            bool sizeChanged = Screen.width != lastWidth || Screen.height != lastHeight;
            if (!force && !sizeChanged && !stageJustFound) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            if (document.panelSettings != null) document.panelSettings.match = ComputeMatch(lastWidth, lastHeight);
            if (stage == null) return;

            var rect = ComputeStageRect(lastWidth, lastHeight);
            stage.style.left = rect.x;
            stage.style.top = rect.y;
            stage.style.width = rect.width;
            stage.style.height = rect.height;
        }

        /// <summary>1 (match height) when the window is wider than the 1920x1080 reference aspect and so
        /// pillarboxed, 0 (match width) when it is narrower and letterboxed, 0 for a degenerate screen size.</summary>
        public static float ComputeMatch(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) return 0f;
            return screenWidth / screenHeight > LetterboxCamera.ReferenceAspect ? 1f : 0f;
        }

        /// <summary>
        /// Where the 1920x1080 stage sits (in the panel's own logical units) once <see cref="ComputeMatch"/> has
        /// picked an axis: with ScaleWithScreenSize/MatchWidthOrHeight, the panel's logical size on the matched
        /// axis is pinned to the reference size and the other axis grows with the screen's actual aspect, so the
        /// stage is centred inside that spare room on the growing axis — exactly where <see cref="LetterboxCamera"/>
        /// draws the game camera's own bars.
        /// </summary>
        public static Rect ComputeStageRect(float screenWidth, float screenHeight)
        {
            float refWidth = LetterboxCamera.ReferenceWidth, refHeight = LetterboxCamera.ReferenceHeight;
            if (screenWidth <= 0f || screenHeight <= 0f) return new Rect(0f, 0f, refWidth, refHeight);

            float logicalWidth, logicalHeight;
            if (ComputeMatch(screenWidth, screenHeight) >= 1f) // match height: height pinned, width grows with aspect
            {
                logicalHeight = refHeight;
                logicalWidth = refHeight * screenWidth / screenHeight;
            }
            else // match width: width pinned, height grows with aspect
            {
                logicalWidth = refWidth;
                logicalHeight = refWidth * screenHeight / screenWidth;
            }

            return new Rect((logicalWidth - refWidth) / 2f, (logicalHeight - refHeight) / 2f, refWidth, refHeight);
        }
    }
}
