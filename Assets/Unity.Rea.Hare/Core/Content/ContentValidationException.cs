using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Hare.Content
{
    /// <summary>
    /// Raised by <see cref="ContentRegistry"/> when a content/tuning data file fails validation. Carries every
    /// problem found (not just the first), each tagged with the dotted path of the value that is wrong
    /// (e.g. "weapons.vulcan.bulletSpeed: must be greater than 0"), so a bad data file can be fixed in one pass.
    /// </summary>
    public sealed class ContentValidationException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public ContentValidationException(IReadOnlyList<string> errors)
            : base(BuildMessage(errors))
        {
            Errors = errors;
        }

        static string BuildMessage(IReadOnlyList<string> errors)
        {
            if (errors == null || errors.Count == 0) return "Content failed validation.";
            return $"Content failed validation ({errors.Count} problem{(errors.Count == 1 ? "" : "s")}):\n" +
                   string.Join("\n", errors.Select(e => "  - " + e));
        }
    }
}
