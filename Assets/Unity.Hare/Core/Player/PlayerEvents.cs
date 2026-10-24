using System.Collections.Generic;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// Something that happened to a player's ship, returned by every command. Presentation plays these (sound,
    /// effects, spawning a shot's object) and never writes back.
    /// </summary>
    public abstract class PlayerEvent
    {
        /// <summary>The seat it happened to.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>The ship perched (GML landing collisions).</summary>
    public sealed class ShipLanded : PlayerEvent { }

    /// <summary>The ship left a perch (GML player_leap).</summary>
    public sealed class ShipLeapt : PlayerEvent { }

    /// <summary>The ship stopped dead (player_hurt's HP-reached-0 branch).</summary>
    public sealed class ShipHalted : PlayerEvent { }

    /// <summary>player_shield raised the obj_shieldwall.</summary>
    public sealed class ShieldRaised : PlayerEvent { public float Radius; }

    /// <summary>The shield wall came down (key release, leap, death, reset or energy at zero).</summary>
    public sealed class ShieldDropped : PlayerEvent { }

    /// <summary>A hit that left the ship alive; <see cref="Health"/> is the HP remaining.</summary>
    public sealed class PlayerDamaged : PlayerEvent { public int Health; }

    /// <summary>HP reached 0; <see cref="Lives"/> is the lives remaining.</summary>
    public sealed class PlayerDied : PlayerEvent { public int Lives; }

    /// <summary>reset_player: the ship flies back in heading <see cref="Direction"/> degrees.</summary>
    public sealed class PlayerRespawned : PlayerEvent { public float Direction; }

    /// <summary>The last life is gone and the death wait has run out (game_restart).</summary>
    public sealed class GameEnded : PlayerEvent { }

    /// <summary>Alarm 9: a primary pool refilled; <see cref="Full"/> is whether it reached its maximum.</summary>
    public sealed class AmmoReloaded : PlayerEvent { public AmmoPool Pool; public bool Full; }

    /// <summary>A perched or mine weapon (identified by the key it was installed with) fired a shot.</summary>
    public sealed class GunFired : PlayerEvent { public int Key; public GunType Type; }

    /// <summary>A trigger pull with too little ammo (sfx_empty_clip).</summary>
    public sealed class GunDryFired : PlayerEvent { public int Key; public GunType Type; }

    /// <summary>The vulcan's spin-up, fire loop and spin-down cues.</summary>
    public sealed class VulcanCue : PlayerEvent { public int Key; public VulcanSound Sound; }

    /// <summary>A gun was installed, moved to another slot or removed, so the ground cycle was re-read.</summary>
    public sealed class GroundLoadoutChanged : PlayerEvent { }

    /// <summary>next_ground_weapon equipped <see cref="Weapon"/> (sfx_switch).</summary>
    public sealed class GroundWeaponSwitched : PlayerEvent { public GroundWeapon Weapon; }

    /// <summary>next_air_weapon equipped <see cref="Weapon"/> (sfx_switch).</summary>
    public sealed class AirWeaponSwitched : PlayerEvent { public AirWeapon Weapon; }

    /// <summary>The air loadout fired <see cref="Weapon"/>: spawn its laser, rail or rocket.</summary>
    public sealed class AirWeaponFired : PlayerEvent { public AirWeapon Weapon; }

    /// <summary>The air loadout was pulled without ammo.</summary>
    public sealed class AirWeaponDryFired : PlayerEvent { public AirWeapon Weapon; }

    /// <summary>The held laser went out (trigger release, switch, death or shield).</summary>
    public sealed class AirLaserStopped : PlayerEvent { }

    /// <summary>Collects the events one command raises, stamping them with the seat they belong to.</summary>
    internal sealed class EventSink
    {
        public EventSink(PlayerId id) => Id = id;

        public PlayerId Id { get; }
        public List<PlayerEvent> Events { get; } = new List<PlayerEvent>();

        public void Emit(PlayerEvent e)
        {
            e.Id = Id;
            Events.Add(e);
        }
    }
}
