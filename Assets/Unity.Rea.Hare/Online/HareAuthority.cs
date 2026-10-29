using System.Collections.Generic;
using Unity.Hare.Player;
using Unity.Multiplayer;      // NetSession
using Unity.Multiplayer.Core; // Seat, Disconnection
using UnityEngine;

namespace Unity.Hare.Online
{
    /// <summary>
    /// The one authority of an online game (U.HARE-T35, BIBLE 5.3): on the server (a listen server's host or a
    /// dedicated server), and only there, it holds the session's <see cref="SharedWorld"/>, runs the commands each
    /// <see cref="HareNetPlayer"/> receives from its owner, steps the world once per fixed step and writes every
    /// ship's state into its player's replicated fields. Clients never have one: they send input and draw what
    /// arrives. One per server session (a test process runs a host and its clients side by side), made on first use
    /// by <see cref="For"/> and gone when the session ends.
    /// </summary>
    public sealed class HareAuthority : MonoBehaviour
    {
        static readonly Dictionary<NetSession, HareAuthority> bySession = new Dictionary<NetSession, HareAuthority>();

        readonly Dictionary<PlayerId, HareNetPlayer> players = new Dictionary<PlayerId, HareNetPlayer>();
        NetSession session;

        /// <summary>The state this authority owns. Change it only through <see cref="Execute"/>.</summary>
        public SharedWorld World { get; private set; }

        /// <summary>The players seated in <see cref="World"/>, by id.</summary>
        public IReadOnlyDictionary<PlayerId, HareNetPlayer> Players => players;

        /// <summary>
        /// The authority of <paramref name="netSession"/>, made the first time its server asks. Only server code
        /// calls this (an entity's own <c>IsServer</c>: a listen server's own player spawns while the host is still
        /// starting, before <see cref="NetSession.IsServer"/> turns true); a client never has an authority.
        /// </summary>
        public static HareAuthority For(NetSession netSession)
        {
            if (netSession == null) throw new System.ArgumentNullException(nameof(netSession));
            if (bySession.TryGetValue(netSession, out var existing) && existing != null) return existing;

            var go = new GameObject("HareAuthority");
            var authority = go.AddComponent<HareAuthority>();
            authority.Attach(netSession);
            bySession[netSession] = authority;
            return authority;
        }

        /// <summary>The authority <paramref name="netSession"/> already has, or null (never makes one).</summary>
        public static HareAuthority Find(NetSession netSession) =>
            netSession != null && bySession.TryGetValue(netSession, out var authority) && authority != null ? authority : null;

        void Attach(NetSession netSession)
        {
            session = netSession;
            World = new SharedWorld(UnityEngine.Random.Range);
            session.SeatReleased += OnSeatReleased;
            session.Ended += OnEnded;
        }

        /// <summary>Seats <paramref name="player"/>'s seat in the world (a returning seat gets its ship back).</summary>
        internal PlayerId Join(HareNetPlayer player, int seatIndex)
        {
            var id = World.Seat(seatIndex);
            players[id] = player;
            player.Publish(World, id);
            return id;
        }

        internal void Leave(HareNetPlayer player)
        {
            foreach (var pair in players)
            {
                if (pair.Value != player) continue;
                players.Remove(pair.Key);
                return;
            }
        }

        /// <summary>Runs a seated player's command (its input, as a client sent it) and returns what it did.</summary>
        public CommandResult Execute(PlayerCommand command) => World.Execute(command);

        void FixedUpdate()
        {
            // A session disposed without raising Ended (a test fixture tearing down) leaves nothing behind either.
            if (session == null || !session.IsActive)
            {
                Destroy(gameObject);
                return;
            }
            Step(Time.fixedDeltaTime);
        }

        /// <summary>The one step: every ship moves, then every player's replicated state is written.</summary>
        public void Step(float dt)
        {
            World.Step(dt);
            foreach (var pair in players)
                if (pair.Value != null) pair.Value.Publish(World, pair.Key);
        }

        void OnSeatReleased(Seat seat)
        {
            var id = SharedWorld.PlayerFor(seat.Index);
            players.Remove(id);
            World.Release(seat.Index);
        }

        void OnEnded(Disconnection why)
        {
            if (this != null) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (session != null)
            {
                session.SeatReleased -= OnSeatReleased;
                session.Ended -= OnEnded;
                if (bySession.TryGetValue(session, out var registered) && registered == this) bySession.Remove(session);
            }
            players.Clear();
        }
    }
}
