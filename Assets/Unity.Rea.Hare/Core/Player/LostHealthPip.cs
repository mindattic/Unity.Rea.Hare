using System;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Player
{
    /// <summary>
    /// obj_losthealth, engine-free: the pip player_hurt leaves where the HP it just lost was drawn,
    /// <c>instance_create(obj_healthbar.column2 + player.HP * obj_healthbar.width, view_hview - obj_healthbar.height - 36,
    /// obj_losthealth)</c>, drawn (spr_lost_health, origin top left) on the GUI layer. Each step it is not paused it
    /// rises a pixel and loses 0.01 alpha, and it is gone once its alpha reaches 0 (100 steps).
    /// </summary>
    public sealed class LostHealthPip
    {
        public const string Sprite = "spr_lost_health";

        readonly HealthTuning tuning;
        float steps;

        /// <param name="healthLeft">player.HP after the hit.</param>
        /// <param name="guiHeight">view_hview: the GUI layer's height (1080).</param>
        public LostHealthPip(int healthLeft, float guiHeight, HealthTuning tuning = null)
        {
            this.tuning = tuning ?? GameContent.Current.Health;
            X = this.tuning.LostHealthColumnX + healthLeft * this.tuning.LostHealthPipWidth;
            Y = guiHeight - this.tuning.LostHealthPipHeight - this.tuning.LostHealthBottomMargin;
            Alpha = 1f;
        }

        /// <summary>The pip's top left on the GUI layer, px.</summary>
        public float X { get; }
        public float Y { get; private set; }
        /// <summary>GML: image_alpha.</summary>
        public float Alpha { get; private set; }
        public bool IsGone => Alpha <= 1e-5f;

        /// <summary>Its Step for <paramref name="deltaTime"/> seconds (nothing while <paramref name="paused"/>); false once gone.</summary>
        public bool Step(float deltaTime, bool paused)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must not be negative.");
            if (IsGone) return false;
            if (paused) return true;
            float n = deltaTime * GameClock.StepsPerSecond;
            steps += n;
            Y -= tuning.LostHealthRisePerStep * n;
            Alpha = Math.Max(0f, 1f - tuning.LostHealthFadePerStep * steps);
            return !IsGone;
        }
    }
}
