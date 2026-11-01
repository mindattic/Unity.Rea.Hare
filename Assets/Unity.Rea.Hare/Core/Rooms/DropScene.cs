using System;
using System.Collections.Generic;

namespace Unity.Rea.Hare.Rooms
{
    /// <summary>obj_drop_title's and obj_drop_intro's <c>phase</c>.</summary>
    public enum DropPhase
    {
        /// <summary>Falling in at <c>vspeed = 10</c> until <c>y &gt; room_height/4</c>.</summary>
        Start,
        /// <summary>Stopped, shaking in place (the title's Alarm 1 counts down; the intro waits for a key).</summary>
        Hang,
        /// <summary>Falling out at <c>vspeed = 10</c> until <c>y &gt; room_height</c>.</summary>
        Exit,
    }

    /// <summary>Which of the drop's two smokes: the c_orange trail every step or Alarm 0's c_black puff.</summary>
    public enum DropSmoke
    {
        Trail,
        Puff,
    }

    /// <summary>The falling rabbit (obj_drop_title / obj_drop_intro) as it stands, in room pixels.</summary>
    public sealed class FallingDrop
    {
        /// <summary>GML x, y: spr_drop's origin, the middle of its top edge.</summary>
        public double X { get; internal set; }
        public double Y { get; internal set; }
        /// <summary>Create's <c>startX = x</c>, the line it shakes about (the intro's A and D move it).</summary>
        public double StartX { get; internal set; }
        public double VSpeed { get; internal set; }
        public DropPhase Phase { get; internal set; }
        /// <summary>GML alarm[0], the black puff's countdown.</summary>
        public int SmokeAlarm { get; internal set; } = -1;
        /// <summary>GML alarm[1], obj_drop_title's hang; -1 when not set.</summary>
        public int HangAlarm { get; internal set; } = -1;
    }

    /// <summary>One obj_drop_cloud, in room pixels (origin top left).</summary>
    public sealed class DropCloud
    {
        internal DropCloud(int id) => Id = id;

        public int Id { get; }
        public string Sprite { get; internal set; } = "";
        public double X { get; internal set; }
        public double Y { get; internal set; }
        public double VSpeed { get; internal set; }
        public double Alpha { get; internal set; }
        /// <summary>Its sprite's height, which its Step's <c>y &lt; -sprite_height</c> reads.</summary>
        public int Height { get; internal set; }
    }

    /// <summary>One of the room's backgrounds: where GameMaker has scrolled it to and how fast it moves.</summary>
    public sealed class DropBackgroundLayer
    {
        internal DropBackgroundLayer(int index, DropBackground background)
        {
            Index = index;
            Name = background.Name;
            HSpeed = background.HSpeed;
            VSpeed = background.VSpeed;
        }

        /// <summary>The slot (background_x[i]'s i); 0 is drawn at the back.</summary>
        public int Index { get; }
        public string Name { get; }
        /// <summary>background_x[i] and background_y[i], pixels; tiled both ways from here.</summary>
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public int HSpeed { get; internal set; }
        public int VSpeed { get; internal set; }
    }

    /// <summary>
    /// What room_title and room_intro share, engine-free: the falling rabbit (obj_drop_title or obj_drop_intro), the cloud
    /// emitter (obj_drop_emitter) and its clouds (obj_drop_cloud), and the backgrounds. Its room's screen
    /// (<see cref="TitleScreen"/>, <see cref="IntroScreen"/>) owns it and runs it inside its own commands; it raises
    /// nothing itself, but reports the drop's smoke and its leaving through what <see cref="Step"/> returns.
    ///
    /// One step runs GameMaker's order: the alarms (the emitter's Alarm 0, then the drop's Alarm 0 and 1), the Step
    /// events (the drop's, then each cloud's), the built-in motion (y += vspeed), then the backgrounds' scroll. Every
    /// random value is drawn in the original's order from the scene's <see cref="IRandomSource"/>.
    /// </summary>
    public sealed class DropScene
    {
        readonly IRandomSource random;
        readonly List<DropCloud> clouds = new List<DropCloud>();
        readonly DropBackgroundLayer[] backgrounds;
        int emitterAlarm = -1;
        int nextCloud;

