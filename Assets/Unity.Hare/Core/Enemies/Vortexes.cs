using System;
using System.Collections.Generic;

namespace Unity.Hare.Enemies
{
    /// <summary>A vortex's GML mode string.</summary>
    public enum VortexMode
    {
        Idle,
        Open,
        Spawn,
        Close
    }

    /// <summary>
    /// What par_enemy_emitter and obj_wormhole share (gmlHARE): a half-transparent spr_vortex drawn twice (image_angle
    /// turning -5 a step, back_angle -6) that Alarm 0 walks through idle, open, spawn, close, idle, each change
    /// setting first_pass so the Step event runs that mode's one-off work once. Open grows image_xscale 0.01 a step up
    /// to scale, close shrinks it to 0. Alarms run before Step, as in GML.
    /// </summary>
    public abstract class VortexBody
    {
        /// <summary>spr_vortex is 550x550 with its origin centred.</summary>
        public const float SpriteWidthPx = 550f;
        /// <summary>image_angle += rot_speed (-5); back_angle += rot_speed - 1.</summary>
        public const float RotSpeed = -5f;
        public const float GrowPerStep = 0.01f;
        public const float Alpha = 0.5f;

        protected const float Off = -1f;

        EnemyWorld world;
        float alarm = Off;

        public int Id { get; private set; }
        public EnemyKind Instance { get; internal set; } = EnemyKind.Hydra;
        /// <summary>GML scale: the open size the vortex grows to.</summary>
        public float Scale { get; internal set; } = 0.25f;
        /// <summary>GML image_xscale (= image_yscale).</summary>
        public float ImageScale { get; protected set; }
        public VortexMode Mode { get; private set; } = VortexMode.Idle;
        public float ImageAngle { get; private set; }
        public float BackAngle { get; private set; }
        /// <summary>Steps until Alarm 0, or -1 when unset.</summary>
        public float AlarmSteps => alarm;
        public RoomBounds Room { get; protected set; }
        public Vec2 Position { get; internal set; }
        /// <summary>It closed for good and left the world (a wormhole after its one enemy).</summary>
        public bool IsGone { get; protected set; }

        protected bool FirstPass { get; set; } = true;
        protected EnemyWorld World => world;
        protected IRandomSource Rng => world.Random;

        internal void Attach(EnemyWorld owner, int id)
        {
            world = owner;
            Id = id;
        }

        /// <summary>The shared GML Create: invisible, half alpha, idle, random back_angle.</summary>
        protected void SetupVortex(RoomBounds? room)
        {
            Room = room ?? world.Room;
            ImageScale = 0f;
            Mode = VortexMode.Idle;
            FirstPass = true;
            BackAngle = Rng.IRandom(0, 359);
        }

        protected void SetAlarm(float steps) => alarm = steps;

        protected void SetAngles(float image, float back)
        {
            ImageAngle = GmlMath.Wrap(image);
            BackAngle = GmlMath.Wrap(back);
        }

        /// <summary>One GML step (Alarm 0, then Step), <paramref name="steps"/> GML steps long.</summary>
        internal void Tick(float steps)
        {
            if (IsGone) return;
            if (alarm >= 0f)
            {
                alarm -= steps;
                if (alarm <= 1e-3f)
                {
                    alarm = Off;
                    Advance();
                }
            }

            if (!world.Paused)
            {
                ImageAngle = GmlMath.Wrap(ImageAngle + RotSpeed * steps);
                BackAngle = GmlMath.Wrap(BackAngle + (RotSpeed - 1f) * steps);
            }

            StepEvent(steps);
        }

        /// <summary>GML Alarm 0: the next mode, with first_pass set.</summary>
        void Advance()
        {
            switch (Mode)
            {
                case VortexMode.Idle: Mode = VortexMode.Open; break;
                case VortexMode.Open: Mode = VortexMode.Spawn; break;
                case VortexMode.Spawn: Mode = VortexMode.Close; break;
                default: Mode = VortexMode.Idle; break;
            }
            FirstPass = true;
        }

