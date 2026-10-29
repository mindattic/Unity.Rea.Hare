using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.UI;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// The gameplay HUD drawn (U.HARE-T25): Core's <see cref="GameHud"/> frame on the 1920x1080 GUI layer, one element per
    /// sprite (the imported art, top-left origin, tinted by its image_blend) and one <see cref="HareUi.OutlineText"/> per
    /// line (size-1 black outline, fnt_score, fnt_point or fnt_small), each hung off its x as its draw_set_halign says.
    /// Elements are kept by key and only rebuilt when their text changes; the arena refreshes it every frame.
    /// </summary>
    public sealed class GameHudView : VisualElement
    {
        public const string ElementName = "gameplay-hud";

        readonly Dictionary<string, VisualElement> sprites = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, string> spriteNames = new Dictionary<string, string>();
        readonly Dictionary<string, VisualElement> texts = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, string> shownTexts = new Dictionary<string, string>();
        readonly HashSet<string> warned = new HashSet<string>();
        readonly int outline;
        string order;

        GameHudView(int width, int height, int outline)
        {
            this.outline = outline;
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = width;
            style.height = height;
        }

        /// <summary>The frame last drawn.</summary>
        public HudFrame Frame { get; private set; }

        /// <summary>obj_game's Create making the four HUD objects, on <paramref name="hud"/> (under anything drawn on it later).</summary>
        public static GameHudView Show(VisualElement hud, GameHud model)
        {
            var view = new GameHudView(model.GuiWidth, model.GuiHeight, model.Definition.Outline);
            hud.Insert(0, view);
            return view;
        }

        /// <summary>The drawn sprite <paramref name="key"/> (e.g. "pip0", "energy-fill"), or null while it is not drawn.</summary>
        public VisualElement SpriteElement(string key) =>
            sprites.TryGetValue(key, out var e) && e.style.display.value != DisplayStyle.None ? e : null;

        /// <summary>The drawn line <paramref name="key"/> (e.g. "lives", "speed"), or null while it is not drawn.</summary>
        public VisualElement TextElement(string key) =>
            texts.TryGetValue(key, out var e) && e.style.display.value != DisplayStyle.None ? e : null;

        /// <summary>Draws <paramref name="frame"/>.</summary>
        public void Refresh(HudFrame frame)
        {
            Frame = frame;
            var seen = new HashSet<string>();
            var keys = new StringBuilder();
            foreach (var s in frame.Sprites)
            {
                seen.Add("s:" + s.Key);
                keys.Append("s:").Append(s.Key).Append('|');
                PlaceSprite(s);
            }
            foreach (var t in frame.Texts)
            {
                seen.Add("t:" + t.Key);
                keys.Append("t:").Append(t.Key).Append('|');
                PlaceText(t);
            }
            foreach (var pair in sprites)
                if (!seen.Contains("s:" + pair.Key)) pair.Value.style.display = DisplayStyle.None;
            foreach (var pair in texts)
                if (!seen.Contains("t:" + pair.Key)) pair.Value.style.display = DisplayStyle.None;

            // Draw order: the frame's, sprites under text (a select box over its icon, an empty pip over its back).
            string now = keys.ToString();
            if (now != order)
            {
                order = now;
                foreach (var s in frame.Sprites) sprites[s.Key].BringToFront();
                foreach (var t in frame.Texts) texts[t.Key].BringToFront();
            }
        }

        void PlaceSprite(HudSprite s)
        {
            if (!sprites.TryGetValue(s.Key, out var e))
            {
                e = new VisualElement { name = s.Key, pickingMode = PickingMode.Ignore };
                e.style.position = Position.Absolute;
                e.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
                sprites.Add(s.Key, e);
                Add(e);
            }
            Sprite sprite = null;
            if (!spriteNames.TryGetValue(s.Key, out var shown) || shown != s.Sprite)
            {
                spriteNames[s.Key] = s.Sprite;
                var manifest = HareArtManifest.Load();
                sprite = manifest != null ? manifest.Frame(s.Sprite) : null;
                if (sprite != null) e.style.backgroundImage = new StyleBackground(sprite);
                else
                {
                    e.style.backgroundImage = StyleKeyword.None;
                    if (warned.Add(s.Sprite)) Debug.LogWarning("[Unity.Hare] " + s.Sprite + " is not imported.");
                }
            }
            else sprite = e.style.backgroundImage.value.sprite;

            e.style.display = DisplayStyle.Flex;
            e.style.left = s.X;
            e.style.top = s.Y;
            float width = float.IsNaN(s.Width) ? (sprite != null ? sprite.rect.width : 0f) : s.Width;
            float height = float.IsNaN(s.Height) ? (sprite != null ? sprite.rect.height : 0f) : s.Height;
            e.style.width = width;
            e.style.height = height;
            e.style.opacity = s.Alpha;
            ColorUtility.TryParseHtmlString(s.Color, out var tint);
            e.style.unityBackgroundImageTintColor = tint;
        }

        void PlaceText(HudText t)
        {
            if (!texts.TryGetValue(t.Key, out var e) || !shownTexts.TryGetValue(t.Key, out var shown) || shown != t.Text)
            {
                int index = -1;
                if (e != null)
                {
                    index = IndexOf(e);
                    e.RemoveFromHierarchy();
                }
                e = HareUi.OutlineText(t.Text, outline, StyleFor(t.Font).UssClass);
                e.name = t.Key;
                e.style.position = Position.Absolute;
                if (index >= 0) Insert(index, e); else Add(e);
                texts[t.Key] = e;
                shownTexts[t.Key] = t.Text;
            }
            e.style.display = DisplayStyle.Flex;
            e.style.left = t.X;
            e.style.top = t.Y;
            switch (t.Align)
            {
                case ResultsAlign.Center:
                    e.style.translate = new Translate(Length.Percent(-50), 0);
                    e.style.unityTextAlign = TextAnchor.UpperCenter;
                    break;
                case ResultsAlign.Right:
                    e.style.translate = new Translate(Length.Percent(-100), 0);
                    e.style.unityTextAlign = TextAnchor.UpperRight;
                    break;
                default:
                    e.style.translate = new Translate(0, 0);
                    e.style.unityTextAlign = TextAnchor.UpperLeft;
                    break;
            }
        }

        /// <summary>The text of the drawn line <paramref name="key"/>, or null.</summary>
        public string TextOf(string key) => TextElement(key) != null && shownTexts.TryGetValue(key, out var text) ? text : null;

        static HareTextStyles.Style StyleFor(HudFont font)
        {
            switch (font)
            {
                case HudFont.Score: return HareTextStyles.HudScore;
                case HudFont.Small: return HareTextStyles.HudAmmo;
                default: return HareTextStyles.HudText;
            }
        }
    }
}
