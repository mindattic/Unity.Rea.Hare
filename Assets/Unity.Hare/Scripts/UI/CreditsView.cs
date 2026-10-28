using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Hare.UI
{
    /// <summary>One credited third-party asset: what it is, who made it, where it came from and its licence.</summary>
    public sealed class CreditEntry
    {
        public readonly string Kind, Asset, Author, Source, Licence;
        public CreditEntry(string kind, string asset, string author, string source, string licence)
        {
            Kind = kind; Asset = asset; Author = author; Source = source; Licence = licence;
        }
    }

    /// <summary>
    /// The credits screen (U.HARE-T69): third-party assets the game ships, from datafiles/credits.txt (the original's
    /// only credit, the Jim Rogers minigun sound), the music tracks' ID3 tags and the font pipeline's licence notes.
    /// The original had no credits room; this one is a deviation (docs/BIBLE.md V50), reached from the title.
    /// Licences not stated by a file in the repo or the original project are "unverified" with the source named.
    /// docs/CREDITS.md carries the same list in full; keep the two in step.
    /// </summary>
    public static class CreditsView
    {
        public const string Unverified = "unverified";
        public const string ElementName = "credits";

        public static readonly IReadOnlyList<CreditEntry> Entries = new[]
        {
            new CreditEntry("Sound", "Minigun sound effect", "Jim Rogers", "http://soundbible.com/1920-Minigun.html",
                "unverified (named only in the original's datafiles/credits.txt)"),
            new CreditEntry("Music", "In a Heartbeat", "Kevin MacLeod", "ID3 tag of music_inaheartbeat.mp3", "unverified (tag says nothing; album \"Royalty Free\")"),
            new CreditEntry("Music", "Latin Industries", "Kevin MacLeod", "ID3 tag of music_latinindustries.mp3", "unverified (tag: \"Royalty Free\")"),
            new CreditEntry("Music", "Overcast", "Kevin MacLeod", "ID3 tag of music_overcast.mp3", "unverified (tag: \"Royalty Free\")"),
            new CreditEntry("Music", "Pinball Spring", "unverified (file carries no tag)", "file name music_pinballspring.mp3", Unverified),
            new CreditEntry("Music", "Presenterator", "Kevin MacLeod", "ID3 tag of music_presenterator.mp3", "unverified (tag: \"Royalty Free\")"),
            new CreditEntry("Music", "Wallpaper", "Kevin MacLeod", "ID3 tag of music_wallpaper.mp3", "unverified (tag: \"Royalty Free\")"),
            new CreditEntry("Font", "Arial (rasterised glyphs only)", "Monotype", "Assets/Unity.Hare/Fonts/gml-fonts.json", "proprietary; no font file shipped"),
            new CreditEntry("Font", "Berlin Sans FB (rasterised glyphs only)", "The Font Bureau", "Assets/Unity.Hare/Fonts/gml-fonts.json", "proprietary; no font file shipped"),
            new CreditEntry("Sound", "All other sound effects", "unverified", "HARE.gmx sound\audio (no credit recorded)", Unverified),
            new CreditEntry("Sprites", "All sprites and backgrounds", "unverified", "HARE.gmx (no credit recorded)", Unverified),
        };

        /// <summary>The screen's lines, one per string: heading, each entry, then the closing hint.</summary>
        public static IEnumerable<string> Lines()
        {
            yield return "Credits";
            foreach (var e in Entries)
                yield return e.Kind + ": " + e.Asset + " - " + e.Author + " (" + e.Licence + ")";
            yield return "Click or press Esc to return";
        }

        /// <summary>Builds the screen over <paramref name="parent"/>: a dark backdrop with the lines, in the menu's faces.</summary>
        public static VisualElement Build(VisualElement parent, float width, float height)
        {
            var root = new VisualElement { name = ElementName, pickingMode = PickingMode.Ignore };
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.top = 0;
            root.style.width = width;
            root.style.height = height;
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.88f);
            root.style.alignItems = Align.Center;
            root.style.paddingTop = 60;
            parent.Add(root);

            bool first = true;
            foreach (var line in Lines())
            {
                var style = first ? HareTextStyles.MenuTitle : HareTextStyles.MenuText;
                var label = HareUi.Text(line, style.UssClass, root);
                label.style.whiteSpace = WhiteSpace.Normal;
                label.style.maxWidth = width - 160;
                label.style.unityTextAlign = TextAnchor.UpperCenter;
                label.style.marginBottom = first ? 24 : 8;
                label.style.color = Color.white;
                first = false;
            }
            return root;
        }
    }
}
