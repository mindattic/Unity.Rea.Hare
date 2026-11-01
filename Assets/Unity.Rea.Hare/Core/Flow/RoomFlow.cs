using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Flow
{
    /// <summary>
    /// The original's room flow, engine-free: which room the game is in, <c>next_room</c>, and the room's
    /// <c>obj_fade_*</c>. There is no global instance: whoever holds the game's authority owns one. State changes
    /// only through <see cref="Execute"/>, which runs a <see cref="FlowCommand"/> and returns the
    /// <see cref="FlowEvent"/>s it raised; presentation plays them (a <see cref="RoomChanged"/> loads that room's
    /// scene) and never writes back.
    ///
    /// The original moves between rooms two ways, both here: at once with <c>room_goto</c> (<see cref="GoToRoom"/>:
    /// the Esc events, obj_startup's any key, obj_white_dot, obj_drop_intro, the yes/no dialog) or through the room's
    /// fade (<see cref="FadeToRoom"/>: the menu buttons, obj_story, obj_scroll_world, obj_btn_map/_upgrade), whose
    /// Step (<see cref="StepRoom"/>, at the room's room_speed) runs <c>room_goto(next_room)</c> once the screen is
    /// black. Entering a room creates its fade (alpha 1, fading out), so every room with one comes up from black.
    /// </summary>
    public sealed class RoomFlow
    {
        readonly List<FlowEvent> events = new List<FlowEvent>();
        PlayerId issuer;

        /// <param name="content">The data to run from; null reads <see cref="GameContent.Current"/>.</param>
        public RoomFlow(ContentRegistry content = null)
        {
            content ??= GameContent.Current;
            Table = content.Rooms;
            NextRoom = Table.StartingNextRoom;
        }

        public RoomTable Table { get; }

        /// <summary>The room the game is in; null until the first <see cref="GoToRoom"/> or <see cref="EnterRoom"/>.</summary>
        public RoomId? Current { get; private set; }

        /// <summary>The current room's definition; null before the first room.</summary>
        public RoomDefinition CurrentDefinition { get; private set; }

        /// <summary>GML: next_room, where the fade-in goes.</summary>
        public RoomId NextRoom { get; private set; }

        /// <summary>The current room's obj_fade_*; null in a room with none (startup, room_intro).</summary>
        public RoomFade Fade { get; private set; }

        /// <summary>The fade's drawn opacity, 0 when the room has no fade.</summary>
        public float FadeOpacity => Fade?.Opacity ?? 0f;

        /// <summary>The current room's room_speed; 0 before the first room.</summary>
        public int StepsPerSecond => CurrentDefinition?.StepsPerSecond ?? 0;

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public FlowResult Execute(FlowCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<FlowEvent>() : events.ToArray();
            events.Clear();
            return new FlowResult(accepted, raised);
        }

        void Emit(FlowEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        // ---- Rules the commands call ----

        /// <summary>The room's instances are created: its definition and a fresh obj_fade_* (if it has one).</summary>
        void Enter(RoomId room)
        {
            CurrentDefinition = Table.Get(room);
            Current = room;
            Fade = CurrentDefinition.Fade == FadeKind.None ? null : new RoomFade(CurrentDefinition.Fade, Table.FadeSpeed);
        }

        /// <summary>room_goto(room).</summary>
        internal bool GoTo(RoomId room)
        {
            var from = Current;
            Enter(room);
            Emit(new RoomChanged { From = from, To = room });
            return true;
        }

        /// <summary>The room was loaded again outside the flow (room_restart, or a room scene opened on its own).</summary>
        internal bool Reenter(RoomId room)
        {
            Enter(room);
            Emit(new RoomEntered { Room = room });
            return true;
        }

        /// <summary><c>next_room = room; fade.mode = "fadein"; fade.firstPass = true;</c></summary>
        internal bool FadeTo(RoomId room, bool onlyWhenIdle)
        {
            if (!CanStartFade(onlyWhenIdle)) return false;
            NextRoom = room;
            Fade.BeginFadeIn();
            Emit(new FadeInStarted { NextRoom = room });
            return true;
        }

        /// <summary><c>fade.mode = "fadein"; fade.firstPass = true;</c> with next_room left as it is.</summary>
        internal bool FadeIn(bool onlyWhenIdle)
        {
            if (!CanStartFade(onlyWhenIdle)) return false;
            Fade.BeginFadeIn();
            Emit(new FadeInStarted { NextRoom = NextRoom });
            return true;
        }

        /// <summary>
        /// A room with no obj_fade_* has nothing to fade; a button that checks <c>obj_fade_title.mode == "off"</c>
        /// first (obj_btn_campaign, obj_btn_diff*, obj_btn_level*) does nothing while a fade is running.
        /// </summary>
        bool CanStartFade(bool onlyWhenIdle) => Fade != null && (!onlyWhenIdle || Fade.Mode == FadeMode.Off);

        /// <summary>The controller's Esc key-press event: <c>room_goto(room_title)</c>, in the rooms that have one.</summary>
        internal bool Escape()
        {
            if (CurrentDefinition == null || !CurrentDefinition.EscapeToTitle) return false;
            return GoTo(Table.EscapeRoom);
        }

        /// <summary>The controller's F1 key-press event: <c>window_set_fullscreen(!window_get_fullscreen())</c>.</summary>
        internal bool ToggleFullscreen()
        {
            if (CurrentDefinition == null || !CurrentDefinition.FullscreenKey) return false;
            Emit(new FullscreenToggled());
            return true;
        }

        /// <summary>One room step: the fade's Step event, and what it does once the fade-in is opaque.</summary>
        internal bool Step()
        {
            if (CurrentDefinition == null) return false;
            if (Fade == null || !Fade.Step()) return true;

            if (Fade.Kind == FadeKind.NextRoom) GoTo(NextRoom);
            else Emit(new LevelAdvanceRequested()); // obj_fade_game: current_level += 1; setup_level();
            return true;
        }
    }

    /// <summary>What <see cref="RoomFlow.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct FlowResult
    {
        public FlowResult(bool accepted, IReadOnlyList<FlowEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<FlowEvent> Events { get; }
    }
}
