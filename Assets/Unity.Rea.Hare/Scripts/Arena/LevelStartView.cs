using UnityEngine;
using UnityEngine.UIElements;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// obj_levelstart drawn (U.HARE-T57): Core's <see cref="LevelStartBanner"/> on the 1920x1080 GUI layer, as its Draw GUI
    /// event draws it at <c>x = screen_width / 2, y = screen_height / 2</c>: level_name in fnt_score
    /// (<see cref="HareTextStyles.HudScore"/>) and, 65 px below, strObjective in fnt_point (<see cref="HareTextStyles.HudText"/>),
    /// both <c>fa_center</c> white with a size-2 black outline, at the banner's alpha. The arena steps the banner on the
    /// room clock; this only draws it.
    /// </summary>
    public sealed class LevelStartView : VisualElement
    {
        public const string ElementName = "obj_levelstart";

        readonly LevelStartBanner banner;
        readonly ArenaTuning tuning;
        VisualElement nameLine;
        VisualElement objectiveLine;
        string shownName;
        string shownObjective;

        LevelStartView(LevelStartBanner banner, ArenaTuning tuning)
        {
            this.banner = banner;
            this.tuning = tuning;
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = tuning.HudWidth;
            style.height = tuning.HudHeight;
            Refresh();
        }

        /// <summary>The banner's rules.</summary>
        public LevelStartBanner Banner => banner;

        /// <summary>The level_name line as drawn.</summary>
        public VisualElement NameLine => nameLine;

        /// <summary>The strObjective line as drawn.</summary>
        public VisualElement ObjectiveLine => objectiveLine;

        /// <summary>instance_create(0, 0, obj_levelstart) on <paramref name="hud"/>.</summary>
        public static LevelStartView Show(VisualElement hud, LevelStartBanner banner, ArenaTuning tuning)
        {
            var view = new LevelStartView(banner, tuning);
            hud.Add(view);
            return view;
        }

        /// <summary>Draws the banner as it now is; removes it once it has gone.</summary>
        public void Refresh()
        {
            if (banner.Gone)
            {
                RemoveFromHierarchy();
                return;
            }
            if (nameLine == null || shownName != banner.LevelName || shownObjective != banner.Objective) Build();
            style.opacity = banner.DrawAlpha;
        }

        void Build()
        {
            Clear();
            shownName = banner.LevelName;
            shownObjective = banner.Objective;
            float x = tuning.HudWidth / 2f, y = tuning.HudHeight / 2f;
            nameLine = Line(shownName, HareTextStyles.HudScore, x, y);
            objectiveLine = Line(shownObjective, HareTextStyles.HudText, x, y + tuning.LevelStartObjectiveOffsetY);
        }

        /// <summary>draw_set_halign(fa_center): the line's middle on <paramref name="x"/>, its top on <paramref name="y"/>.</summary>
        VisualElement Line(string text, HareTextStyles.Style textStyle, float x, float y)
        {
            var line = HareUi.OutlineText(text, tuning.LevelStartOutline, textStyle.UssClass, this);
            line.style.position = Position.Absolute;
            line.style.left = x;
            line.style.top = y;
            line.style.translate = new Translate(Length.Percent(-50), 0);
            line.style.unityTextAlign = TextAnchor.UpperCenter;
            return line;
        }
    }
}
