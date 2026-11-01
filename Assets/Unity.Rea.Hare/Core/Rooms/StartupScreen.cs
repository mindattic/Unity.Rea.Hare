using System;
using System.Collections.Generic;

namespace Unity.Rea.Hare.Rooms
{
    /// <summary>One startup background as it stands: where GameMaker has scrolled it to and how fast it is moving.</summary>
    public sealed class StartupLayer
    {
        internal StartupLayer(int index, StartupBackground background)
        {
            Index = index;
            Name = background.Name;
            Drifts = background.Drifts;
        }

        /// <summary>The background's slot in the room (GML: the i in background_x[i]); 0 is drawn at the back.</summary>
        public int Index { get; }
        public string Name { get; }
        /// <summary>Whether an alarm re-rolls this background's speeds.</summary>
        public bool Drifts { get; }
        /// <summary>GML: background_x[i], pixels; the tiled image repeats every background width from here.</summary>
        public int X { get; internal set; }
        /// <summary>GML: background_y[i], pixels, growing downward.</summary>
        public int Y { get; internal set; }
        /// <summary>GML: background_hspeed[i], pixels a step.</summary>
        public int HSpeed { get; internal set; }
        /// <summary>GML: background_vspeed[i], pixels a step.</summary>
        public int VSpeed { get; internal set; }
        /// <summary>GML: alarm[i], steps left until the re-roll; -1 for a background with no alarm.</summary>
        public int Alarm { get; internal set; } = -1;

        /// <summary>Where a tile starts along an axis <paramref name="size"/> pixels long: <paramref name="position"/> mod size, in [0, size).</summary>
        public static int Wrap(int position, int size) => size <= 0 ? 0 : ((position % size) + size) % size;
    }

    /// <summary>
    /// obj_startup, engine-free: the startup room's drifting circuit-board backgrounds, its note and its way out. There is
    /// no global instance: the room's view owns one. State changes only through <see cref="Execute"/>, which runs a
    /// <see cref="StartupCommand"/> and returns the <see cref="StartupEvent"/>s it raised; presentation draws the
    /// <see cref="Layers"/> and plays the events (fullscreen, the room change) and never writes back.
    ///
    /// The original, read from obj_startup.object.gmx and startup.room.gmx: the room starts five tiled backgrounds at
    /// (0, 0), the first three moving. Create sets the window fullscreen and arms alarm[0], [1] and [2] for
    /// <c>irandom_range(room_speed * shift_min, room_speed * shift_max)</c> steps (1 to 12 seconds); each alarm sets its
    /// background's hspeed and vspeed to <c>irandom_range(-2, 2)</c> and arms itself again the same way. Every step
    /// GameMaker counts the alarms down (firing those that reach 0, in alarm order) and then moves each background by
    /// its speeds. Any key press, and the left mouse button while it is held, run <c>room_goto(room_title)</c>.
    /// </summary>
    public sealed class StartupScreen
    {
        readonly List<StartupEvent> events = new List<StartupEvent>();
        readonly StartupLayer[] layers;
        readonly IRandomSource random;
        PlayerId issuer;

        /// <param name="tuning">The startup data (the registry's <c>startup</c> section).</param>
        /// <param name="stepsPerSecond">The room's room_speed, which turns shift_min and shift_max into steps.</param>
        /// <param name="random">Where every irandom_range comes from.</param>
        public StartupScreen(StartupTuning tuning, int stepsPerSecond, IRandomSource random)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            if (stepsPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(stepsPerSecond), stepsPerSecond, "Must be positive.");
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            StepsPerSecond = stepsPerSecond;
            layers = new StartupLayer[tuning.Backgrounds.Length];
            for (int i = 0; i < layers.Length; i++) layers[i] = new StartupLayer(i, tuning.Backgrounds[i]);
            Reset();
        }

        public StartupTuning Tuning { get; }

        /// <summary>The room's room_speed.</summary>
        public int StepsPerSecond { get; }

        /// <summary>The backgrounds, back to front.</summary>
        public IReadOnlyList<StartupLayer> Layers => layers;

        /// <summary>Whether the Create event has run.</summary>
        public bool Created { get; private set; }

        /// <summary>Whether a key or the mouse has already sent the game on (the room is on its way out).</summary>
        public bool Continued { get; private set; }

        /// <summary>The shortest wait between re-rolls, in steps: room_speed * shift_min.</summary>
        public int ShiftMinSteps => StepsPerSecond * Tuning.ShiftMinSeconds;

        /// <summary>The longest wait between re-rolls, in steps: room_speed * shift_max.</summary>
        public int ShiftMaxSteps => StepsPerSecond * Tuning.ShiftMaxSeconds;

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public StartupResult Execute(StartupCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<StartupEvent>() : events.ToArray();
            events.Clear();
            return new StartupResult(accepted, raised);
        }

        void Emit(StartupEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        /// <summary>The room's backgrounds as startup.room.gmx places them.</summary>
        void Reset()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var background = Tuning.Backgrounds[i];
                var layer = layers[i];
                layer.X = 0;
                layer.Y = 0;
                layer.HSpeed = background.HSpeed;
                layer.VSpeed = background.VSpeed;
                layer.Alarm = -1;
            }
        }

        int RollShift() => random.IRandom(ShiftMinSteps, ShiftMaxSteps);

        // ---- Rules the commands call ----

        /// <summary>The Create event: fullscreen, then alarm[i] = irandom_range(...) for each drifting background in order.</summary>
        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            Reset();
            if (Tuning.FullscreenOnEntry) Emit(new FullscreenRequested());
            foreach (var layer in layers)
                if (layer.Drifts) layer.Alarm = RollShift();
            return true;
        }

        /// <summary>One step: the alarms count down and fire in order, then every background moves by its speeds.</summary>
        internal bool Step()
        {
            if (!Created) return false;
            foreach (var layer in layers)
            {
                if (layer.Alarm <= 0) continue;
                layer.Alarm--;
                if (layer.Alarm == 0) Drift(layer);
            }
            foreach (var layer in layers)
            {
                layer.X += layer.HSpeed;
                layer.Y += layer.VSpeed;
            }
            return true;
        }

        /// <summary>Alarm i: <c>background_hspeed[i] = irandom_range(-2,2); background_vspeed[i] = irandom_range(-2,2);</c> and re-arm.</summary>
        void Drift(StartupLayer layer)
        {
            layer.HSpeed = random.IRandom(Tuning.DriftSpeedMin, Tuning.DriftSpeedMax);
            layer.VSpeed = random.IRandom(Tuning.DriftSpeedMin, Tuning.DriftSpeedMax);
            layer.Alarm = RollShift();
            Emit(new DriftChanged { Layer = layer.Index, HSpeed = layer.HSpeed, VSpeed = layer.VSpeed, NextShiftSteps = layer.Alarm });
        }

        /// <summary>
        /// The any-key and global-left-button events: <c>room_goto(room_title)</c>. Once is enough; a second press (or the
        /// button still held on the next frame) before the room has gone asks for nothing more.
        /// </summary>
        internal bool Continue()
        {
            if (Continued) return false;
            Continued = true;
            Emit(new ContinueRequested { Room = Tuning.ContinueRoom });
            return true;
        }
    }

    /// <summary>What <see cref="StartupScreen.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct StartupResult
    {
        public StartupResult(bool accepted, IReadOnlyList<StartupEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<StartupEvent> Events { get; }
    }
}
