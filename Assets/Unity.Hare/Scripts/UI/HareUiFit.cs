using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Rendering;

namespace Unity.Hare.UI
{
    /// <summary>
    /// Keeps a <see cref="PanelSettings"/>' MatchWidthOrHeight axis locked to whichever one
    /// <see cref="LetterboxCamera"/> is also matching, so the UI's 1920x1080 logical area always exactly covers
    /// the visible, non-letterboxed part of the screen: match height (1) when the window is wider than 16:9 and
    /// pillarboxed, match width (0) when it is narrower and letterboxed. The sizing math (<see cref="ComputeMatch"/>)
    /// is the same branch <see cref="LetterboxCamera.ComputeViewportRect"/> uses, and is just as pure and testable.
    /// Attach alongside the <see cref="UIDocument"/> the panel belongs to.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HareUiFit : MonoBehaviour
    {
        UIDocument document;
        int lastWidth = -1, lastHeight = -1;

        void Awake() => document = GetComponent<UIDocument>();

        void OnEnable() => Apply(force: true);

        void Update() => Apply(force: false);

        void Apply(bool force)
        {
            if (!force && Screen.width == lastWidth && Screen.height == lastHeight) return;
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null || document.panelSettings == null) return;
            document.panelSettings.match = ComputeMatch(lastWidth, lastHeight);
        }

        /// <summary>1 (match height) when the window is wider than the 1920x1080 reference aspect and so
        /// pillarboxed, 0 (match width) when it is narrower and letterboxed, 0 for a degenerate screen size.</summary>
        public static float ComputeMatch(float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f) return 0f;
            return screenWidth / screenHeight > LetterboxCamera.ReferenceAspect ? 1f : 0f;
        }
    }
}
