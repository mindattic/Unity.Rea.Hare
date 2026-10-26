using System;
using System.Collections.Generic;

namespace Unity.Hare.Content
{
    /// <summary>One line of obj_conversation's story text or room_story's scenario text.</summary>
    [Serializable]
    public class DialogueLine
    {
        /// <summary>A short, unique key a presentation script asks for (e.g. "story.scenario1.intro.01").</summary>
        public string Id = "";
        public string Speaker = "";
        public string Text = "";

        public DialogueLine() { }

        public DialogueLine(string id, string speaker, string text)
        {
            Id = id;
            Speaker = speaker;
            Text = text;
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Id", Id);
            ValidationHelpers.Required(errors, path + ".Text", Text);
        }
    }

    /// <summary>
    /// The story and conversation text (docs/RECONSTRUCTION.md's "text" row), transcribed from the original by
    /// U.HARE-T28: obj_story's <c>typewriter.text += ...</c> lines in order (<c>story.NN</c>, <c>indent()</c> written out as
    /// its ten spaces) and obj_conversation's <c>text</c> (<c>conversation.map.NN</c>, spoken by Admiral Zee). GameMaker's
    /// <c>#</c> is a new line and <c>^</c> a typewriter pause. A script is the lines under one id, joined in order
    /// (<see cref="Script"/>). obj_tip's texts are the popups section's, credits.txt the audio pipeline's.
    /// </summary>
    [Serializable]
    public class DialogueCatalog
    {
        public DialogueLine[] Lines =
        {
            new DialogueLine("story.01", "", "^^"),
            new DialogueLine("story.02", "", "Introduction##^"),
            new DialogueLine("story.03", "", "          It was the dawn of what many believed would be a golden age.#^"),
            new DialogueLine("story.04", "", "          At long last the world had acheived lasting peace.#^^"),
            new DialogueLine("story.05", "", "          Then an unimaginable threat emerged.##^^"),
            new DialogueLine("story.06", "", "          As mankind had been squabbling amongst itself,#"),
            new DialogueLine("story.07", "", "          A vast interstellar empire had been spreading it's influence across the the depths of space for ten millenia.#^^^"),
            new DialogueLine("story.08", "", "          Bent on total conquest, ^"),
            new DialogueLine("story.09", "", "built on the broken backs of slaves ^"),
            new DialogueLine("story.10", "", "and maintained by mercilessness.##^^"),
            new DialogueLine("story.11", "", "Homeworld Orbital Patrol##^"),
            new DialogueLine("story.12", "", "          H.O.P was rapidly established to combat this otherworldy threat.#^"),
            new DialogueLine("story.13", "", "          Composed of a miraculous amalgamation of the world's greatest military and scientific resources;#"),
            new DialogueLine("story.14", "", "          They were our only hope.##^"),
            new DialogueLine("story.15", "", "High Altitude Reconnaissance & Engagement##^"),
            new DialogueLine("story.16", "", "          The H.A.R.E Project was conceived as a first line of defense against the alien invaders;#"),
            new DialogueLine("story.17", "", "          A vangaurd against aspiring conquerors from beyond the stars.##^^"),
            new DialogueLine("story.18", "", "          By intercepting the extraterrestial menace before they can establish a foothold,#"),
            new DialogueLine("story.19", "", "          our chances of repelling the cosmic threat can be marginally improved.##^^"),
            new DialogueLine("story.20", "", "Your Mission##^"),
            new DialogueLine("story.21", "", "          Assume control of an experimental H.A.R.E. Unit.#^"),
            new DialogueLine("story.22", "", "          Patrol the exosphere for intrusions.#^"),
            new DialogueLine("story.23", "", "          Exterminate all intruders with extreme prejudice.#"),
            new DialogueLine("story.24", "", "#^^^"),
            new DialogueLine("story.25", "", "Good luck ^"),
            new DialogueLine("story.26", "", "...you'll need it!"),
            new DialogueLine("conversation.map.01", "Admiral Zee", "We need your help, Hutch!#^"),
            new DialogueLine("conversation.map.02", "Admiral Zee", "The planet is in peril!#^"),
            new DialogueLine("conversation.map.03", "Admiral Zee", "Alien invaders have broken through the blockade!"),
        };

        public DialogueLine Find(string id)
        {
            foreach (var line in Lines)
                if (line != null && line.Id == id) return line;
            return null;
        }

        /// <summary>
        /// The lines of script <paramref name="id"/> (the line called <paramref name="id"/> and those called
        /// <c>id.anything</c>), in file order.
        /// </summary>
        public IReadOnlyList<DialogueLine> ScriptLines(string id)
        {
            var found = new List<DialogueLine>();
            if (string.IsNullOrEmpty(id)) return found;
            foreach (var line in Lines)
                if (line != null && (line.Id == id || (line.Id != null && line.Id.StartsWith(id + ".", StringComparison.Ordinal))))
                    found.Add(line);
            return found;
        }

        /// <summary>Script <paramref name="id"/>'s text: its lines' text joined in order, as the original's <c>text += ...</c> builds it.</summary>
        public string Script(string id)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var line in ScriptLines(id)) builder.Append(line.Text);
            return builder.ToString();
        }

        /// <summary>Who speaks script <paramref name="id"/>: its first line's speaker ("" for none).</summary>
        public string Speaker(string id)
        {
            var lines = ScriptLines(id);
            return lines.Count > 0 ? lines[0].Speaker ?? "" : "";
        }

        internal void Validate(List<string> errors, string path)
        {
            if (Lines == null) { errors.Add(path + ".Lines: must not be null."); return; }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Lines.Length; i++)
            {
                var line = Lines[i];
                string entryPath = $"{path}.Lines[{i}]";
                if (line == null) { errors.Add($"{entryPath}: must not be null."); continue; }
                line.Validate(errors, entryPath);
                if (!string.IsNullOrWhiteSpace(line.Id) && !seen.Add(line.Id))
                    errors.Add($"{entryPath}.Id: '{line.Id}' is used by more than one line.");
            }
        }
    }
}
