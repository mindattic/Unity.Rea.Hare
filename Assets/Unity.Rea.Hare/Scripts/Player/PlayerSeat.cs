using System;
using UnityEngine;

namespace Unity.Hare.Player
{
    /// <summary>
    /// A ship's seat in the Core world (no global current player: every view finds its own seat on its own ship).
    /// Local play seats one player in a world of its own; an authority that owns a shared world seats the ship
    /// with <see cref="Bind"/> before any view touches it. Views send commands through <see cref="Send"/> and the
    /// events they raise come back out of <see cref="EventRaised"/> for presentation to play.
    /// </summary>
    public sealed class PlayerSeat : MonoBehaviour
    {
        PlayerWorld world;
        PlayerShip ship;
        int nextKey;

        public PlayerId Id { get; private set; }
        public PlayerWorld World { get { Ensure(); return world; } }

        /// <summary>The Core state of this seat's ship (read-only for presentation).</summary>
        public PlayerShip Ship { get { Ensure(); return ship; } }

        /// <summary>Every event a command raised, in order, after the command ran.</summary>
        public event Action<PlayerEvent> EventRaised;

        /// <summary>The seat on <paramref name="ship"/>, adding and seating it on first use (scenes, tests).</summary>
        public static PlayerSeat Of(GameObject ship)
        {
            var seat = ship.GetComponent<PlayerSeat>();
            if (seat == null) seat = ship.AddComponent<PlayerSeat>(); // Awake does not run in Edit Mode, so Ensure is lazy
            seat.Ensure();
            return seat;
        }

        /// <summary>Seats this ship as <paramref name="id"/> in a world an authority owns. Call before any view uses it.</summary>
        public void Bind(PlayerWorld sharedWorld, PlayerId id)
        {
            if (ship != null)
                throw new InvalidOperationException("This ship is already seated; add the seat and Bind it before adding the views.");
            world = sharedWorld;
            Id = id;
            ship = world.Has(id) ? world.Get(id) : world.AddPlayer(id);
        }

        void Ensure()
        {
            if (ship != null) return;
            Bind(new PlayerWorld(UnityEngine.Random.Range), new PlayerId(1));
        }

        /// <summary>A key for a gun or trigger holder that is unique on this seat.</summary>
        public int AllocateKey() => ++nextKey;

        /// <summary>Plays events a command run elsewhere (the enemy world hurting this ship) raised on this seat.</summary>
        public void Play(System.Collections.Generic.IReadOnlyList<PlayerEvent> events)
        {
            var raised = EventRaised;
            if (raised == null) return;
            foreach (var e in events) raised(e);
        }

        /// <summary>Runs a command on this seat's world and plays its events to the views.</summary>
        public CommandResult Send(PlayerCommand command)
        {
            Ensure();
            var result = world.Execute(command);
            var raised = EventRaised;
            if (raised != null)
                foreach (var e in result.Events) raised(e);
            return result;
        }
    }

    /// <summary>
    /// Base of the thin views on a ship: finds the seat lazily, sends commands, and plays the events its ship
    /// raises. A view never changes game state itself.
    /// </summary>
    public abstract class SeatView : MonoBehaviour
    {
        PlayerSeat seat;

        /// <summary>This ship's seat, subscribing the view to its events on first use.</summary>
        protected PlayerSeat Seat
        {
            get
            {
                if (seat == null)
                {
                    seat = PlayerSeat.Of(gameObject);
                    seat.EventRaised += OnPlayerEvent;
                }
                return seat;
            }
        }

        protected bool HasSeat => seat != null;

        /// <summary>The Core state of this ship, for reading.</summary>
        protected PlayerShip Core => Seat.Ship;

        protected PlayerId Id => Seat.Id;

        protected CommandResult Send(PlayerCommand command) => Seat.Send(command);

        /// <summary>Plays one event the ship raised (any command, from any view).</summary>
        protected virtual void OnPlayerEvent(PlayerEvent e) { }

        protected virtual void OnDestroy()
        {
            if (seat != null) seat.EventRaised -= OnPlayerEvent;
        }
    }
}