        public DropScene(DropSceneTuning tuning, int roomWidth, int roomHeight, IRandomSource random)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            if (roomWidth <= 0) throw new ArgumentOutOfRangeException(nameof(roomWidth), roomWidth, "Must be positive.");
            if (roomHeight <= 0) throw new ArgumentOutOfRangeException(nameof(roomHeight), roomHeight, "Must be positive.");
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            RoomWidth = roomWidth;
            RoomHeight = roomHeight;
            var list = tuning.Backgrounds ?? Array.Empty<DropBackground>();
            backgrounds = new DropBackgroundLayer[list.Length];
            for (int i = 0; i < list.Length; i++) backgrounds[i] = new DropBackgroundLayer(i, list[i]);
        }

        public DropSceneTuning Tuning { get; }
        public int RoomWidth { get; }
        public int RoomHeight { get; }

        /// <summary>The rabbit; null once it is destroyed (a key on the title, or the title's drop below the room).</summary>
        public FallingDrop Drop { get; private set; }

        /// <summary>Whether obj_drop_emitter still exists.</summary>
        public bool EmitterAlive { get; private set; }

        /// <summary>The clouds, oldest first.</summary>
        public IReadOnlyList<DropCloud> Clouds => clouds;

        /// <summary>The backgrounds, back to front.</summary>
        public IReadOnlyList<DropBackgroundLayer> Backgrounds => backgrounds;

        /// <summary>The y past which phase "start" stops: <c>room_height / 4</c>.</summary>
        public double HangLine => RoomHeight * Tuning.HangFraction;

        /// <summary>The room's instances come up: the drop at its place (Create: <c>vspeed = 10; startX = x; alarm[0] = smoke_delay</c>) and the emitter (<c>alarm[0] = 1</c>).</summary>
        internal void Create()
        {
            Drop = new FallingDrop
            {
                X = Tuning.DropX,
                Y = Tuning.DropY,
                StartX = Tuning.DropX,
                VSpeed = Tuning.FallSpeed,
                Phase = DropPhase.Start,
                SmokeAlarm = Tuning.SmokeDelaySteps,
            };
            EmitterAlive = true;
            emitterAlarm = Tuning.CloudFirstAlarmSteps;
            clouds.Clear();
            foreach (var layer in backgrounds)
            {
                layer.X = 0;
                layer.Y = 0;
            }
        }

        /// <summary><c>with(obj_drop_title) { instance_destroy(); }</c>. False when there was none.</summary>
        internal bool DestroyDrop()
        {
            if (Drop == null) return false;
            Drop = null;
            return true;
        }

        /// <summary><c>with(obj_drop_emitter) { instance_destroy(); }</c>: no more clouds; those made fly on. False when there was none.</summary>
        internal bool DestroyEmitter()
        {
            if (!EmitterAlive) return false;
            EmitterAlive = false;
            emitterAlarm = -1;
            return true;
        }

        /// <summary><c>background_vspeed[index] = 0</c> (obj_logo's Create stops the night sky).</summary>
        internal void StopBackground(int index)
        {
            if (index < 0 || index >= backgrounds.Length) return;
            backgrounds[index].VSpeed = 0;
        }

        /// <summary>
        /// obj_drop_intro's held A and D (keyboard events, before the Step): <c>if(startX &gt; 300) startX -= 1</c> and
        /// <c>if(startX &lt; room_width - 300) startX += 1</c>, A's first.
        /// </summary>
        internal void Steer(bool left, bool right, int margin, int step)
        {
            if (Drop == null) return;
            if (left && Drop.StartX > margin) Drop.StartX -= step;
            if (right && Drop.StartX < RoomWidth - margin) Drop.StartX += step;
        }

        /// <summary>The intro's key press: <c>if(phase == "hang") phase = "exit"</c>. False unless it was hanging.</summary>
        internal bool Release()
        {
            if (Drop == null || Drop.Phase != DropPhase.Hang) return false;
            Drop.Phase = DropPhase.Exit;
            return true;
        }

