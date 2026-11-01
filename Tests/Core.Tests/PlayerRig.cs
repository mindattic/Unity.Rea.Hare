using System.Collections.Generic;
using System.Linq;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>One seated player in a world, with the commands the views send as one-liners and every event kept.</summary>
    internal sealed class PlayerRig
    {
        public const float Step = 1f / GameClock.StepsPerSecond;

        public PlayerWorld World;
        public PlayerId Id;
        public PlayerShip Ship;
        public readonly List<PlayerEvent> Events = new List<PlayerEvent>();

        public PlayerRig(int id = 1, WeaponTable table = null, float jitter = 0f)
        {
            World = new PlayerWorld((min, max) => jitter);
            Id = new PlayerId(id);
            Ship = World.AddPlayer(Id, table);
        }

        public CommandResult Do(PlayerCommand command)
        {
            var result = World.Execute(command);
            Events.AddRange(result.Events);
            return result;
        }

        public IEnumerable<T> Seen<T>() where T : PlayerEvent => Events.OfType<T>();

        public ShipFlight Flight => Ship.Flight;
        public ShipHealth Health => Ship.Health;
        public AmmoPools Ammo => Ship.Ammo;

        public PlayerRig Landed()
        {
            Do(new Land(Id));
            return this;
        }

        public int Install(int key, GunType type, WeaponSlot slot)
        {
            Do(new InstallGun(Id, key, type, slot));
            return key;
        }

        /// <summary>Ticks a gun one GML step at a time and returns how many shots left it.</summary>
        public int RunGun(int key, int steps)
        {
            int shots = 0;
            for (int i = 0; i < steps; i++) shots += Do(new TickGun(Id, key, Step)).Events.Count(e => e is GunFired);
            return shots;
        }

        public void RunAir(float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) Do(new TickAir(Id, Step));
        }
    }
}
