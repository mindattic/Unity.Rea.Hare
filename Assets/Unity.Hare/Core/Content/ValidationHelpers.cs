using System.Collections.Generic;

namespace Unity.Hare.Content
{
    /// <summary>Small, repeated checks every content table's own Validate method uses, so the error wording is consistent.</summary>
    static class ValidationHelpers
    {
        public static void Positive(List<string> errors, string path, float value)
        {
            if (!(value > 0f)) errors.Add($"{path}: must be greater than 0 (was {value}).");
        }

        public static void Positive(List<string> errors, string path, int value)
        {
            if (value <= 0) errors.Add($"{path}: must be greater than 0 (was {value}).");
        }

        public static void NonNegative(List<string> errors, string path, float value)
        {
            if (value < 0f) errors.Add($"{path}: must not be negative (was {value}).");
        }

        public static void NonNegative(List<string> errors, string path, int value)
        {
            if (value < 0) errors.Add($"{path}: must not be negative (was {value}).");
        }

        public static void AtMost(List<string> errors, string path, int value, int max)
        {
            if (value > max) errors.Add($"{path}: must be at most {max} (was {value}).");
        }

        public static void Required(List<string> errors, string path, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) errors.Add($"{path}: must not be empty.");
        }
    }
}
