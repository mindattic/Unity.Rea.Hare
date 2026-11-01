using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Weather
{
    /// <summary>One cloud, raindrop, snowflake, grain of sand or bank of fog, in room pixels (from the top left, y down).</summary>
    public sealed class WeatherParticle
    {
        internal WeatherParticle(int id, WeatherKind kind) { Id = id; Kind = kind; }

        public int Id { get; }
        public WeatherKind Kind { get; }
        /// <summary>The GML sprite it draws.</summary>
        public string Sprite { get; internal set; } = "";
        public float X { get; internal set; }
        public float Y { get; internal set; }
        /// <summary>Its motion a step: obj_cloud's hspeed (Hspeed), the others' dX / dY.</summary>
        public float DX { get; internal set; }
        public float DY { get; internal set; }
        public float Alpha { get; internal set; } = 1f;
        /// <summary>image_angle, degrees (snow and sand turn -1 a step).</summary>
        public float Angle { get; internal set; }
        /// <summary>obj_fog's <c>dir == "left"</c>.</summary>
        public bool DriftsLeft { get; internal set; }
        /// <summary>GML depth: obj_cloud 0, every other weather object -1.</summary>
        public int Depth => Kind == WeatherKind.Cloud ? 0 : -1;
    }

    /// <summary>The only way the weather changes. Runs in <see cref="WeatherField.Execute"/>.</summary>
    public abstract class WeatherCommand
    {
        internal abstract bool Apply(WeatherField field);
    }

    /// <summary>
    /// instance_create(0, 0, obj_*_emitter) for each kind the level names, in order (the play rooms place obj_cloud_emitter
    /// themselves): every emitter already running stops and its weather goes first, as when a room is entered again.
    /// </summary>
    public sealed class StartWeather : WeatherCommand
    {
        readonly WeatherKind[] kinds;
        public StartWeather(params WeatherKind[] kinds) => this.kinds = kinds ?? Array.Empty<WeatherKind>();
        internal override bool Apply(WeatherField field) { field.Start(kinds); return true; }
    }

    /// <summary>Every emitter and every particle goes (setup_level's Clear Objects, or the room ending).</summary>
    public sealed class ClearWeather : WeatherCommand
    {
        internal override bool Apply(WeatherField field) => field.Clear();
    }

    /// <summary>GML paused: the particles stop where they are; the emitters, which never read it, keep making them.</summary>
    public sealed class SetWeatherPaused : WeatherCommand
    {
        readonly bool paused;
        public SetWeatherPaused(bool paused) => this.paused = paused;
        internal override bool Apply(WeatherField field)
        {
            if (field.Paused == paused) return false;
            field.Paused = paused;
            return true;
        }
    }

    /// <summary>One room step: the emitters' alarms and Steps, then every particle's Step and motion.</summary>
    public sealed class StepWeather : WeatherCommand
    {
        internal override bool Apply(WeatherField field) { field.Step(); return true; }
    }

    /// <summary>Something the weather did; presentation draws <see cref="WeatherField.Particles"/> and may play these.</summary>
    public abstract class WeatherEvent { }

    public sealed class WeatherParticleSpawned : WeatherEvent { public WeatherParticle Particle; }

    public sealed class WeatherParticleRemoved : WeatherEvent { public WeatherParticle Particle; }

    public readonly struct WeatherResult
    {
        public WeatherResult(bool accepted, IReadOnlyList<WeatherEvent> events)
        {
            Accepted = accepted;
            Events = events ?? Array.Empty<WeatherEvent>();
        }

        public bool Accepted { get; }
        public IReadOnlyList<WeatherEvent> Events { get; }
    }

    /// <summary>
    /// A play room's weather, engine-free (U.HARE-T18), porting gmlHARE's five weather emitters and what they make:
    /// <list type="bullet">
    /// <item>obj_cloud_emitter: Create makes 12 obj_cloud anywhere in the room and sets alarm[0] = 1; Alarm 0 makes
    /// irandom_range(1, 4) more at the right edge and re-arms every 200 steps. obj_cloud: x = room_width,
    /// hspeed = -irandom_range(1, 2), image_alpha = 0.1 * irandom_range(1, 3), y = irandom_range(-sprite_height/2,
    /// room_height + sprite_height/2), spr_cloud1..4; gone once x &lt; -sprite_width; while paused, speed = 0.</item>
    /// <item>obj_rain_emitter: one obj_raindrop a step, at irandom_range(0, room_width) and irandom_range(-sprite_height,
    /// -room_height), falling irandom_range(15, 20) and drifting irandom_range(-1, -2) a step, gone below the room.</item>
    /// <item>obj_snow_emitter: one obj_snow a step, the same start, spr_snow1/2 at alpha 0.8, falling irandom_range(0.5, 2)
    /// (0, 1 or 2; a 0 falls at 0.5, BIBLE D16) and drifting irandom_range(-2, 2), turning -1 a step.</item>
    /// <item>obj_sand_emitter: two obj_sand a step from irandom_range(-room_width/2, 0) and irandom_range(-room_height/2,
    /// room_height), spr_sand1/2 at 0.8, dY irandom_range(-1, 6) (0 becomes 2), dX irandom_range(3, 6), turning -1 a step;
    /// gone below the room or, a grain that rises, past its right edge (BIBLE D17).</item>
    /// <item>obj_fog_emitter: 32 obj_fog, spr_cloud1..4 at alpha 0.3 in the bottom quarter, drifting 1-2 px a step left
    /// or right and coming round again from the far side; drawn (its Draw's draw_self is commented out, BIBLE D18).</item>
    /// </list>
    /// Every random value is drawn in the original's order from the field's own <see cref="IRandomSource"/>.
    /// </summary>
    public sealed class WeatherField
    {
        sealed class Emitter
        {
            public WeatherKind Kind;
            public int Alarm = -1;
        }

        readonly IRandomSource rng;
        readonly List<Emitter> emitters = new List<Emitter>();
        readonly List<WeatherParticle> particles = new List<WeatherParticle>();
        readonly List<WeatherEvent> events = new List<WeatherEvent>();
        int nextId;

        /// <param name="rng">GML's engine random.</param>
        /// <param name="roomWidth">room_width, px.</param>
        /// <param name="roomHeight">room_height, px.</param>
        /// <param name="tuning">The numbers; null reads <see cref="GameContent.Current"/>.</param>
        public WeatherField(IRandomSource rng, int roomWidth, int roomHeight, WeatherTuning tuning = null)
        {
            this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (roomWidth <= 0 || roomHeight <= 0) throw new ArgumentOutOfRangeException(nameof(roomWidth), "The room needs a size.");
            RoomWidth = roomWidth;
            RoomHeight = roomHeight;
            Tuning = tuning ?? GameContent.Current.Weather;
        }

        public WeatherTuning Tuning { get; }
        public int RoomWidth { get; }
        public int RoomHeight { get; }
        public IReadOnlyList<WeatherParticle> Particles => particles;
        /// <summary>GML paused.</summary>
        public bool Paused { get; internal set; }

        /// <summary>The emitters running, in the order they were made.</summary>
        public IReadOnlyList<WeatherKind> Emitters
        {
            get
            {
                var kinds = new WeatherKind[emitters.Count];
                for (int i = 0; i < kinds.Length; i++) kinds[i] = emitters[i].Kind;
                return kinds;
            }
        }

        /// <summary>How many particles of <paramref name="kind"/> there are.</summary>
        public int Count(WeatherKind kind)
        {
            int n = 0;
            foreach (var p in particles) if (p.Kind == kind) n++;
            return n;
        }

        public WeatherResult Execute(WeatherCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<WeatherEvent>() : events.ToArray();
            events.Clear();
            return new WeatherResult(accepted, raised);
        }

        // ---- Emitters ----

        internal void Start(WeatherKind[] kinds)
        {
            Clear();
            foreach (var kind in kinds)
            {
                var emitter = new Emitter { Kind = kind };
                emitters.Add(emitter);
                switch (kind)
                {
                    case WeatherKind.Cloud:
                        // obj_cloud_emitter Create: the initial clouds, then alarm[0] = 1.
                        for (int i = 0; i < Tuning.CloudInitialCount; i++)
                        {
                            var cloud = Spawn(WeatherKind.Cloud);
                            cloud.X = rng.IRandom(0, RoomWidth);
                            cloud.Y = rng.IRandom(0, RoomHeight);
                        }
                        emitter.Alarm = Tuning.CloudFirstAlarmSteps;
                        break;
                    case WeatherKind.Fog:
                        for (int i = 0; i < Tuning.FogCount; i++) Spawn(WeatherKind.Fog);
                        break;
                }
            }
        }

        internal bool Clear()
        {
            if (emitters.Count == 0 && particles.Count == 0) return false;
            emitters.Clear();
            foreach (var p in particles) events.Add(new WeatherParticleRemoved { Particle = p });
            particles.Clear();
            return true;
        }

        internal void Step()
        {
            // The emitters: alarms first, then their Step events (neither reads paused).
            foreach (var emitter in emitters)
            {
                if (emitter.Alarm > 0 && --emitter.Alarm == 0 && emitter.Kind == WeatherKind.Cloud)
                {
                    int batch = rng.IRandom(Tuning.CloudBatchMin, Tuning.CloudBatchMax);
                    for (int i = 0; i < batch; i++) Spawn(WeatherKind.Cloud);
                    emitter.Alarm = Tuning.CloudRateSteps;
                }
            }
            foreach (var emitter in emitters)
            {
                int perStep = emitter.Kind == WeatherKind.Rain ? Tuning.RainPerStep
                    : emitter.Kind == WeatherKind.Snow ? Tuning.SnowPerStep
                    : emitter.Kind == WeatherKind.Sand ? Tuning.SandPerStep
                    : 0;
                for (int i = 0; i < perStep; i++) Spawn(emitter.Kind);
            }

            // The particles' Step events (and obj_cloud's built-in hspeed after its Step).
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                if (!StepParticle(p))
                {
                    particles.RemoveAt(i--);
                    events.Add(new WeatherParticleRemoved { Particle = p });
                }
            }
        }

        // ---- The particles ----

        WeatherParticle Spawn(WeatherKind kind)
        {
            var p = new WeatherParticle(++nextId, kind);
            switch (kind)
            {
                case WeatherKind.Cloud: CreateCloud(p); break;
                case WeatherKind.Rain: CreateRaindrop(p); break;
                case WeatherKind.Snow: CreateSnow(p); break;
                case WeatherKind.Sand: CreateSand(p); break;
                case WeatherKind.Fog: CreateFog(p); break;
            }
            particles.Add(p);
            events.Add(new WeatherParticleSpawned { Particle = p });
            return p;
        }

        void CreateCloud(WeatherParticle p)
        {
            var t = Tuning;
            p.X = RoomWidth;
            p.DX = -rng.IRandom(t.CloudSpeedMin, t.CloudSpeedMax);
            p.Alpha = t.CloudAlphaStep * rng.IRandom(t.CloudAlphaMin, t.CloudAlphaMax);
            // sprite_height is still spr_cloud1's here; GameMaker truncates irandom_range's bounds.
            p.Y = rng.IRandom(Trunc(-t.CloudHeightPx / 2f), Trunc(RoomHeight + t.CloudHeightPx / 2f));
            p.Sprite = t.CloudSprites[rng.IRandom(1, t.CloudSprites.Length) - 1];
        }

        void CreateRaindrop(WeatherParticle p)
        {
            var t = Tuning;
            p.Sprite = t.RainSprite;
            p.X = rng.IRandom(0, RoomWidth);
            p.Y = rng.IRandom(-t.RainHeightPx, -RoomHeight);
            p.Alpha = t.RainAlpha;
            p.DY = rng.IRandom(t.RainFallMin, t.RainFallMax);
            p.DX = rng.IRandom(t.RainDriftMax, t.RainDriftMin);
        }

        void CreateSnow(WeatherParticle p)
        {
            var t = Tuning;
            p.X = rng.IRandom(0, RoomWidth);
            p.Y = rng.IRandom(-t.SnowHeightPx, -RoomHeight);
            p.Alpha = t.SnowAlpha;
            p.Sprite = t.SnowSprites[rng.IRandom(0, t.SnowSprites.Length - 1)];
            int fall = rng.IRandom(t.SnowFallMin, t.SnowFallMax);
            p.DY = fall == 0 ? t.SnowZeroFall : fall; // BIBLE D16: a 0 never fell and piled up above the room for good
            p.DX = rng.IRandom(-t.SnowDriftMax, t.SnowDriftMax);
        }

        void CreateSand(WeatherParticle p)
        {
            var t = Tuning;
            p.X = rng.IRandom(Trunc(-RoomWidth / 2f), 0);
            p.Y = rng.IRandom(Trunc(-RoomHeight / 2f), RoomHeight);
            p.Alpha = t.SandAlpha;
            p.Sprite = t.SandSprites[rng.IRandom(0, t.SandSprites.Length - 1)];
            int fall = rng.IRandom(t.SandFallMin, t.SandFallMax);
            p.DY = fall == 0 ? t.SandZeroFall : fall;
            p.DX = rng.IRandom(t.SandDriftMin, t.SandDriftMax);
        }

        void CreateFog(WeatherParticle p)
        {
            var t = Tuning;
            p.Sprite = t.CloudSprites[rng.IRandom(1, t.CloudSprites.Length) - 1];
            p.DriftsLeft = rng.IRandom(1, 2) == 1;
            if (p.DriftsLeft)
            {
                p.X = rng.IRandom(-RoomWidth, 0);
                p.DX = -rng.IRandom(t.FogSpeedMin, t.FogSpeedMax);
            }
            else
            {
                p.X = rng.IRandom(RoomWidth, RoomWidth * 2);
                p.DX = rng.IRandom(t.FogSpeedMin, t.FogSpeedMax);
            }
            // The script then places it again, anywhere from half a room left to half a room right.
            p.X = rng.IRandom(Trunc(-RoomWidth / 2f), Trunc(RoomWidth + RoomWidth / 2f));
            p.Y = rng.IRandom(Trunc(RoomHeight - RoomHeight / 4f), Trunc(RoomHeight - t.CloudHeightPx / 2f));
            p.Alpha = t.FogAlpha;
        }

        /// <summary>The particle's Step; false when it destroyed itself.</summary>
        bool StepParticle(WeatherParticle p)
        {
            var t = Tuning;
            switch (p.Kind)
            {
                case WeatherKind.Cloud:
                    // if (!paused) { hspeed = Hspeed; if (x < -sprite_width) instance_destroy(); } else speed = 0;
                    if (Paused) return true;
                    if (p.X < -t.CloudWidthPx) return false;
                    p.X += p.DX; // the built-in hspeed motion, after the Step event
                    return true;

                case WeatherKind.Rain:
                    if (Paused) return true;
                    p.X += p.DX;
                    p.Y += p.DY;
                    return p.Y <= RoomHeight;

                case WeatherKind.Snow:
                    if (Paused) return true;
                    p.X += p.DX;
                    p.Y += p.DY;
                    p.Angle += t.SpinPerStep;
                    return p.Y <= RoomHeight;

                case WeatherKind.Sand:
                    if (Paused) return true;
                    p.X += p.DX;
                    p.Y += p.DY;
                    p.Angle += t.SpinPerStep;
                    // BIBLE D17: a grain rolled dY = -1 rose for ever and was never destroyed; it goes past the right edge.
                    return p.Y <= RoomHeight && p.X <= RoomWidth + t.SandWidthPx;

                case WeatherKind.Fog:
                    if (Paused) return true;
                    p.X += p.DX;
                    if (p.DriftsLeft)
                    {
                        if (p.X < -t.CloudWidthPx)
                        {
                            p.X = RoomWidth + rng.IRandom(1, Trunc(RoomWidth / 2f));
                            p.DX = -rng.IRandom(t.FogSpeedMin, t.FogSpeedMax);
                        }
                    }
                    else if (p.X > RoomWidth + t.CloudWidthPx)
                    {
                        p.X = -rng.IRandom(1, Trunc(RoomWidth / 2f));
                        p.DX = rng.IRandom(t.FogSpeedMin, t.FogSpeedMax);
                    }
                    return true;
            }
            return true;
        }

        /// <summary>GameMaker's irandom_range truncates a fractional bound toward zero.</summary>
        static int Trunc(float value) => (int)value;
    }
}