        /// <summary>
        /// One step. <paramref name="smoke"/> is told each effect the drop makes (which, x, y); the result says whether the
        /// drop stopped this step (phase "start" to "hang") and whether it is below the room in phase "exit" (the title's
        /// Step then destroys it; the intro's goes to the game every step it is there).
        /// </summary>
        internal DropStep Step(Action<DropSmoke, double, double> smoke)
        {
            bool stopped = false, below = false;

            // Alarms: obj_drop_emitter's, then the drop's 0 (the black puff) and 1 (the title's hang).
            if (EmitterAlive && emitterAlarm > 0 && --emitterAlarm == 0)
            {
                int batch = random.IRandom(Tuning.CloudBatchMin, Tuning.CloudBatchMax);
                for (int i = 0; i < batch; i++) clouds.Add(MakeCloud());
                emitterAlarm = Tuning.CloudRateSteps;
            }
            var drop = Drop;
            if (drop != null)
            {
                if (drop.SmokeAlarm > 0 && --drop.SmokeAlarm == 0)
                {
                    smoke?.Invoke(DropSmoke.Puff, drop.X, drop.Y + Tuning.SmokeOffsetY);
                    drop.SmokeAlarm = Tuning.SmokeDelaySteps;
                }
                if (drop.HangAlarm > 0 && --drop.HangAlarm == 0)
                {
                    drop.HangAlarm = -1;
                    drop.Phase = DropPhase.Exit;
                }

                // Its Step.
                drop.X = drop.StartX + random.IRandom(-Tuning.Jitter, Tuning.Jitter);
                smoke?.Invoke(DropSmoke.Trail, drop.X, drop.Y + Tuning.SmokeOffsetY);
                if (drop.Phase == DropPhase.Start)
                {
                    if (drop.Y > HangLine)
                    {
                        drop.VSpeed = 0;
                        drop.Phase = DropPhase.Hang;
                        if (Tuning.HangSteps > 0) drop.HangAlarm = Tuning.HangSteps;
                        stopped = true;
                    }
                }
                else if (drop.Phase == DropPhase.Exit)
                {
                    drop.VSpeed = Tuning.FallSpeed;
                    below = drop.Y > RoomHeight;
                }
            }

            // Each cloud's Step: if(y < -sprite_height) instance_destroy();
            for (int i = 0; i < clouds.Count; i++)
                if (clouds[i].Y < -clouds[i].Height) clouds.RemoveAt(i--);

            // The built-in motion.
            if (drop != null) drop.Y += drop.VSpeed;
            foreach (var cloud in clouds) cloud.Y += cloud.VSpeed;

            // The backgrounds' scroll.
            foreach (var layer in backgrounds)
            {
                layer.X += layer.HSpeed;
                layer.Y += layer.VSpeed;
            }
            return new DropStep(stopped, below);
        }

        /// <summary>obj_drop_cloud's Create, in its order of random draws.</summary>
        DropCloud MakeCloud()
        {
            var t = Tuning;
            var cloud = new DropCloud(++nextCloud);
            // sprite_width and sprite_height are still spr_cloud1's here; GameMaker truncates irandom_range's bounds.
            cloud.X = random.IRandom(Trunc(-t.CloudWidth / 2d), Trunc(RoomWidth - t.CloudWidth / 2d));
            cloud.VSpeed = -random.IRandom(t.CloudSpeedMin, t.CloudSpeedMax);
            cloud.Alpha = t.CloudAlphaStep * random.IRandom(t.CloudAlphaMin, t.CloudAlphaMax);
            cloud.Y = RoomHeight + t.CloudHeight * random.IRandom(t.CloudBelowMin, t.CloudBelowMax);
            cloud.Sprite = t.CloudSprites[random.IRandom(1, t.CloudSprites.Length) - 1];
            cloud.Height = t.CloudHeight;
            return cloud;
        }

        static int Trunc(double value) => (int)Math.Truncate(value);
    }

    /// <summary>What one <see cref="DropScene.Step"/> did to the drop.</summary>
    public readonly struct DropStep
    {
        public DropStep(bool stopped, bool below)
        {
            Stopped = stopped;
            Below = below;
        }

        /// <summary>It reached <c>room_height/4</c> and stopped (phase "hang").</summary>
        public bool Stopped { get; }
        /// <summary>It is falling out and below the room (<c>y &gt; room_height</c> in phase "exit").</summary>
        public bool Below { get; }
    }
}
