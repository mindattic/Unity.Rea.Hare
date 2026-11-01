using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Online
{
    /// <summary>
    /// The content registry's <c>online</c> section (AGENTS.md rule 4). The original has no online play, so none of
    /// these figures is reconstructed: they are the rebuild's own, kept as data so a playtest can change them without
    /// code (BIBLE V18).
    /// </summary>
    public class OnlineTuning
    {
        /// <summary>Most players in one online game, the host's own player included.</summary>
        public int MaxPlayers = 4;

        /// <summary>World units between neighbouring seats' spawn points along the room's horizontal centre line.</summary>
        public float SpawnSpacing = 1.5f;

        /// <summary>How far (degrees) the aim must move before a client sends it to the server again.</summary>
        public float AimResendDegrees = 0.5f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".MaxPlayers", MaxPlayers);
            ValidationHelpers.AtMost(errors, path + ".MaxPlayers", MaxPlayers, 64);
            ValidationHelpers.NonNegative(errors, path + ".SpawnSpacing", SpawnSpacing);
            ValidationHelpers.NonNegative(errors, path + ".AimResendDegrees", AimResendDegrees);
        }
    }
}
