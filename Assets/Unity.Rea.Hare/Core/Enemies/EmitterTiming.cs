using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// par_enemy_emitter's clock (gmlHARE, <see cref="EmitterBody"/>): Create's random first alarm, how long each of
    /// open/spawn/close lasts, and setup_level's default instance_wait_min/max for every emitter it places. Used to
    /// live as <c>const</c> fields on <see cref="EmitterBody"/>; now it is loaded content, like every other timing
    /// number (AGENTS.md rule 4).
    /// </summary>
    [Serializable]
    public class EmitterTiming
    {
        /// <summary>Create's alarm[0] = irandom_range(1, room_speed * 2).</summary>
        public int FirstAlarmMaxSteps = 240;
        /// <summary>Open, spawn and close each last alarm[0] = 120 steps.</summary>
        public int PhaseSteps = 120;
        /// <summary>setup_level's instance_wait_min for every emitter it places.</summary>
        public int DefaultWaitMinSteps = 200;
        /// <summary>setup_level's instance_wait_max for every emitter it places.</summary>
        public int DefaultWaitMaxSteps = 500;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".FirstAlarmMaxSteps", FirstAlarmMaxSteps);
            ValidationHelpers.Positive(errors, path + ".PhaseSteps", PhaseSteps);
            ValidationHelpers.Positive(errors, path + ".DefaultWaitMinSteps", DefaultWaitMinSteps);
            ValidationHelpers.Positive(errors, path + ".DefaultWaitMaxSteps", DefaultWaitMaxSteps);
            if (DefaultWaitMinSteps > DefaultWaitMaxSteps)
                errors.Add($"{path}: DefaultWaitMinSteps ({DefaultWaitMinSteps}) must not be greater than DefaultWaitMaxSteps ({DefaultWaitMaxSteps}).");
        }
    }
}