        protected abstract void StepEvent(float steps);

        protected void Grow(float steps)
        {
            ImageScale = ImageScale < Scale ? Math.Min(Scale, ImageScale + GrowPerStep * steps) : Scale;
        }

        /// <summary>Shrinks toward 0; true once closed.</summary>
        protected bool Shrink(float steps)
        {
            ImageScale -= GrowPerStep * steps;
            if (ImageScale > 1e-4f) return false; // float slack, so 100 steps close a scale of 1
            ImageScale = 0f;
            return true;
        }

        /// <summary>GML instance_create(x, y, instance) plus ef_ring and ef_smoke at the vortex.</summary>
        protected EnemyBody Release()
        {
            var enemy = world.Spawn(Instance, Position, Room, null);
            world.Sink.Emit(Id, new VortexReleased { Enemy = enemy, At = Position });
            return enemy;
        }

        /// <summary>The vortex is finished and out of the world.</summary>
        protected void Close()
        {
            IsGone = true;
            world.RemoveVortex(this);
            world.Sink.Emit(Id, new VortexClosed());
        }

        /// <summary>A whole-pixel point at least <paramref name="bufferPx"/> inside the room: GML x = irandom_range(buffer, room_width - buffer), y likewise (y grows downward).</summary>
        protected Vec2 RandomPointInside(float bufferPx) => SpawnPoints.RandomPointInside(Rng, Room, bufferPx);
    }

    /// <summary>Where the spawners put things (setup_level's rnd_x / rnd_y).</summary>
    public static class SpawnPoints
    {
        /// <summary>
        /// A whole-pixel point at least <paramref name="bufferPx"/> inside <paramref name="room"/>:
        /// GML x = irandom_range(buffer, room_width - buffer), y likewise (y grows downward).
        /// </summary>
        public static Vec2 RandomPointInside(IRandomSource rng, RoomBounds room, float bufferPx)
        {
            float widthPx = room.Width * GameClock.PixelsPerUnit;
            float heightPx = room.Height * GameClock.PixelsPerUnit;
            int x = rng.IRandom((int)Math.Round(bufferPx), (int)Math.Round(widthPx - bufferPx));
            int y = rng.IRandom((int)Math.Round(bufferPx), (int)Math.Round(heightPx - bufferPx));
            return new Vec2(room.XMin + GameClock.Px(x), room.YMax - GameClock.Px(y));
        }
    }

    /// <summary>
    /// par_enemy_emitter (gmlHARE), the looping vortex setup_level places per enemy type:
    /// Create: Alarm 0 = irandom_range(1, room_speed * 2), idle. Idle (first pass): jump to a random point at least
    /// sprite width x scale inside the room, randomise both angles, wait irandom_range(instance_wait_min,
    /// instance_wait_max). Open: 120 steps, growing only while unpaused and instance_number(instance) &lt; instance_max.
    /// Spawn (first pass): one enemy if unpaused and still under instance_max; 120 steps. Close: 120 steps, shrinking
    /// to nothing; then idle again. instance_max caps the live count of that enemy object across the whole room, so a
    /// level keeps topping its population back up to the cap as the ship kills them.
    /// </summary>
    public sealed class EmitterBody : VortexBody
    {
        /// <summary>Create's alarm[0] = irandom_range(1, room_speed * 2).</summary>
        public const int FirstAlarmMaxSteps = 240;
        /// <summary>Open, spawn and close each last alarm[0] = 120 steps.</summary>
        public const int PhaseSteps = 120;
        /// <summary>setup_level's instance_wait_min/max for every emitter it places.</summary>
        public const int DefaultWaitMinSteps = 200;
        public const int DefaultWaitMaxSteps = 500;

