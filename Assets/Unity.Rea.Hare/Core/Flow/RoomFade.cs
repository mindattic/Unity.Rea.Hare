using System;

namespace Unity.Rea.Hare.Flow
{
    /// <summary>obj_fade_*'s <c>mode</c>: "off", "fadein" (to opaque) or "fadeout" (to clear).</summary>
    public enum FadeMode
    {
        Off,
        FadeIn,
        FadeOut,
    }

    /// <summary>
    /// One room's <c>obj_fade_*</c> (obj_fade_game, _map, _story, _title, _upgrade: the same Create and Step, only what
    /// happens at opaque differs, <see cref="FadeKind"/>). Created with the room: <c>image_alpha = 1</c>,
    /// <c>mode = "fadeout"</c>, <c>firstPass = true</c>, <c>executeOnce = false</c>, so every room with a fade
    /// comes up from black. A room's buttons start the fade-in by setting <c>mode = "fadein"</c> and
    /// <c>firstPass = true</c> (<see cref="BeginFadeIn"/>). The alpha is GameMaker's double and steps by
    /// <c>fadespeed</c> (0.03) a step exactly as the Step event does, including the first pass resetting it.
    /// </summary>
    public sealed class RoomFade
    {
        public RoomFade(FadeKind kind, double fadeSpeed)
        {
            if (kind == FadeKind.None) throw new ArgumentException("A room with no fade has no obj_fade_*.", nameof(kind));
            if (!(fadeSpeed > 0d)) throw new ArgumentOutOfRangeException(nameof(fadeSpeed), fadeSpeed, "Must be greater than 0.");
            Kind = kind;
            FadeSpeed = fadeSpeed;
            Alpha = 1d;
            Mode = FadeMode.FadeOut;
            FirstPass = true;
            ExecuteOnce = false;
        }

        /// <summary>What the fade does at opaque.</summary>
        public FadeKind Kind { get; }
        /// <summary>GML: fadespeed.</summary>
        public double FadeSpeed { get; }
        /// <summary>GML: image_alpha, unclamped (it overshoots to 1.02 and -0.02 exactly as GameMaker's does).</summary>
        public double Alpha { get; private set; }
        /// <summary>GML: mode.</summary>
        public FadeMode Mode { get; private set; }
        /// <summary>GML: firstPass.</summary>
        public bool FirstPass { get; private set; }
        /// <summary>GML: executeOnce; set once the fade-in has acted, so it acts only once.</summary>
        public bool ExecuteOnce { get; private set; }

        /// <summary>The alpha draw_self actually draws with: image_alpha clamped to 0..1.</summary>
        public float Opacity => (float)Math.Max(0d, Math.Min(1d, Alpha));

        /// <summary>A button's <c>fade.mode = "fadein"; fade.firstPass = true;</c>.</summary>
        internal void BeginFadeIn()
        {
            Mode = FadeMode.FadeIn;
            FirstPass = true;
        }

        /// <summary>
        /// The Step event, once. Returns true on the one step the fade-in, already opaque, acts
        /// (<c>if(!executeOnce) { executeOnce = true; ... }</c>).
        /// </summary>
        internal bool Step()
        {
            switch (Mode)
            {
                case FadeMode.Off:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Alpha = 0d;
                    }
                    return false;

                case FadeMode.FadeIn:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Alpha = 0d;
                    }
                    if (Alpha < 1d)
                    {
                        Alpha += FadeSpeed;
                        return false;
                    }
                    if (ExecuteOnce) return false;
                    ExecuteOnce = true;
                    return true;

                default: // FadeOut
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Alpha = 1d;
                    }
                    if (Alpha > 0d)
                    {
                        Alpha -= FadeSpeed;
                    }
                    else
                    {
                        FirstPass = true;
                        Mode = FadeMode.Off;
                    }
                    return false;
            }
        }
    }
}
