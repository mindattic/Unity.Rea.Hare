using System;

namespace Unity.Hare.Player
{
    /// <summary>
    /// Where a ship's lives are counted: globals.gml's LIVES. obj_player never owns LIVES in the original; player_hurt's
    /// death and the out-of-bounds rule spend the game's one global. A ship counts its own (<see cref="LivesCounter"/>)
    /// until the room it plays in hands it the session's (<see cref="Session.SessionLives"/>, U.HARE-T54), so a death and
    /// a flight out of the arena take from the same LIVES the HUD and game_restart read.
    /// </summary>
    public interface ILivesLedger
    {
        /// <summary>GML: LIVES.</summary>
        int Lives { get; }

        /// <summary><c>LIVES -= 1</c>, never below 0 (docs/BIBLE.md D14).</summary>
        void Spend();
    }

    /// <summary>A ship's own LIVES, for a ship that plays outside a session (tests, a room loaded on its own).</summary>
    public sealed class LivesCounter : ILivesLedger
    {
        public LivesCounter(int lives = 0) => Set(lives);

        public int Lives { get; private set; }

        public void Set(int lives)
        {
            if (lives < 0) throw new ArgumentOutOfRangeException(nameof(lives), lives, "Must not be negative.");
            Lives = lives;
        }

        public void Spend() => Lives = Math.Max(0, Lives - 1);
    }

    /// <summary>
    /// obj_player's Alarm 5 colour, the <c>color</c> it draws with: while invulnerable the alarm steps colorCycle
    /// 0..4 every 3 steps, and case 4 has no colour of its own, so cornflower blue holds for two beats.
    /// </summary>
    public enum ShipColor
    {
        White,
        Pink,
        Amber,
        CornflowerBlue,
    }

    /// <summary>
    /// The sprite obj_player draws (its sprite_index), as player_hurt, Alarm 7, reset_player and the landing, crouch and
    /// key events set it: asset_leap in flight, asset_crouch while crouching or shielding on a perch, else asset_idle,
    /// each with its *_hurt pose while hurting.
    /// </summary>
    public static class ShipPose
    {
        public const string Leap = "spr_rabbit_leap";
        public const string LeapHurt = "spr_rabbit_leap_hurt";
        public const string Idle = "spr_rabbit_idle";
        public const string IdleHurt = "spr_rabbit_idle_hurt";
        public const string Crouch = "spr_rabbit_crouch";
        public const string CrouchHurt = "spr_rabbit_crouch_hurt";

        /// <summary>The GML sprite for a ship in <paramref name="state"/>.</summary>
        public static string SpriteFor(PlayerFlightState state, bool crouching, bool shielding, bool hurting)
        {
            if (state == PlayerFlightState.Leaving) return Leap; // state_leave: sprite_index = asset_leap, every step (U.HARE-T22)
            if (state == PlayerFlightState.Moving || state == PlayerFlightState.Dead) return hurting ? LeapHurt : Leap;
            if (crouching || shielding) return hurting ? CrouchHurt : Crouch;
            return hurting ? IdleHurt : Idle;
        }
    }
}
