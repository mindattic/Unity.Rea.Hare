using System;
using System.Collections.Generic;

namespace Unity.Hare.Rooms
{
    /// <summary>
    /// room_intro, engine-free: obj_drop_intro falling in through obj_drop_emitter's clouds over bg_nightsky and
    /// bg_gradient3, stopping a quarter of the way down with the controls tip (obj_tip), steered by A and D, and sent on
    /// by any other key to fall out of the room into a quick game at level 1. There is no global instance: the intro
    /// room's view owns one. State changes only through <see cref="Execute"/>; presentation draws <see cref="Scene"/>
    /// and plays the events (smoke, the tip, the session's game mode, the room change) and never writes back.
    ///
    /// Nothing in the original goes to room_intro (no script or object names it, and it is not the first room); it is
    /// ported because it is a real room, and the room flow can be sent there.
    /// </summary>
    public sealed class IntroScreen
    {
        readonly List<IntroEvent> events = new List<IntroEvent>();
        PlayerId issuer;

        /// <param name="tuning">The intro data (the registry's <c>intro</c> section).</param>
        /// <param name="roomWidth">room_width, 1920.</param>
        /// <param name="roomHeight">room_height, 1080.</param>
        /// <param name="random">Where the drop's shake and the clouds' irandom_range come from.</param>
        public IntroScreen(IntroTuning tuning, int roomWidth, int roomHeight, IRandomSource random)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Scene = new DropScene(tuning.Scene, roomWidth, roomHeight, random ?? throw new ArgumentNullException(nameof(random)));
        }

        public IntroTuning Tuning { get; }

        /// <summary>The falling rabbit, its clouds and the backgrounds.</summary>
        public DropScene Scene { get; }

        /// <summary>Whether the room's Create has run.</summary>
        public bool Created { get; private set; }

        /// <summary>Whether the rabbit has stopped and made its tip.</summary>
        public bool TipShown { get; private set; }

        /// <summary>Whether it has fallen out of the room and sent the game on (the room is on its way out).</summary>
        public bool Left { get; private set; }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public IntroResult Execute(IntroCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<IntroEvent>() : events.ToArray();
            events.Clear();
            return new IntroResult(accepted, raised);
        }

        void Emit(IntroEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        // ---- Rules the commands call ----

        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            Scene.Create();
            return true;
        }

        /// <summary>
        /// A and D, then one step of the scene. When the rabbit stops it makes the tip; once it is below the room in
        /// phase "exit" it sets the quick game and <c>room_goto(room_gamescreen)</c>, once.
        /// </summary>
        internal bool Step(bool left, bool right)
        {
            if (!Created) return false;
            Scene.Steer(left, right, Tuning.SteerMargin, Tuning.SteerStep);
            var step = Scene.Step((smoke, x, y) => Emit(new IntroSmokePuffed
            {
                Smoke = smoke,
                X = x,
                Y = y,
                Size = Scene.Tuning.SmokeSize,
                Color = smoke == DropSmoke.Trail ? Scene.Tuning.TrailColor : Scene.Tuning.PuffColor,
            }));
            if (step.Stopped && !TipShown)
            {
                TipShown = true;
                Emit(new IntroTipRequested());
            }
            if (step.Below && !Left)
            {
                Left = true;
                Emit(new IntroModeChosen { Level = Tuning.ExitLevel });
                Emit(new IntroRoomRequested { Room = Tuning.ExitRoom });
            }
            return true;
        }

        /// <summary>The key press: <c>if (!keyboard_check(ord("A")) &amp;&amp; !keyboard_check(ord("D"))) { if(phase == "hang") phase = "exit"; }</c></summary>
        internal bool PressKey(bool aHeld, bool dHeld)
        {
            if (!Created || aHeld || dHeld) return false;
            if (!Scene.Release()) return false;
            Emit(new IntroDropReleased());
            return true;
        }
    }

    /// <summary>What <see cref="IntroScreen.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct IntroResult
    {
        public IntroResult(bool accepted, IReadOnlyList<IntroEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<IntroEvent> Events { get; }
    }
}
