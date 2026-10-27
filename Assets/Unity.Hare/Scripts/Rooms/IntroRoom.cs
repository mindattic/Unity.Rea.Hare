using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Popups;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Rooms
{
    /// <summary>
    /// room_intro played (U.HARE-T62): obj_intro and obj_drop_intro, the intro room's view of Core's
    /// <see cref="IntroScreen"/>. ProjectSetup puts one in the intro scene; it builds the backgrounds, clouds and falling
    /// rabbit (<see cref="DropSceneView"/>) and a 1920x1080 GUI panel for the tip in code. It steps the screen on the room
    /// flow's clock (<see cref="RoomFlowDirector.RoomStepped"/>) with A and D as held (obj_drop_intro's keyboard events),
    /// sends each key press as <see cref="PressIntroKey"/>, and plays what comes back: the smoke, the controls tip
    /// (<see cref="TipView"/> with <see cref="TipDefinition.IntroText"/>), the quick game through the session and
    /// <c>room_goto(room_gamescreen)</c> through the flow. obj_intro's Esc to room_title is the room flow's (the rooms
    /// table's EscapeToTitle). It decides no rule itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntroRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object, after the GML object.</summary>
        public const string ObjectName = "obj_intro";

        RoomDefinition room;
        RoomFlowDirector director;
        PanelSettings panelSettings;
        InputAction anyKey, aKey, dKey;

        /// <summary>The room's rules and state.</summary>
        public IntroScreen State { get; private set; }

        /// <summary>The backgrounds, clouds and falling rabbit as drawn.</summary>
        public DropSceneView SceneView { get; private set; }

        /// <summary>The panel's 1920x1080 stage, the GUI layer the tip is drawn on.</summary>
        public VisualElement Stage { get; private set; }

        /// <summary>The controls tip once the rabbit has stopped (it goes when a key or a click dismisses it).</summary>
        public TipView Tip { get; private set; }

        /// <summary>Every event the screen raised, after it was played.</summary>
        public event Action<IntroEvent> EventPlayed;

        void Awake()
        {
            var content = GameContent.Current;
            room = content.Rooms.Get(RoomId.Intro);
            // The map's launched mission, if the campaign came here from room_map (U.HARE-T19, docs/BIBLE.md V42).
            var session = RoomFlowDirector.Active != null ? RoomFlowDirector.Active.Session : null;
            bool mission = session != null && session.Mode == GameMode.Campaign && session.Scenario > 0 && session.MissionLevel > 0;
            State = new IntroScreen(content.Intro, room.Width, room.Height, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks)),
                mission ? session.Scenario : -1, mission ? session.MissionLevel : 0);
            SceneView = DropSceneView.Create(transform, State.Scene);
            BuildPanel();

            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            aKey = new InputAction("A", InputActionType.Button, "<Keyboard>/a");
            dKey = new InputAction("D", InputActionType.Button, "<Keyboard>/d");
        }

        IEnumerable<InputAction> Actions()
        {
            yield return anyKey;
            yield return aKey;
            yield return dKey;
        }

        void OnEnable()
        {
            foreach (var action in Actions()) action?.Enable();
        }

        void OnDisable()
        {
            foreach (var action in Actions()) action?.Disable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null) director.RoomStepped += OnRoomStepped;
            Play(State.Execute(new CreateIntro(RoomFlowDirector.Seat)));
        }

        void OnDestroy()
        {
            if (director != null) director.RoomStepped -= OnRoomStepped;
            foreach (var action in Actions()) action?.Dispose();
            if (panelSettings != null) Destroy(panelSettings);
        }

        void OnRoomStepped(RoomId stepped)
        {
            if (stepped == RoomId.Intro) Play(State.Execute(new StepIntro(RoomFlowDirector.Seat, aKey.IsPressed(), dKey.IsPressed())));
        }

        void Update()
        {
            // obj_drop_intro's key press for any key, unless A or D is held.
            if (State.Created && !State.Left && anyKey.WasPressedThisFrame())
                Play(State.Execute(new PressIntroKey(RoomFlowDirector.Seat, aKey.IsPressed(), dKey.IsPressed())));
        }

        void Play(IntroResult result)
        {
            var seat = RoomFlowDirector.Seat;
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case IntroSmokePuffed smoke:
                        SceneView.Smoke(smoke.X, smoke.Y, smoke.Size, smoke.Color);
                        break;
                    case IntroTipRequested _:
                        var tip = GameContent.Current.Popups.Tip;
                        Tip = TipView.Show(Stage, new TipBox(tip, tip.IntroText, room.Width, room.Height), room.StepsPerSecond);
                        break;
                    case IntroModeChosen mode:
                        if (director != null) director.ExecuteSession(new ChooseQuickstart(seat, mode.Level));
                        break;
                    case IntroMissionKept kept:
                        if (director != null) director.ExecuteSession(new ChooseMission(seat, kept.Scenario, kept.Level));
                        break;
                    case IntroRoomRequested go:
                        if (director != null) director.Execute(new GoToRoom(seat, go.Room));
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        void BuildPanel()
        {
            var host = new GameObject("Gui");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            Stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();
        }

        /// <summary>The room's object, as ProjectSetup puts it in the intro scene.</summary>
        public static IntroRoom Create() => new GameObject(ObjectName).AddComponent<IntroRoom>();
    }
}
