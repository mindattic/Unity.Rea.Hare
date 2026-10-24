using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Rendering;

namespace Unity.Hare.UI
{
    /// <summary>
    /// Builds the PanelSettings and the <see cref="BuildRoot"/>'s stage every screen (HUD, menus, results, upgrade,
    /// story — U.HARE-T45) is laid out on, at the original's 1920x1080 reference resolution
    /// (<see cref="LetterboxCamera.ReferenceWidth"/>/<see cref="LetterboxCamera.ReferenceHeight"/>) so UI pixels
    /// line up with the GML <c>draw_text</c>/<c>draw_text_outline</c> calls they replace.
    ///
    /// Built entirely in code (AGENTS.md rule 6): there is no hand-authored PanelSettings asset and nothing is
    /// wired up in the Inspector. <see cref="ThemeResourcePath"/>'s <c>HareTheme.tss</c> (just
    /// <c>@import url("unity-theme://default")</c>, the same one-line pattern Unity.GridGame's <c>GaidenTheme.tss</c>
    /// uses) gives native controls (Button, ScrollView) their baseline chrome — a panel left with no
    /// ThemeStyleSheet logs a warning at runtime and ships unstyled, since the Editor-only default theme Unity
    /// auto-generates under <c>Assets/UI Toolkit/</c> the first time a panel needs one is not itself under a
    /// Resources folder and would be stripped from a player build. <see cref="ThemeResourcePath"/>'s
    /// <c>Theme.uss</c> then carries every Hare-specific class the widget kit (<see cref="HareUi"/>) uses.
    /// </summary>
    public static class HarePanel
    {
        /// <summary>Resources path (no extension) of the shared Hare UI stylesheet.</summary>
        public const string ThemeResourcePath = "Hare/UI/Theme";

        /// <summary>Resources path (no extension) of the panel's ThemeStyleSheet (native control chrome).</summary>
        public const string ThemeStyleSheetResourcePath = "Hare/UI/HareTheme";

        /// <summary>The name <see cref="BuildRoot"/> gives the 1920x1080 stage; <see cref="HareUiFit"/> looks it
        /// up by this name to keep it confined to the screen's visible, non-letterboxed area.</summary>
        public const string StageName = "hare-stage";

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

            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeStyleSheetResourcePath);
            if (settings.themeStyleSheet == null)
                Debug.LogError($"[Unity.Hare] missing {ThemeStyleSheetResourcePath} — native UI Toolkit controls will render unstyled");

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

        /// <summary>
        /// Styles the document's root with <c>Theme.uss</c> (classed <c>hare-root fill</c>) and adds a child
        /// "stage" (classed <c>hare-stage</c>, named <see cref="StageName"/>) pinned to exactly 1920x1080 logical
        /// pixels — <see cref="HareUiFit"/> keeps it centred in whatever larger logical area a pillarboxed or
        /// letterboxed window's match axis produces, so it always exactly covers the screen's visible,
        /// non-letterboxed area the same way <see cref="LetterboxCamera"/> does for the game camera. Returns the
        /// stage: this, not the root, is what a screen adds itself to. A full-bleed veil (<see cref="HareUi.Dialog"/>)
        /// that should also dim the letterbox bars adds itself to <c>document.rootVisualElement</c> instead.
        /// </summary>
        public static VisualElement BuildRoot(UIDocument document)
        {
            var root = document.rootVisualElement;
            var theme = Resources.Load<StyleSheet>(ThemeResourcePath);
            if (theme != null) root.styleSheets.Add(theme);
            root.AddToClassList("hare-root");
            root.AddToClassList("fill");

            var stage = new VisualElement { name = StageName };
            stage.AddToClassList("hare-stage");
            stage.style.position = Position.Absolute;
            stage.style.width = LetterboxCamera.ReferenceWidth;
            stage.style.height = LetterboxCamera.ReferenceHeight;
            root.Add(stage);
            return stage;
        }
    }
}
