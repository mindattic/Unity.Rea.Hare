using System;
using Unity.Rea.Hare.Player;

namespace Unity.Rea.Hare.Session
{
    /// <summary>
    /// The session's LIVES as a ship's <see cref="ILivesLedger"/> (U.HARE-T54): a death and a flight out of the arena
    /// take from globals.gml's one LIVES, through the session's own <see cref="SpendLife"/> command, so the session
    /// raises its <see cref="LifeLost"/> as for any other change. It reads the session through <c>session</c> each
    /// time, so after game_restart (a new <see cref="GameSession"/>) the ship counts from the new one.
    /// </summary>
    public sealed class SessionLives : ILivesLedger
    {
        readonly Func<GameSession> session;
        readonly PlayerId seat;
        readonly Action<SessionResult> played;

        /// <param name="session">The session whose LIVES the ship spends (read on every use).</param>
        /// <param name="seat">The seat the spend is made for.</param>
        /// <param name="played">Plays what each spend raised (the HUD, sounds); may be null.</param>
        public SessionLives(Func<GameSession> session, PlayerId seat, Action<SessionResult> played = null)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.seat = seat;
            this.played = played;
        }

        public SessionLives(GameSession session, PlayerId seat, Action<SessionResult> played = null)
            : this(() => session, seat, played)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
        }

        public int Lives => session()?.Lives ?? 0;

        public void Spend()
        {
            var current = session();
            if (current == null) return;
            var result = current.Execute(new SpendLife(seat));
            played?.Invoke(result);
        }
    }
}
