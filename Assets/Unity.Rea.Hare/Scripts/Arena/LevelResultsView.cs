using System;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.UI;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// obj_level_results drawn (U.HARE-T22): Core's <see cref="LevelResults"/> on the 1920x1080 GUI layer, as its Draw GUI
    /// event draws it: draw_self (spr_results at the panel's x, y less its 356,0 origin, at image_alpha 0.7), then, at
    /// alpha 1 in white with a size-1 black outline, "Level Complete" in fnt_score (<see cref="HareTextStyles.HudScore"/>)
    /// and the "Time", "Enemies Killed" and "Press [ENTER] to continue" lines in fnt_point
    /// (<see cref="HareTextStyles.HudText"/>), each hung off its x as its draw_set_halign says. It reads obj_healthbar's
    /// strTime and KILLS every frame (<see cref="Refresh"/>); the arena owns it and takes it away on Enter.
    /// </summary>
    public sealed class LevelResultsView : VisualElement
    {
        public const string ElementName = "obj_level_results";

        readonly LevelResults results;
        readonly Func<string> time;
        readonly Func<int> kills;
        readonly VisualElement lines;
        string shownTime;
        int shownKills = -1;

        LevelResultsView(LevelResults results, ArenaTuning tuning, Func<string> time, Func<int> kills)
        {
            this.results = results;
            this.time = time ?? (() => "00:00");
            this.kills = kills ?? (() => 0);
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = tuning.HudWidth;
            style.height = tuning.HudHeight;

            Panel = new VisualElement { name = "spr_results", pickingMode = PickingMode.Ignore };
            Panel.style.position = Position.Absolute;
            Panel.style.left = results.SpriteLeft;
            Panel.style.top = results.SpriteTop;
            Panel.style.width = results.Definition.Width;
            Panel.style.height = results.Definition.Height;
            Panel.style.opacity = results.Definition.Alpha;
            var manifest = HareArtManifest.Load();
            var sprite = manifest != null ? manifest.Frame(results.Definition.Sprite) : null;
            if (sprite != null) Panel.style.backgroundImage = new StyleBackground(sprite);
            else Debug.LogWarning("[Unity.Hare] " + results.Definition.Sprite + " is not imported.");
            Add(Panel);

            lines = new VisualElement { name = "lines", pickingMode = PickingMode.Ignore };
            lines.style.position = Position.Absolute;
            lines.style.left = 0;
            lines.style.top = 0;
            Add(lines);
            Refresh();
        }

        /// <summary>The panel's rules.</summary>
        public LevelResults Results => results;

        /// <summary>draw_self: spr_results at 0.7 alpha.</summary>
        public VisualElement Panel { get; }

        /// <summary>The text lines as drawn, in the Draw GUI's order (title, "Time", strTime, "Enemies Killed", KILLS, prompt).</summary>
        public VisualElement Lines => lines;

        /// <summary>The text of each drawn line, in order.</summary>
        public string[] Texts()
        {
            var texts = new string[lines.childCount];
            for (int i = 0; i < texts.Length; i++) texts[i] = lines[i].Q<Label>()?.text ?? "";
            return texts;
        }

        /// <summary>set_level_complete's <c>instance_create(0,0, obj_level_results)</c> on <paramref name="hud"/>.</summary>
        public static LevelResultsView Show(VisualElement hud, LevelResults results, ArenaTuning tuning, Func<string> time, Func<int> kills)
        {
            var view = new LevelResultsView(results, tuning, time, kills);
            hud.Add(view);
            return view;
        }

        /// <summary>Draws the lines with strTime and KILLS as they now are.</summary>
        public void Refresh()
        {
            string t = time();
            int k = kills();
            if (t == shownTime && k == shownKills) return;
            shownTime = t;
            shownKills = k;
            lines.Clear();
            int outline = results.Definition.Outline;
            foreach (var line in results.Lines(t, k))
            {
                var font = line.Large ? HareTextStyles.HudScore : HareTextStyles.HudText;
                var element = HareUi.OutlineText(line.Text, outline, font.UssClass, lines);
                element.style.position = Position.Absolute;
                element.style.left = line.X;
                element.style.top = line.Y;
                switch (line.Align)
                {
                    case ResultsAlign.Center:
                        element.style.translate = new Translate(Length.Percent(-50), 0);
                        element.style.unityTextAlign = TextAnchor.UpperCenter;
                        break;
                    case ResultsAlign.Right:
                        element.style.translate = new Translate(Length.Percent(-100), 0);
                        element.style.unityTextAlign = TextAnchor.UpperRight;
                        break;
                    default:
                        element.style.unityTextAlign = TextAnchor.UpperLeft;
                        break;
                }
            }
        }
    }
}