        /// <summary>GML instance_min: set by setup_level but never read by the emitter.</summary>
        public int InstanceMin { get; private set; } = 1;
        /// <summary>GML instance_max: no spawn while this many of the object are alive.</summary>
        public int InstanceMax { get; private set; } = 2;
        public int WaitMinSteps { get; private set; } = DefaultWaitMinSteps;
        public int WaitMaxSteps { get; private set; } = DefaultWaitMaxSteps;
        /// <summary>GML buffer = sprite_get_width(spr_vortex) * scale.</summary>
        public float BufferPx => SpriteWidthPx * Scale;
        /// <summary>The enemy released by the latest spawn phase, if any.</summary>
        public EnemyBody LastSpawned { get; private set; }

        bool BelowCap => World.Count(Instance) < InstanceMax;

        /// <summary>GML instance_create(x, y, par_enemy_emitter) followed by setup_level's field writes.</summary>
        internal void Create(EnemyKind kind, Vec2 position, int instanceMin, int instanceMax, float scale, RoomBounds? room,
            int waitMinSteps, int waitMaxSteps, int? firstAlarmSteps)
        {
            Position = position;
            Instance = kind;
            InstanceMin = instanceMin;
            InstanceMax = instanceMax;
            Scale = scale;
            WaitMinSteps = waitMinSteps;
            WaitMaxSteps = waitMaxSteps;
            SetupVortex(room);
            SetAlarm(firstAlarmSteps ?? Rng.IRandom(1, FirstAlarmMaxSteps));
        }

        protected override void StepEvent(float steps)
        {
            switch (Mode)
            {
                case VortexMode.Idle:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Position = RandomPointInside(BufferPx);
                        SetAngles(Rng.IRandom(0, 359), Rng.IRandom(0, 359));
                        SetAlarm(Rng.IRandom(WaitMinSteps, WaitMaxSteps));
                    }
                    break;

                case VortexMode.Open:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        SetAlarm(PhaseSteps);
                    }
                    if (!World.Paused && BelowCap) Grow(steps);
                    break;

                case VortexMode.Spawn:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        LastSpawned = !World.Paused && BelowCap ? Release() : null;
                        SetAlarm(PhaseSteps);
                    }
                    break;

