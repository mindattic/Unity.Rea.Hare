using Unity.Shared.Core;
using Unity.Shared.Core.Json;

namespace Unity.Hare
{
    /// <summary>
    /// Hare's own block of settings.json: Unity.Shared's free-form <c>Profile.Settings.Game</c>, typed for this
    /// game. The original kept these as the globals <c>difficulty</c> and <c>use_metric</c>; its <c>master_volume</c>
    /// is superseded by Unity.Shared's shared <see cref="CommonSettings"/> (<c>MusicVolume</c>/<c>SfxVolume</c>/
    /// <c>MuteMusic</c>/<c>MuteSfx</c>), kept the same way across every MindAttic game instead of Hare's own.
    /// Each property reads straight from, and writes straight into, the underlying <see cref="Data"/> object, so
    /// fields a later version doesn't know about survive a load and save. Change a value, then call
    /// <see cref="Profile.SaveSettings"/> to write it.
    /// </summary>
    public sealed class HareSettings
    {
        /// <summary>JSON field names, as written in settings.json's <c>game</c> block.</summary>
        public static class Keys
        {
#pragma warning disable CS1591
            public const string Difficulty = "difficulty";
            public const string UseMetric = "useMetric";
            public const string Fullscreen = "fullscreen";
#pragma warning restore CS1591
        }

        /// <summary>Generator mode's difficulty before the player changes it: midway between gmlHARE's
        /// obj_btn_diff0 and obj_btn_diff100 point targets.</summary>
        public const int DefaultDifficulty = 50;

        /// <summary>The lowest difficulty (obj_btn_diff0).</summary>
        public const int MinDifficulty = 0;

        /// <summary>The highest difficulty (obj_btn_diff100).</summary>
        public const int MaxDifficulty = 100;

        /// <summary>Whether distances and speeds show in metric before the player changes it.</summary>
        public const bool DefaultUseMetric = false;

        /// <summary>Whether the game runs fullscreen before the player changes it.</summary>
        public const bool DefaultFullscreen = true;

        /// <summary>A settings block over <paramref name="data"/> (normally <c>Profile.Settings.Game</c>).</summary>
        public HareSettings(JsonObject data) => Data = data;

        /// <summary>The raw <c>game</c> object, unknown fields and all.</summary>
        public JsonObject Data { get; }

        /// <summary>Generator mode's difficulty, as a point target (<see cref="MinDifficulty"/>-<see cref="MaxDifficulty"/>,
        /// clamped on read and write; see <c>generate_enemy_list</c>, docs/REVERSE_ENGINEERING.md).</summary>
        public int Difficulty
        {
            get => Clamp(Data.GetInt(Keys.Difficulty, DefaultDifficulty));
            set => Data.Set(Keys.Difficulty, Clamp(value));
        }

        /// <summary>Whether distances and speeds show in metric.</summary>
        public bool UseMetric
        {
            get => Data.GetBool(Keys.UseMetric, DefaultUseMetric);
            set => Data.Set(Keys.UseMetric, value);
        }

        /// <summary>Whether the game runs fullscreen.</summary>
        public bool Fullscreen
        {
            get => Data.GetBool(Keys.Fullscreen, DefaultFullscreen);
            set => Data.Set(Keys.Fullscreen, value);
        }

        /// <summary>Writes every field this block owns that is missing, with its default.</summary>
        public void FillDefaults()
        {
            if (!(Data[Keys.Difficulty] is JsonNumber)) Difficulty = DefaultDifficulty;
            if (!(Data[Keys.UseMetric] is bool)) UseMetric = DefaultUseMetric;
            if (!(Data[Keys.Fullscreen] is bool)) Fullscreen = DefaultFullscreen;
        }

        static int Clamp(int value) => value < MinDifficulty ? MinDifficulty : value > MaxDifficulty ? MaxDifficulty : value;
    }
}
