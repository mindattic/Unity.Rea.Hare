using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// One player's ship as the rules see it: flight and energy, health and lives, the four ammo pools, the guns
    /// and the two loadouts. Nothing outside the world changes it except through <see cref="PlayerCommand"/>s.
    /// </summary>
    public sealed class PlayerShip
    {
        internal PlayerShip(PlayerId id, WeaponTable table, Func<float, float, float> random)
        {
            Id = id;
            Sink = new EventSink(id);
            Table = table ?? GameContent.Current.Weapons;
            Flight = new ShipFlight(Sink);
            Health = new ShipHealth(Sink, Flight, random);
            Ammo = new AmmoPools(Sink);
            Guns = new GunRack(Sink, Flight, Ammo, Table);
            Ground = new GroundLoadout(Sink, Guns);
            Air = new AirLoadout(Sink, Flight, Ammo, Table);
        }

        public PlayerId Id { get; }
        public WeaponTable Table { get; }
        public ShipFlight Flight { get; }
        public ShipHealth Health { get; }
        public AmmoPools Ammo { get; }
        public GunRack Guns { get; }
        public GroundLoadout Ground { get; }
        public AirLoadout Air { get; }

        internal EventSink Sink { get; }

        /// <summary>The GML sprite the ship draws now (sprite_index): its pose, hurt or not (<see cref="ShipPose"/>).</summary>
        public string Pose => ShipPose.SpriteFor(Flight.State, Flight.IsCrouching, Flight.IsShielding, Health.IsHurting);

        /// <summary>
        /// GML set_ammo_cost as player_leap, fling_player and the landing collisions call it: reads the ship's
        /// equipped weapons (ground_weapon, ground_weapon_secondary, air_weapon, air_weapon_secondary) and applies
        /// <see cref="AmmoPools.SetAmmoCost"/>.
        /// </summary>
        internal void SetAmmoCostFromLoadout()
        {
            GroundWeapon? primary = Ground.IsActive ? Ground.Current : null;
            GroundWeapon? secondary = null;
            bool hasMine = false;

            foreach (var gun in Guns.Guns)
            {
                if (gun.Type == GunType.MineLauncher) { hasMine = true; continue; }
                if (gun.Slot == WeaponSlot.Primary)
                {
                    if (!Ground.IsActive && primary == null) primary = gun.GroundKind;
                }
                else if (secondary == null)
                {
                    secondary = gun.GroundKind;
                }
            }

            AirWeapon? air = Air.IsActive && Air.IsUnlocked(Air.Current) ? Air.Current : (AirWeapon?)null;
            Ammo.SetAmmoCost(primary, secondary, air, hasMine);
        }
    }

    /// <summary>
    /// The state of every seat. One authority holds it; commands go in carrying the id of the player they act for
    /// and the events they raise come back. Nothing here knows which player is "current".
    /// </summary>
    public sealed class PlayerWorld
    {
        readonly Dictionary<PlayerId, PlayerShip> ships = new Dictionary<PlayerId, PlayerShip>();
        readonly Func<float, float, float> random;

        /// <param name="random">Uniform float in [min, max], injected so tests are deterministic (GML random_range).</param>
        public PlayerWorld(Func<float, float, float> random = null)
        {
            if (random == null)
            {
                var rng = new Random();
                random = (min, max) => min + (float)rng.NextDouble() * (max - min);
            }
            this.random = random;
        }

        public IEnumerable<PlayerShip> Ships => ships.Values;

        /// <summary>Seats a player with a fresh ship.</summary>
        public PlayerShip AddPlayer(PlayerId id, WeaponTable table = null)
        {
            if (ships.ContainsKey(id)) throw new InvalidOperationException("Player " + id + " is already seated.");
            var ship = new PlayerShip(id, table, random);
            ships.Add(id, ship);
            return ship;
        }

        public bool Has(PlayerId id) => ships.ContainsKey(id);

        /// <summary>Unseats a player: its ship leaves the world (an online seat released for good).</summary>
        public bool Remove(PlayerId id) => ships.Remove(id);

        public PlayerShip Get(PlayerId id)
        {
            if (!ships.TryGetValue(id, out var ship)) throw new KeyNotFoundException("No player " + id + " is seated.");
            return ship;
        }

        /// <summary>Runs a command on the ship of the player it carries and returns what it did.</summary>
        public CommandResult Execute(PlayerCommand command)
        {
            var ship = Get(command.Id);
            ship.Sink.Events.Clear();
            bool accepted = command.Apply(ship);
            var events = ship.Sink.Events.Count == 0 ? Array.Empty<PlayerEvent>() : ship.Sink.Events.ToArray();
            return new CommandResult(accepted, events);
        }
    }

    /// <summary>What a command did: whether it took effect, and the events it raised in order.</summary>
    public readonly struct CommandResult
    {
        public CommandResult(bool accepted, IReadOnlyList<PlayerEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<PlayerEvent> Events { get; }

        /// <summary>The first event of type <typeparamref name="T"/>, or null.</summary>
        public T Find<T>() where T : PlayerEvent
        {
            foreach (var e in Events)
                if (e is T match) return match;
            return null;
        }

        public bool Has<T>() where T : PlayerEvent => Find<T>() != null;
    }
}
