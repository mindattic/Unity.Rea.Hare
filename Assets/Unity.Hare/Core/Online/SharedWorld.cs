using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Online
{
    /// <summary>
    /// The one world an online game's authority holds (U.HARE-T35, BIBLE 5.3): every seat's ship in one
    /// <see cref="PlayerWorld"/>, where each ship is in the room, and the one fixed step that advances them all. The
    /// server (a listen server's host, or a dedicated server) owns exactly one of these per session; clients never
    /// hold one, they send their input as commands and draw the state the server replicates.
    ///
    /// A seat index (Unity.Multiplayer's <c>Seat.Index</c>, 0-based and stable across a reconnect) maps to a
    /// <see cref="PlayerId"/> with <see cref="PlayerFor"/>, so a player who drops and comes back flies the same ship.
    /// </summary>
    public sealed class SharedWorld
    {
        readonly Dictionary<PlayerId, Vec2> positions = new Dictionary<PlayerId, Vec2>();
        readonly List<PlayerId> seated = new List<PlayerId>();
        readonly OnlineTuning tuning;

        /// <param name="random">Uniform float in [min, max] for the player world (GML random_range); null seeds one.</param>
        /// <param name="tuning">The online section; null reads <see cref="GameContent.Current"/>.</param>
        public SharedWorld(Func<float, float, float> random = null, OnlineTuning tuning = null)
        {
            Players = new PlayerWorld(random);
            this.tuning = tuning ?? GameContent.Current.Online;
        }

        /// <summary>Every seated ship's rules. Change them only through <see cref="Execute"/>.</summary>
        public PlayerWorld Players { get; }

        /// <summary>The ships seated now, in the order they were seated.</summary>
        public IReadOnlyList<PlayerId> Seated => seated;

        /// <summary>The player a seat flies as: seat 0 is P1, the same id local play gives its one ship.</summary>
        public static PlayerId PlayerFor(int seatIndex)
        {
            if (seatIndex < 0) throw new ArgumentOutOfRangeException(nameof(seatIndex), seatIndex, "A seat index is never negative.");
            return new PlayerId(seatIndex + 1);
        }

        /// <summary>The seat a player id belongs to (the inverse of <see cref="PlayerFor"/>).</summary>
        public static int SeatOf(PlayerId id) => id.Value - 1;

        /// <summary>
        /// Where a seat's ship starts: on the room's horizontal centre line, seats alternating right and left of the
        /// centre (0 at the centre, where local play's one ship starts; 1 right, 2 left, 3 further right, ...).
        /// </summary>
        public Vec2 SpawnPoint(int seatIndex)
        {
            if (seatIndex < 0) throw new ArgumentOutOfRangeException(nameof(seatIndex), seatIndex, "A seat index is never negative.");
            int step = (seatIndex + 1) / 2;
            float side = seatIndex % 2 == 1 ? 1f : -1f;
            return new Vec2(side * step * tuning.SpawnSpacing, 0f);
        }

        public bool IsSeated(PlayerId id) => positions.ContainsKey(id);

        /// <summary>
        /// Seats <paramref name="seatIndex"/>: a fresh ship at its spawn point, flying (obj_player Create), or, for a
        /// seat already in the world (a player back from a dropped connection), the ship it left, where it left it.
        /// Returns the seat's player id.
        /// </summary>
        public PlayerId Seat(int seatIndex)
        {
            var id = PlayerFor(seatIndex);
            if (positions.ContainsKey(id)) return id;

            if (!Players.Has(id)) Players.AddPlayer(id);
            positions[id] = SpawnPoint(seatIndex);
            seated.Add(id);
            Players.Execute(new ResetFlight(id));
            return id;
        }

        /// <summary>The seat is gone for good (its grace ran out, or it was kicked): its ship leaves the world.</summary>
        public bool Release(int seatIndex)
        {
            var id = PlayerFor(seatIndex);
            if (!positions.Remove(id)) return false;
            seated.Remove(id);
            Players.Remove(id);
            return true;
        }

        /// <summary>Where <paramref name="id"/>'s ship is, in world units.</summary>
        public Vec2 PositionOf(PlayerId id)
        {
            if (!positions.TryGetValue(id, out var position)) throw new KeyNotFoundException("No player " + id + " is seated.");
            return position;
        }

        /// <summary>The ship's rules, for reading.</summary>
        public PlayerShip Ship(PlayerId id) => Players.Get(id);

        /// <summary>Runs one seat's command (its input, as a client sent it) on the shared world.</summary>
        public CommandResult Execute(PlayerCommand command)
        {
            if (!positions.ContainsKey(command.Id)) throw new KeyNotFoundException("No player " + command.Id + " is seated.");
            return Players.Execute(command);
        }

        /// <summary>
        /// The one fixed step: every seated ship's flight advances (energy, turning, speed) and then moves along its
        /// heading at its speed, in the order the ships were seated. Returns every event the step raised.
        /// </summary>
        public IReadOnlyList<PlayerEvent> Step(float dt)
        {
            var events = new List<PlayerEvent>();
            foreach (var id in seated)
            {
                events.AddRange(Players.Execute(new StepFlight(id, dt)).Events);
                positions[id] = positions[id] + Players.Get(id).Flight.Velocity * dt;
            }
            return events;
        }
    }
}
