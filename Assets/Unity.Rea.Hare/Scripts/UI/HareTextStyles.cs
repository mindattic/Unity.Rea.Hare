using UnityEngine.UIElements;

namespace Unity.Rea.Hare.UI
{
    /// <summary>
    /// The named text styles every HUD, menu and typewriter screen draws with (U.HARE-T48), each the face the original's
    /// own <c>draw_set_font</c> calls pick for that kind of text (reverse-engineered in docs/REVERSE_ENGINEERING.md,
    /// "Fonts"). A style is a Theme.uss class (<c>.text-hud-score</c>, ...) that sets the face's FontAsset
    /// (<c>-unity-font-definition: resource("Hare/Fonts/&lt;face&gt;")</c>) at its native size, so the face and size
    /// live in the stylesheet, as data; <see cref="Apply"/> adds the class. <see cref="Face"/> names the face each
    /// style resolves to, for code that measures text (Core's <c>GmlFontMetrics</c>), and the tests hold the
    /// stylesheet to it.
    /// </summary>
    public static class HareTextStyles
    {
        public readonly struct Style
        {
            public readonly string Name, UssClass, Face;
            public Style(string name, string ussClass, string face) { Name = name; UssClass = ussClass; Face = face; }
            public override string ToString() => Name;
        }

        /// <summary>The big HUD numbers: the score (<c>obj_game</c>), lives (<c>obj_healthbar</c>), the speed readout (<c>obj_speedometer</c>).</summary>
        public static readonly Style HudScore = new Style(nameof(HudScore), "text-hud-score", HareFonts.Score);

        /// <summary>The rest of the HUD: the objective (<c>obj_game</c>), clock and coins (<c>obj_healthbar</c>), the speed unit, the energy bar's figure.</summary>
        public static readonly Style HudText = new Style(nameof(HudText), "text-hud", HareFonts.Point);

        /// <summary>The ammo counters (<c>obj_weapon_gui</c>).</summary>
        public static readonly Style HudAmmo = new Style(nameof(HudAmmo), "text-hud-ammo", HareFonts.Small);

        /// <summary>Menu headings: the title buttons' captions (<c>obj_btn_campaign</c>, <c>_quickstart</c>, <c>_quit</c>, <c>_yes</c>, <c>_no</c>),
        /// the yes/no dialogs, "Level Complete" (<c>obj_level_results</c>), <c>obj_levelstart</c>, <c>obj_upgrade</c>.</summary>
        public static readonly Style MenuTitle = new Style(nameof(MenuTitle), "text-menu-title", HareFonts.Score);

        /// <summary>Menu body text: the map and level buttons, dialog messages, the results rows, tips, labels, scenario details, the startup screen.</summary>
        public static readonly Style MenuText = new Style(nameof(MenuText), "text-menu", HareFonts.Point);

        /// <summary>The in-game typewriter (<c>obj_typewriter</c>).</summary>
        public static readonly Style Typewriter = new Style(nameof(Typewriter), "text-typewriter", HareFonts.Point);

        /// <summary>The story screen's typewriter (<c>obj_typewriter_story</c>).</summary>
        public static readonly Style StoryTypewriter = new Style(nameof(StoryTypewriter), "text-story", HareFonts.Medium);

        /// <summary>A conversation's speech (<c>obj_conversation</c>).</summary>
        public static readonly Style Conversation = new Style(nameof(Conversation), "text-conversation", HareFonts.Score);

        /// <summary>The speaker's name under a conversation portrait (<c>obj_conversation</c>).</summary>
        public static readonly Style ConversationName = new Style(nameof(ConversationName), "text-conversation-name", HareFonts.Point);

        /// <summary>Floating pickup and damage text (<c>obj_float_text</c>).</summary>
        public static readonly Style FloatText = new Style(nameof(FloatText), "text-float", HareFonts.Point);

        /// <summary>Text drawn with no <c>draw_set_font</c>: GameMaker's default face, Arial 12, which <c>fnt_basic</c> bakes.</summary>
        public static readonly Style Default = new Style(nameof(Default), "text-default", HareFonts.Basic);

        public static readonly Style[] All =
        {
            HudScore, HudText, HudAmmo, MenuTitle, MenuText, Typewriter, StoryTypewriter, Conversation, ConversationName, FloatText, Default,
        };

        /// <summary>Gives <paramref name="element"/> (and, by USS inheritance, every Label inside it, such as an
        /// <see cref="HareUi.OutlineText"/> stack's layers) the style, replacing any other text style it had.</summary>
        public static T Apply<T>(T element, Style style) where T : VisualElement
        {
            foreach (var s in All) element.RemoveFromClassList(s.UssClass);
            element.AddToClassList(style.UssClass);
            return element;
        }
    }
}