                case VortexMode.Close:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        SetAlarm(PhaseSteps);
                    }
                    Shrink(steps);
                    break;
            }
        }
    }

    /// <summary>
    /// obj_wormhole and create_wormhole (gmlHARE): a one-shot vortex for a single enemy of the generator's list.
    /// create_wormhole sets its scale per kind and drops it at a random point at least sprite width x scale inside
    /// the room. Create waits one_tick (room_speed * 2 = 240 steps) idle; open grows for 240 steps; spawn releases the
    /// enemy unconditionally (no cap, no pause check) and waits 240; close shrinks to nothing and destroys the wormhole.
    /// </summary>
    public sealed class WormholeBody : VortexBody
    {
        /// <summary>one_tick = room_speed * 2.</summary>
        public const int OneTickSteps = 240;

        /// <summary>The enemy this wormhole released, once it has.</summary>
        public EnemyBody Spawned { get; private set; }

        /// <summary>
        /// create_wormhole(kind). The original sized its placement margin with the caller's (undefined) scale; this
        /// uses the wormhole's own, which is what it meant.
        /// </summary>
        internal void Create(EnemyKind kind, RoomBounds? room, Vec2? position)
        {
            Instance = kind;
            Scale = EnemyTable.Get(kind).WormholeScale;
            SetupVortex(room);
            Position = position ?? RandomPointInside(SpriteWidthPx * Scale);
            SetAlarm(OneTickSteps);
        }

        protected override void StepEvent(float steps)
        {
            switch (Mode)
            {
                case VortexMode.Open:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        SetAlarm(OneTickSteps);
                    }
                    Grow(steps);
                    break;

                case VortexMode.Spawn:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Spawned = Release();
                        SetAlarm(OneTickSteps);
                    }
                    break;

                case VortexMode.Close:
                    FirstPass = false;
                    if (Shrink(steps)) Close();
                    break;
            }
        }
    }

    /// <summary>
    /// obj_wormhole_emitter (gmlHARE), the generator mode's pacer (gm_generator): setup_level runs
    /// generate_enemy_list(difficulty) and creates this emitter, which
    /// Create: opens a wormhole for the first 10% of the list at once (repeat(enemy_count * 0.1), the fraction
    /// dropped), then sets Alarm 0 to 1 + room_speed * irandom_range(0, 4) steps; Alarm 0: opens a wormhole for the
    /// next enemy, or, once the list is spent, destroys itself and runs set_level_complete; then re-arms
    /// 1 + room_speed * irandom_range(0, 8). So after the opening burst one enemy arrives every 1 step to 8 s (4 s on
    /// average), each 4 s after its wormhole appears, and the level ends one alarm after the last wormhole opens.
    /// </summary>
    /// <remarks>
    /// setup_level calls instance_create(obj_wormhole_emitter, 0, 0) with its arguments swapped, and the globals
    /// enemy_count/current_enemy are never reset; the port creates the emitter properly and keeps its list and cursor
    /// per emitter.
    /// </remarks>
    public sealed class WormholeEmitterBody
    {
        public const float OpeningFraction = 0.1f;
        /// <summary>Create's rate = 1 + room_speed * irandom_range(0, 4).</summary>
        public const int FirstRateMaxSeconds = 4;
        /// <summary>Alarm 0's rate = 1 + room_speed * irandom_range(0, 8).</summary>
        public const int RateMaxSeconds = 8;

        readonly List<EnemyKind> list = new List<EnemyKind>();
        readonly List<WormholeBody> wormholes = new List<WormholeBody>();
        EnemyWorld world;
        float alarm = -1f;
        RoomBounds room;

        public int Id { get; private set; }
        /// <summary>GML enemy_list.</summary>
        public IReadOnlyList<EnemyKind> List => list;
        /// <summary>GML current_enemy: how many wormholes have opened.</summary>
        public int CurrentEnemy { get; private set; }
        /// <summary>Every wormhole this emitter opened, in order.</summary>
        public IReadOnlyList<WormholeBody> Wormholes => wormholes;
        public float AlarmSteps => alarm;
        public bool Finished { get; private set; }

        internal void Attach(EnemyWorld owner, int id)
        {
            world = owner;
            Id = id;
        }

        /// <summary>GML Create over <paramref name="enemies"/>.</summary>
        internal void Create(IEnumerable<EnemyKind> enemies, RoomBounds? roomBounds, int? firstRateSteps)
        {
            list.Clear();
            list.AddRange(enemies);
            wormholes.Clear();
            CurrentEnemy = 0;
            Finished = false;
            room = roomBounds ?? world.Room;

            int opening = (int)Math.Floor(list.Count * OpeningFraction);
            for (int i = 0; i < opening; i++) OpenNext();

            alarm = firstRateSteps ?? Rate(FirstRateMaxSeconds);
        }

        int Rate(int maxSeconds) => 1 + (int)GameClock.StepsPerSecond * world.Random.IRandom(0, maxSeconds);

        void OpenNext()
        {
            var wormhole = world.AddVortex(new WormholeBody());
            wormhole.Create(list[CurrentEnemy], room, null);
            CurrentEnemy++;
            wormholes.Add(wormhole);
            world.Sink.Emit(Id, new WormholeOpened { Wormhole = wormhole });
        }

        /// <summary>One GML step (Alarm 0), <paramref name="steps"/> GML steps long.</summary>
        internal void Tick(float steps)
        {
            if (Finished || alarm < 0f) return;
            alarm -= steps;
            if (alarm > 1e-3f) return;
            alarm = -1f;

            if (CurrentEnemy < list.Count)
            {
                OpenNext();
                alarm = Rate(RateMaxSeconds);
            }
            else
            {
                Finished = true;
                // set_level_complete: level_complete = true, obj_level_results, paused = true.
                world.Paused = true;
                world.RemoveWormholeEmitter(this);
                world.Sink.Emit(Id, new LevelCompleted());
            }
        }
    }
}
