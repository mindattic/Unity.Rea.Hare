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

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Id", Id);
            ValidationHelpers.Required(errors, path + ".Text", Text);
        }
    }

    /// <summary>
    /// The story and conversation text (docs/RECONSTRUCTION.md's "text" row: load_scenario, obj_conversation,
    /// obj_tip, credits.txt). Empty until U.HARE-T28 transcribes it from the original; the registry validates
    /// whatever is here today so that ticket adds lines to a data file instead of hardcoding strings in a script.
    /// </summary>
    [Serializable]
    public class DialogueCatalog
    {
        public DialogueLine[] Lines = Array.Empty<DialogueLine>();

        public DialogueLine Find(string id)
        {
            foreach (var line in Lines)
                if (line != null && line.Id == id) return line;
            return null;
        }

        internal void Validate(List<string> errors, string path)
        {
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
