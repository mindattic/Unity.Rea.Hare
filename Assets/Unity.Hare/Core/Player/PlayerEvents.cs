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

    /// <summary>
    /// The ship perched (GML landing collisions): on <see cref="Surface"/> (null for a landing with no surface to ride)
    /// with id <see cref="SurfaceId"/>, now facing <see cref="Facing"/> degrees (the surface normal).
    /// </summary>
    public sealed class ShipLanded : PlayerEvent
    {
        public SurfaceKind? Surface;
        public int SurfaceId;
        public float Facing;
    }

    /// <summary>The ship left a perch (GML player_leap).</summary>
    public sealed class ShipLeapt : PlayerEvent
    {
        /// <summary>
        /// The image_angle it had on its perch: player_leap kicks up dirt (ef_firework, spawn_chips) at its feet,
        /// 32 px along this, before it turns to the aim.
        /// </summary>
        public float PerchedFacing;
    }

    /// <summary>fling_player threw the ship off its perch, heading <see cref="Direction"/> at <see cref="Speed"/>, spinning.</summary>
    public sealed class ShipFlung : PlayerEvent
    {
        public float Direction;
        public float Speed;
    }

    /// <summary>
    /// obj_level_results' Enter sent the ship off the level (U.HARE-T22: state_leave, invulnerable for good), off its
    /// perch if it had one, heading <see cref="Direction"/> for now.
    /// </summary>
    public sealed class ShipLeaving : PlayerEvent { public float Direction; }

    /// <summary>The ship stopped dead (player_hurt's HP-reached-0 branch).</summary>
    public sealed class ShipHalted : PlayerEvent { }

    /// <summary>player_shield raised the obj_shieldwall.</summary>
    public sealed class ShieldRaised : PlayerEvent { public float Radius; }

    /// <summary>The shield wall came down (key release, leap, death, reset or energy at zero).</summary>
    public sealed class ShieldDropped : PlayerEvent { }

    /// <summary>
    /// A hit that left the ship alive; <see cref="Health"/> is the HP remaining. player_hurt plays sfx_hurt<see cref="HurtSound"/>
    /// (1..8) and puts an obj_losthealth where the lost pip was.
    /// </summary>
    public sealed class PlayerDamaged : PlayerEvent { public int Health; public int HurtSound = 1; }

    /// <summary>A carrot gave back HP; <see cref="Health"/> is the HP now.</summary>
    public sealed class PlayerHealed : PlayerEvent { public int Health; }

    /// <summary>HP reached 0 and LIVES went down by one; <see cref="Lives"/> is the lives remaining.</summary>
    public sealed class PlayerDied : PlayerEvent { public int Lives; }

    /// <summary>The ship flew out of the arena past its buffer zone and lost a life; <see cref="Lives"/> is the lives remaining.</summary>
    public sealed class PlayerLeftArena : PlayerEvent { public int Lives; }

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

    /// <summary>The air loadout fired <see cref="Weapon"/>: spawn its rocket or mine.</summary>
    public sealed class AirWeaponFired : PlayerEvent { public AirWeapon Weapon; }

    /// <summary>The air loadout was pulled without ammo.</summary>
    public sealed class AirWeaponDryFired : PlayerEvent { public AirWeapon Weapon; }

    /// <summary>The laser gun's beam went out (trigger release, switch, death or shield): destroy obj_laser.</summary>
    public sealed class GunBeamStopped : PlayerEvent { public int Key; }

    /// <summary>Collects the events one command raises, stamping them with the seat they belong to.</summary>
    internal sealed class EventSink
    {
        public EventSink(PlayerId id) => Id = id;

        public PlayerId Id { get; }
        public List<PlayerEvent> Events { get; } = new List<PlayerEvent>();

        /// <summary>Every event as it is emitted, for the ship's own parts that follow others' (<see cref="ShipLook"/>).</summary>
        public event System.Action<PlayerEvent> Emitted;

        public void Emit(PlayerEvent e)
        {
            e.Id = Id;
            Events.Add(e);
            Emitted?.Invoke(e);
        }
    }
}
