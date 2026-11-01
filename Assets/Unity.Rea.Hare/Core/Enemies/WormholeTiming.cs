using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// obj_wormhole and obj_wormhole_emitter's clock (gmlHARE, generator mode): one_tick, the opening burst's
    /// fraction of the list, and the two re-arm ranges (the first wormhole, then every one after). Used to live as
    /// <c>const</c> fields on <see cref="WormholeBody"/>/<see cref="WormholeEmitterBody"/>; now loaded content.
    /// </summary>
    [Serializable]
    public class WormholeTiming
    {
        /// <summary>one_tick = room_speed * 2.</summary>
        public int OneTickSteps = 240;
        /// <summary>Create's repeat(enemy_count * 0.1): the fraction of the list opened at once.</summary>
        public float OpeningFraction = 0.1f;
        /// <summary>Create's rate = 1 + room_speed * irandom_range(0, 4), in seconds.</summary>
        public int FirstRateMaxSeconds = 4;
        /// <summary>Alarm 0's rate = 1 + room_speed * irandom_range(0, 8), in seconds.</summary>
        public int RateMaxSeconds = 8;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".OneTickSteps", OneTickSteps);
            if (OpeningFraction < 0f || OpeningFraction > 1f)
                errors.Add($"{path}.OpeningFraction: must be between 0 and 1 (was {OpeningFraction}).");
            ValidationHelpers.NonNegative(errors, path + ".FirstRateMaxSeconds", FirstRateMaxSeconds);
            ValidationHelpers.NonNegative(errors, path + ".RateMaxSeconds", RateMaxSeconds);
        }
    }
}
