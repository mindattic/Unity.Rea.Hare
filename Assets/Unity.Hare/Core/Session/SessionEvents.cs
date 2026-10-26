using System.Collections.Generic;

namespace Unity.Hare.Session
{
    /// <summary>
    /// Something that happened to the session, returned by every <see cref="SessionCommand"/>. The HUD, results
    /// screen, audio and room flow play these and never write back.
    /// </summary>
    public abstract class SessionEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>game_restart: every global is back at its globals.gml value.</summary>
    public sealed class SessionReset : SessionEvent { }

    /// <summary>current_score changed by <see cref="Delta"/> to <see cref="Score"/>.</summary>
    public sealed class ScoreChanged : SessionEvent { public int Score; public int Delta; }

    /// <summary>LIVES went down by one; <see cref="Lives"/> is what is left.</summary>
    public sealed class LifeLost : SessionEvent { public int Lives; }

    /// <summary>obj_coin's hundredth coin: LIVES went up by one (sfx_extralife).</summary>
    public sealed class ExtraLifeEarned : SessionEvent { public int Lives; }

    /// <summary>COINS changed to <see cref="Coins"/>.</summary>
    public sealed class CoinsChanged : SessionEvent { public int Coins; }

    /// <summary>SKILLPOINTS changed to <see cref="SkillPoints"/>.</summary>
    public sealed class SkillPointsChanged : SessionEvent { public int SkillPoints; }

    /// <summary>KILLS changed to <see cref="Kills"/>.</summary>
    public sealed class KillsChanged : SessionEvent { public int Kills; }

    /// <summary>
    /// The out-of-bounds rule found no life to take (<c>if(LIVES &gt; 0) ... else game_restart()</c>); a
    /// <see cref="SessionReset"/> follows.
    /// </summary>
    public sealed class GameOver : SessionEvent { }

    /// <summary>A menu chose the game mode and where it starts (obj_btn_level*, obj_btn_campaign, obj_btn_diff*).</summary>
    public sealed class GameModeChosen : SessionEvent { public GameMode Mode; public int Scenario; public int Level; public int Difficulty; }

    /// <summary>obj_destination set current_scenario.</summary>
    public sealed class ScenarioChanged : SessionEvent { public int Scenario; }

    /// <summary>A level script set its objective; <see cref="Text"/> is set_objective_string's strObjective.</summary>
    public sealed class ObjectiveSet : SessionEvent { public ObjectiveKind Kind; public string Text; public string LevelName; }

    /// <summary>set_level_complete: the objective was met, play pauses and obj_level_results opens.</summary>
    public sealed class LevelCompleted : SessionEvent { public int Level; public ObjectiveKind Objective; }

    /// <summary>current_level went up (the ship flew off the top, or obj_fade_game faded in); setup_level follows.</summary>
    public sealed class LevelAdvanced : SessionEvent { public int Level; }

    /// <summary>obj_healthbar's one-second alarm counted a second of play.</summary>
    public sealed class SecondElapsed : SessionEvent { public int Seconds; }

    /// <summary>obj_game's last-ten-seconds countdown on a time limit (sfx_beep).</summary>
    public sealed class TimeLimitWarning : SessionEvent { public int SecondsLeft; }

    /// <summary>The global paused flag changed.</summary>
    public sealed class PausedChanged : SessionEvent { public bool Paused; }

    /// <summary>master_volume changed (sound_global_volume).</summary>
    public sealed class MasterVolumeChanged : SessionEvent { public float Volume; }

    /// <summary>use_metric changed (the speedometer's units).</summary>
    public sealed class UnitsChanged : SessionEvent { public bool UseMetric; }

    /// <summary>debug changed (obj_game's F12 overlay shown or hidden).</summary>
    public sealed class DebugChanged : SessionEvent { public bool Debug; }

    /// <summary>What a session command did: whether it took effect, and the events it raised in order.</summary>
    public readonly struct SessionResult
    {
        public SessionResult(bool accepted, IReadOnlyList<SessionEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<SessionEvent> Events { get; }

        /// <summary>The first event of type <typeparamref name="T"/>, or null.</summary>
        public T Find<T>() where T : SessionEvent
        {
            if (Events == null) return null;
            foreach (var e in Events)
                if (e is T match) return match;
            return null;
        }
    }
}
