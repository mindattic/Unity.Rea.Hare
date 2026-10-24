using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Rendering;

namespace Unity.Hare.UI
{
    /// <summary>
    /// Builds the PanelSettings and root <see cref="VisualElement"/> every screen (HUD, menus, results, upgrade,
    /// story — U.HARE-T45) is laid out on, at the original's 1920x1080 reference resolution
    /// (<see cref="LetterboxCamera.ReferenceWidth"/>/<see cref="LetterboxCamera.ReferenceHeight"/>) so UI pixels
    /// line up with the GML <c>draw_text</c>/<c>draw_text_outline</c> calls they replace.
    ///
    /// Built entirely in code (AGENTS.md rule 6): there is no hand-authored PanelSettings asset and nothing is
    /// wired up in the Inspector. No custom ThemeStyleSheet is assigned — Unity's own default runtime theme gives
    /// native controls (Button, ScrollView) their baseline chrome, and <see cref="ThemeResourcePath"/>'s
    /// <c>Theme.uss</c> carries every Hare-specific class the widget kit (<see cref="HareUi"/>) uses.
    /// </summary>
    public static class HarePanel
    {
        /// <summary>Resources path (no extension) of the shared Hare UI stylesheet.</summary>
        public const string ThemeResourcePath = "Hare/UI/Theme";

        /// <summary>
        /// A fresh PanelSettings at the original's 1920x1080 reference resolution, scaled to fill the window by
        /// whichever axis <see cref="HareUiFit"/> (or the caller) selects. Not saved to disk
        /// (<see cref="HideFlags.DontSave"/>): like <see cref="LetterboxCamera"/>'s camera rig, it is rebuilt by
        /// code every time rather than checked in as a binary asset.
        /// </summary>
        public static PanelSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "HarePanelSettings";
            settings.hideFlags = HideFlags.DontSave;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int((int)LetterboxCamera.ReferenceWidth, (int)LetterboxCamera.ReferenceHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0f; // HareUiFit keeps this matched to whichever axis LetterboxCamera is itself limited by
            settings.clearColor = false; // CameraRig's cameras own the backbuffer clear; the UI panel only draws over it
            return settings;
        }

        /// <summary>
        /// Adds (or reuses) a <see cref="UIDocument"/> on <paramref name="host"/> wired to <paramref name="settings"/>
        /// (a fresh <see cref="CreateSettings"/> panel when none is given).
        /// </summary>
        public static UIDocument Attach(GameObject host, PanelSettings settings = null)
        {
            var document = host.GetComponent<UIDocument>();
            if (document == null) document = host.AddComponent<UIDocument>();
            document.panelSettings = settings != null ? settings : CreateSettings();
            return document;
        }

        /// <summary>The document's root VisualElement, with <c>Theme.uss</c> applied and classed <c>hare-root fill</c>
        /// so a screen can start adding itself to it immediately.</summary>
        public static VisualElement BuildRoot(UIDocument document)
        {
            var root = document.rootVisualElement;
            var theme = Resources.Load<StyleSheet>(ThemeResourcePath);
            if (theme != null) root.styleSheets.Add(theme);
            root.AddToClassList("hare-root");
            root.AddToClassList("fill");
            return root;
        }
    }
}
