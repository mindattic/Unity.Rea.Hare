namespace Unity.Rea.Hare.Pickups
{
    /// <summary>The only way the pickup field changes. Runs in <see cref="PickupField.Execute"/>.</summary>
    public abstract class PickupCommand
    {
        /// <summary>Does the work; false when the command was refused.</summary>
        internal abstract bool Apply(PickupField field);
    }

    /// <summary>GML random_pickup(x, y): an enemy's kill branch rolls for a carrot or drops a coin at <c>at</c>.</summary>
    public sealed class DropRandomPickup : PickupCommand
    {
        readonly Vec2 at;
        public DropRandomPickup(Vec2 at) => this.at = at;
        internal override bool Apply(PickupField field) => field.DropRandom(at);
    }

    /// <summary>instance_create(x, y, obj_carrot / obj_coin) with no roll first (a coin still rolls its own grade).</summary>
    public sealed class DropPickup : PickupCommand
    {
        readonly PickupKind kind;
        readonly Vec2 at;
        public DropPickup(PickupKind kind, Vec2 at)
        {
            this.kind = kind;
            this.at = at;
        }
        internal override bool Apply(PickupField field) { field.Drop(kind, at); return true; }
    }

    /// <summary>The ship <c>id</c> is at <c>position</c> (GML player.x, player.y): the pickups' magnet pulls toward the nearest ship.</summary>
    public sealed class SetPickupTarget : PickupCommand
    {
        readonly PlayerId id;
        readonly Vec2 position;
        public SetPickupTarget(PlayerId id, Vec2 position)
        {
            this.id = id;
            this.position = position;
        }
        internal override bool Apply(PickupField field) { field.SetTarget(id, position); return true; }
    }

    /// <summary>The ship <c>id</c> is gone (or dead): the magnet no longer sees it.</summary>
    public sealed class ClearPickupTarget : PickupCommand
    {
        readonly PlayerId id;
        public ClearPickupTarget(PlayerId id) => this.id = id;
        internal override bool Apply(PickupField field) => field.ClearTarget(id);
    }

    /// <summary>The room the coins and scraps fall out of (room_height + sprite_height).</summary>
    public sealed class SetPickupRoom : PickupCommand
    {
        readonly RoomBounds room;
        public SetPickupRoom(RoomBounds room) => this.room = room;
        internal override bool Apply(PickupField field) { field.Room = room; return true; }
    }

    /// <summary>GML paused: the pickups' and scraps' gated work stops (their alarms keep counting).</summary>
    public sealed class SetPickupsPaused : PickupCommand
    {
        readonly bool paused;
        public SetPickupsPaused(bool paused) => this.paused = paused;
        internal override bool Apply(PickupField field)
        {
            if (field.Paused == paused) return false;
            field.Paused = paused;
            return true;
        }
    }

    /// <summary><c>dt</c> seconds of steps: alarms, Step events, the built-in motion, in GameMaker's order.</summary>
    public sealed class TickPickups : PickupCommand
    {
        readonly float dt;
        public TickPickups(float dt) => this.dt = dt;
        internal override bool Apply(PickupField field) { field.Tick(dt * GameClock.StepsPerSecond); return true; }
    }

    /// <summary>The ship <c>by</c> touched pickup <c>pickupId</c> (its collision event with obj_player). Refused when it is gone.</summary>
    public sealed class CollectPickup : PickupCommand
    {
        readonly PlayerId by;
        readonly int pickupId;
        public CollectPickup(PlayerId by, int pickupId)
        {
            this.by = by;
            this.pickupId = pickupId;
        }
        internal override bool Apply(PickupField field) => field.Collect(by, pickupId);
    }

    /// <summary>
    /// GML spawn_chips(feetX, feetY): 6-12 obj_chip at the ship's feet, which player_leap and the par_rock landing find
    /// <c>FeetDistancePx</c> along <c>imageAngle</c> from <c>shipAt</c>.
    /// </summary>
    public sealed class KickUpChips : PickupCommand
    {
        readonly Vec2 shipAt;
        readonly float imageAngle;
        public KickUpChips(Vec2 shipAt, float imageAngle)
        {
            this.shipAt = shipAt;
            this.imageAngle = imageAngle;
        }
        internal override bool Apply(PickupField field) => field.SpawnChips(field.FeetOf(shipAt, imageAngle), imageAngle) > 0;
    }

    /// <summary>shoot_vulcan's obj_cartridge or shoot_shotgun's shell, at the gun (player.gunX, player.gunY).</summary>
    public sealed class EjectCasing : PickupCommand
    {
        readonly ScrapKind kind;
        readonly Vec2 at;
        public EjectCasing(ScrapKind kind, Vec2 at)
        {
            this.kind = kind;
            this.at = at;
        }
        internal override bool Apply(PickupField field) => field.SpawnCasing(kind, at) != null;
    }

    /// <summary>setup_level's <c>with(par_pickup) instance_destroy()</c>. Chips and casings are not pickups and stay.</summary>
    public sealed class ClearPickups : PickupCommand
    {
        internal override bool Apply(PickupField field) => field.ClearAllPickups();
    }

    /// <summary>A pickup's or scrap's view was destroyed from outside (a scene unloading): it leaves the field quietly.</summary>
    public sealed class ForgetPickup : PickupCommand
    {
        readonly int id;
        public ForgetPickup(int id) => this.id = id;
        internal override bool Apply(PickupField field) => field.Forget(id);
    }
}
