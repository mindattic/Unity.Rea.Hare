using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The only way the enemy world changes. A command runs in <see cref="EnemyWorld.Execute"/>, which returns whether
    /// it took effect, what it made or struck, and the events it raised. Views translate what Unity tells them (a
    /// contact, a tick, a ship's position) into these and play the events.
    /// </summary>
    public abstract class EnemyCommand
    {
        internal abstract bool Apply(EnemyWorld world, EnemyResult result);
    }

    // ---- The world itself ----

    /// <summary>A seat's ship moved: the enemies chase, lead and flee the nearest ship (GML's global player).</summary>
    public sealed class SetTarget : EnemyCommand
    {
        readonly PlayerId id;
        readonly Vec2 position, velocity;

        public SetTarget(PlayerId id, Vec2 position, Vec2 velocityPxPerStep)
        {
            this.id = id;
            this.position = position;
            velocity = velocityPxPerStep;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            world.SetTarget(id, position, velocity);
            return true;
        }
    }

    /// <summary>A seat's ship is gone: enemies stop seeing it.</summary>
    public sealed class ClearTarget : EnemyCommand
    {
        readonly PlayerId id;
        public ClearTarget(PlayerId id) => this.id = id;
        internal override bool Apply(EnemyWorld world, EnemyResult result) => world.ClearTarget(id);
    }

    /// <summary>GML globals paused.</summary>
    public sealed class SetPaused : EnemyCommand
    {
        readonly bool paused;
        public SetPaused(bool paused) => this.paused = paused;

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            world.Paused = paused;
            return true;
        }
    }

    // ---- Making things ----

    /// <summary>GML instance_create(x, y, instance) for an enemy kind, with its Create event. The result carries the body.</summary>
    public sealed class SpawnEnemy : EnemyCommand
    {
        readonly EnemyKind kind;
        readonly Vec2 position;
        readonly RoomBounds? room;
        readonly EnemyOptions options;

        public SpawnEnemy(EnemyKind kind, Vec2 position, RoomBounds? room = null, EnemyOptions options = null)
        {
            this.kind = kind;
            this.position = position;
            this.room = room;
            this.options = options;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            result.Body = world.Spawn(kind, position, room, options);
            return true;
        }
    }

    /// <summary>GML instance_create(x, y, obj_enemy_mine) on its own: a mine laid by <c>owner</c> (a trilobyte, or null for a bare one).</summary>
    public sealed class LayMine : EnemyCommand
    {
        readonly EnemyBody owner;
        readonly Vec2 position;
        readonly float? imageAngle;
        readonly int? fuseSeconds;

        public LayMine(EnemyBody owner, Vec2 position, float? imageAngle = null, int? fuseSeconds = null)
        {
            this.owner = owner;
            this.position = position;
            this.imageAngle = imageAngle;
            this.fuseSeconds = fuseSeconds;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var mine = new EnemyMineBody(owner, imageAngle, fuseSeconds);
            world.AddAt(mine, position, owner != null ? owner.Room : (RoomBounds?)null);
            result.Body = mine;
            return true;
        }
    }

    /// <summary>GML instance_create(0, 0, obj_butterfly / obj_firefly / obj_fly): one insect, which then places itself.</summary>
    public sealed class SpawnInsect : EnemyCommand
    {
        readonly InsectKind kind;
        readonly RoomBounds? room;
        readonly Vec2? position;

        public SpawnInsect(InsectKind kind, RoomBounds? room = null, Vec2? position = null)
        {
            this.kind = kind;
            this.room = room;
            this.position = position;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            result.Insect = world.AddInsect(kind, room, position);
            return true;
        }
    }

    /// <summary>GML instance_create(x, y, par_enemy_emitter) followed by setup_level's field writes.</summary>
    public sealed class AddEmitter : EnemyCommand
    {
        readonly EnemyKind kind;
        readonly Vec2 position;
        readonly int instanceMin, instanceMax, waitMin, waitMax;
        readonly float scale;
        readonly RoomBounds? room;
        readonly int? firstAlarm;

        /// <summary><paramref name="waitMinSteps"/>/<paramref name="waitMaxSteps"/> default to the loaded <see cref="EmitterTiming"/> when null.</summary>
        public AddEmitter(EnemyKind kind, Vec2 position, int instanceMin, int instanceMax, float scale, RoomBounds? room = null,
            int? waitMinSteps = null, int? waitMaxSteps = null, int? firstAlarmSteps = null)
        {
            this.kind = kind;
            this.position = position;
            this.instanceMin = instanceMin;
            this.instanceMax = instanceMax;
            this.scale = scale;
            this.room = room;
            waitMin = waitMinSteps ?? GameContent.Current.Emitters.DefaultWaitMinSteps;
            waitMax = waitMaxSteps ?? GameContent.Current.Emitters.DefaultWaitMaxSteps;
            firstAlarm = firstAlarmSteps;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var emitter = world.AddVortex(new EmitterBody());
            emitter.Create(kind, position, instanceMin, instanceMax, scale, room, waitMin, waitMax, firstAlarm);
            result.Vortex = emitter;
            return true;
        }
    }

    /// <summary>create_wormhole(kind): a one-shot vortex for a single enemy.</summary>
    public sealed class AddWormhole : EnemyCommand
    {
        readonly EnemyKind kind;
        readonly RoomBounds? room;
        readonly Vec2? position;

        public AddWormhole(EnemyKind kind, RoomBounds? room = null, Vec2? position = null)
        {
            this.kind = kind;
            this.room = room;
            this.position = position;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var wormhole = world.AddVortex(new WormholeBody());
            wormhole.Create(kind, room, position);
            result.Vortex = wormhole;
            return true;
        }
    }

    /// <summary>setup_level's generator branch: an obj_wormhole_emitter over a generated list (see <see cref="EnemyList"/>).</summary>
    public sealed class AddWormholeEmitter : EnemyCommand
    {
        readonly IEnumerable<EnemyKind> list;
        readonly int? difficulty;
        readonly RoomBounds? room;
        readonly int? firstRate;

        /// <summary>Over an explicit enemy list.</summary>
        public AddWormholeEmitter(IEnumerable<EnemyKind> list, RoomBounds? room = null, int? firstRateSteps = null)
        {
            this.list = list;
            this.room = room;
            firstRate = firstRateSteps;
        }

        /// <summary>Over generate_enemy_list(<paramref name="difficulty"/>), dealt from the world's random source.</summary>
        public AddWormholeEmitter(int difficulty, RoomBounds? room = null, int? firstRateSteps = null)
        {
            this.difficulty = difficulty;
            this.room = room;
            firstRate = firstRateSteps;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var enemies = difficulty.HasValue ? EnemyList.Generate(difficulty.Value, world.Random) : list;
            var emitter = world.AddWormholeEmitter(new WormholeEmitterBody());
            emitter.Create(enemies, room, firstRate);
            result.Emitter = emitter;
            return true;
        }
    }

    /// <summary>setup_level's Place for the enemy half of a level: see <see cref="EnemyComposition"/>.</summary>
    public sealed class PlaceComposition : EnemyCommand
    {
        readonly EnemyComposition composition;
        readonly RoomBounds? room;
        readonly IDictionary<EnemyKind, int> counts;

        public PlaceComposition(EnemyComposition composition, RoomBounds? room = null, IDictionary<EnemyKind, int> counts = null)
        {
            this.composition = composition;
            this.room = room;
            this.counts = counts;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            result.Placement = composition.Place(world, room ?? world.Room, counts);
            return true;
        }
    }

    // ---- Time ----

    /// <summary>One GML step of one enemy (alarms, Step, built-in motion), <c>dt</c> seconds long.</summary>
    public sealed class TickEnemy : EnemyCommand
    {
        readonly int id;
        readonly float dt;
        public TickEnemy(int id, float dt) { this.id = id; this.dt = dt; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null) return false;
            body.Tick(dt * GameClock.StepsPerSecond);
            result.Body = body;
            return true;
        }
    }

    /// <summary>One GML step of one insect.</summary>
    public sealed class TickInsect : EnemyCommand
    {
        readonly int id;
        readonly float dt;
        public TickInsect(int id, float dt) { this.id = id; this.dt = dt; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var insect = world.GetInsect(id);
            if (insect == null) return false;
            insect.Tick(dt * GameClock.StepsPerSecond);
            result.Insect = insect;
            return true;
        }
    }

    /// <summary>Runs an insect's ai_* script once, now (Alarm 0 without waiting for it).</summary>
    public sealed class ThinkInsect : EnemyCommand
    {
        readonly int id;
        public ThinkInsect(int id) => this.id = id;

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var insect = world.GetInsect(id);
            if (insect == null) return false;
            insect.Think();
            result.Insect = insect;
            return true;
        }
    }

    /// <summary>One GML step of one emitter or wormhole.</summary>
    public sealed class TickVortex : EnemyCommand
    {
        readonly int id;
        readonly float dt;
        public TickVortex(int id, float dt) { this.id = id; this.dt = dt; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var vortex = world.GetVortex(id);
            if (vortex == null) return false;
            vortex.Tick(dt * GameClock.StepsPerSecond);
            result.Vortex = vortex;
            return true;
        }
    }

    /// <summary>One GML step of a wormhole emitter's Alarm 0.</summary>
    public sealed class TickWormholeEmitter : EnemyCommand
    {
        readonly int id;
        readonly float dt;
        public TickWormholeEmitter(int id, float dt) { this.id = id; this.dt = dt; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var emitter = world.GetWormholeEmitter(id);
            if (emitter == null) return false;
            emitter.Tick(dt * GameClock.StepsPerSecond);
            result.Emitter = emitter;
            return true;
        }
    }

    /// <summary>Every enemy, insect, vortex and wormhole emitter takes one step (an authority running the whole room).</summary>
    public sealed class TickWorld : EnemyCommand
    {
        readonly float dt;
        public TickWorld(float dt) => this.dt = dt;

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            float steps = dt * GameClock.StepsPerSecond;
            // Every snapshot is taken before anything ticks, as GML's alarms have already run for the step by the
            // time an instance_create happens: a wormhole an emitter opens, or an enemy a vortex releases, this step
            // is not also ticked this step.
            var emitters = world.EmitterSnapshot();
            var vortices = world.VortexSnapshot();
            var bodies = world.Snapshot();
            var insects = world.InsectSnapshot();
            foreach (var e in emitters) e.Tick(steps);
            foreach (var v in vortices) v.Tick(steps);
            foreach (var b in bodies) b.Tick(steps);
            foreach (var i in insects) i.Tick(steps);
            return true;
        }
    }

    // ---- Weapons meeting enemies ----

    /// <summary>GML Collision obj_bullet on an enemy. Accepted when the bullet is spent; <see cref="EnemyResult.Deflection"/> when a shield bounced it.</summary>
    public sealed class StrikeBullet : EnemyCommand
    {
        readonly int id;
        readonly BulletShot shot;
        public StrikeBullet(int id, BulletShot shot) { this.id = id; this.shot = shot; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null) return false;
            body.ClearDeflection();
            bool spent = body.Strike(shot);
            result.Body = body;
            result.Deflection = body.Deflection;
            return spent;
        }
    }

    /// <summary>GML Collision obj_rocket on an enemy. Accepted when the rocket explodes here.</summary>
    public sealed class StrikeRocket : EnemyCommand
    {
        readonly int id;
        readonly Vec2 at;
        public StrikeRocket(int id, Vec2 rocketAt) { this.id = id; at = rocketAt; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null) return false;
            body.ClearDeflection();
            bool explodes = body.StrikeRocket(at);
            result.Body = body;
            result.Deflection = body.Deflection;
            return explodes;
        }
    }

    /// <summary>GML Collision obj_rocket_explosion on an enemy (most have none).</summary>
    public sealed class StrikeExplosion : EnemyCommand
    {
        readonly int id;
        public StrikeExplosion(int id) => this.id = id;

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null) return false;
            body.StrikeExplosion();
            result.Body = body;
            return true;
        }
    }

    /// <summary>The laser, railgun or flame destroying a par_enemy from its side. Accepted when it died.</summary>
    public sealed class StrikeBeam : EnemyCommand
    {
        readonly int id;
        readonly BeamKind beam;
        public StrikeBeam(int id, BeamKind beam) { this.id = id; this.beam = beam; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null) return false;
            result.Body = body;
            return body.Strike(beam);
        }
    }

    /// <summary>GML Collision obj_shieldwall on an enemy. Accepted when the enemy has that event (and died).</summary>
    public sealed class StrikeShield : EnemyCommand
    {
        readonly int id;
        public StrikeShield(int id) => this.id = id;

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null) return false;
            result.Body = body;
            return body.StrikeShield();
        }
    }

    /// <summary>GML Collision with par_enemy: x/y += lengthdir(2, point_direction(other, self)) for <c>steps</c> steps.</summary>
    public sealed class Separate : EnemyCommand
    {
        readonly int id;
        readonly Vec2 other;
        readonly float steps;
        public Separate(int id, Vec2 other, float steps = 1f) { this.id = id; this.other = other; this.steps = steps; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body == null || body.IsDead) return false;
            body.PushAwayFrom(other, steps);
            result.Body = body;
            return true;
        }
    }

    // ---- Enemies meeting the ship ----

    /// <summary>
    /// Where the player's and the enemies' hit rules meet: an enemy that is a par_enemy touching the ship, or an enemy
    /// round reaching it, hurts it through the ship's own <see cref="TakeHit"/>. Accepted when the hit landed (not
    /// dead, invulnerable or spawn-protected); <see cref="EnemyResult.PlayerResult"/> carries the events its seat plays.
    /// </summary>
    public sealed class HurtPlayer : EnemyCommand
    {
        readonly PlayerWorld players;
        readonly PlayerId player;
        readonly int? enemyId;

        /// <param name="enemyId">The par_enemy touching the ship, or null for an enemy round.</param>
        public HurtPlayer(PlayerWorld players, PlayerId player, int? enemyId = null)
        {
            this.players = players;
            this.player = player;
            this.enemyId = enemyId;
        }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            if (enemyId.HasValue)
            {
                var body = world.Get(enemyId.Value);
                if (body == null || body.IsDead || !body.IsParEnemy) return false;
                result.Body = body;
            }
            if (!players.Has(player)) return false;
            var hit = players.Execute(new TakeHit(player));
            result.PlayerResult = hit;
            return hit.Accepted;
        }
    }

    // ---- Housekeeping ----

    /// <summary>GML x/y = ...: places an enemy, insect or vortex (a level script's, or a test's).</summary>
    public sealed class PlaceBody : EnemyCommand
    {
        readonly int id;
        readonly Vec2 position;
        public PlaceBody(int id, Vec2 position) { this.id = id; this.position = position; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body != null) { body.Teleport(position); return true; }
            var insect = world.GetInsect(id);
            if (insect != null) { insect.Position = position; return true; }
            var vortex = world.GetVortex(id);
            if (vortex != null) { vortex.Position = position; return true; }
            return false;
        }
    }

    /// <summary>Takes an enemy or insect out of the world without a death (its view is gone).</summary>
    public sealed class RemoveBody : EnemyCommand
    {
        readonly int id;
        public RemoveBody(int id) => this.id = id;

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var body = world.Get(id);
            if (body != null) { body.Remove(); return true; }
            var vortex = world.GetVortex(id);
            if (vortex != null) { vortex.Remove(); return true; }
            var emitter = world.GetWormholeEmitter(id);
            if (emitter != null) { world.RemoveWormholeEmitter(emitter); return true; }
            return world.RemoveInsect(id);
        }
    }
}
