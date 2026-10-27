using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Audio;
using Unity.Hare.Content;
using Unity.Hare.Effects;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Popups;
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.Session;
using Unity.Hare.Story;
using Unity.Hare.UI;

namespace Unity.Hare.Map
{
    /// <summary>
    /// room_map played (U.HARE-T19): obj_map and the room's other instances, the map room's view of Core's
    /// <see cref="MapScreen"/>. ProjectSetup puts one in the map scene; it builds, in code:
    /// <list type="bullet">
    /// <item>bg_nightsky, tiled on the backdrop layer and scrolled by background_hspeed[0];</item>
    /// <item>obj_earthball's spr_earthball (depth 0) turning on its origin, and obj_uss_warren's spr_uss_warren (depth 1);</item>
    /// <item>a marker for every mission (<see cref="AlertMarkerView"/>, depth -10; the locked boss's in grey) and its pings;</item>
    /// <item>obj_white_dot's spr_dropdot (depth 1), fading on its way to obj_destination;</item>
    /// <item>obj_btn_upgrade (depth -5): spr_button_area_small, "Upgrades" white or cornflower blue, its message under
    /// the mouse, dimmed with the Draw event by obj_fade_map;</item>
    /// <item>on the GUI layer obj_scenario_details' panel, obj_conversation's comm box (<see cref="ConversationView"/>) and
    /// obj_tip's "Mission Command" (<see cref="TipView"/>), then obj_mouse's spr_pointer over everything.</item>
    /// </list>
    /// It steps the screen on the room flow's clock (<see cref="RoomFlowDirector.RoomStepped"/>), sends A, D, any key
    /// (with A, D and Esc as held), the mouse and the left button as commands, and plays what comes back: sounds, pings,
    /// the session's scenario and mission, the fade to room_upgrade and <c>room_goto(room_intro)</c>. Esc to room_title
    /// is the room flow's (the rooms table's EscapeToTitle). It decides no rule itself. obj_scanner's sweeping beam is
    /// U.HARE-T63's.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object, after the GML object.</summary>
        public const string ObjectName = "obj_map";

        readonly List<AlertMarkerView> markers = new List<AlertMarkerView>();
        RoomDefinition room;
        RoomFlowDirector director;
        PanelSettings panelSettings;
        HareControls controls;
        InputAction anyKey, aKey, dKey, escapeKey, leftButton;
        HareArtManifest manifest;
        Vector2Int backgroundSize;
        Color normalColor, hoverColor, lockedColor;
        VisualElement drawLayer, guiLayer, pointer;
        VisualElement upgradeSprite;
        Label upgradeText, upgradeMessageText;
        string drawnPanel;

        /// <summary>The room's rules and state.</summary>
        public MapScreen State { get; private set; }

        /// <summary>bg_nightsky, tiled.</summary>
        public SpriteRenderer Background { get; private set; }

        /// <summary>obj_earthball.</summary>
        public SpriteRenderer Earth { get; private set; }

        /// <summary>obj_uss_warren.</summary>
        public SpriteRenderer Ship { get; private set; }

        /// <summary>obj_white_dot (off until launched).</summary>
        public SpriteRenderer WhiteDot { get; private set; }

        /// <summary>The mission markers, in obj_map's mission[] order.</summary>
        public IReadOnlyList<AlertMarkerView> Markers => markers;

        /// <summary>The panel's 1920x1080 stage.</summary>
        public VisualElement Stage { get; private set; }

        /// <summary>obj_btn_upgrade's caption and its hover message.</summary>
        public VisualElement UpgradeCaption { get; private set; }
        public VisualElement UpgradeMessage { get; private set; }

        /// <summary>obj_scenario_details and its text.</summary>
        public VisualElement Panel { get; private set; }
        public VisualElement PanelTextElement { get; private set; }

        /// <summary>obj_conversation.</summary>
        public ConversationView Conversation { get; private set; }

        /// <summary>obj_tip, until a key or a click dismisses it.</summary>
        public TipView Tip { get; private set; }

        /// <summary>obj_mouse, which obj_map's Create makes.</summary>
        public MouseView Pointer { get; private set; }

        /// <summary>Every event the screen raised, after it was played.</summary>
        public event Action<MapEvent> EventPlayed;

        void Awake()
        {
            var content = GameContent.Current;
            room = content.Rooms.Get(RoomId.Map);
            manifest = HareArtManifest.Load();
            var map = content.Map;
            normalColor = ParseColor(map.Upgrade.Color, Color.white);
            hoverColor = ParseColor(map.Upgrade.HoverColor, new Color(100f / 255f, 149f / 255f, 237f / 255f));
            lockedColor = ParseColor(map.LockedColor, Color.gray);

            var story = content.Story;
            string script = story.Conversation.MapScript;
            var box = new ConversationBox(story.Conversation, content.Dialogue.Speaker(script), content.Dialogue.Script(script),
                room.Width, room.Height, story.TextDone);
            var active = RoomFlowDirector.Active;
            IEnumerable<int> won = active != null && active.Campaign != null ? active.Campaign.Won : Array.Empty<int>();
            State = new MapScreen(map, content.Popups.Alert, box, won, level => content.Arena.SettingFor(level)?.Name ?? "",
                room.Width, room.Height);

            BuildWorld(map);
            BuildPanel(map);

            // obj_map's Create: mouse = instance_create(0,0,obj_mouse); mouse.sprite_index = spr_pointer.
            Pointer = MouseView.Create(RoomId.Map, gameObject.scene, transform);
            if (Pointer != null) Pointer.DrawOnUi = DrawPointer;

            controls = new HareControls();
            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            aKey = new InputAction("A", InputActionType.Button, "<Keyboard>/a");
            dKey = new InputAction("D", InputActionType.Button, "<Keyboard>/d");
            escapeKey = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            leftButton = new InputAction("LeftButton", InputActionType.Button, "<Mouse>/leftButton");
        }

        IEnumerable<InputAction> Actions()
        {
            yield return anyKey;
            yield return aKey;
            yield return dKey;
            yield return escapeKey;
            yield return leftButton;
        }

        void OnEnable()
        {
            if (controls == null) return;
            controls.Player.Aim.Enable();
            foreach (var action in Actions()) action.Enable();
        }

        void OnDisable()
        {
            if (controls == null) return;
            controls.Player.Aim.Disable();
            foreach (var action in Actions()) action.Disable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null) director.RoomStepped += OnRoomStepped;
            Play(State.Execute(new CreateMap(RoomFlowDirector.Seat)));
            BuildMarkers();
            Present();
        }

        void OnDestroy()
        {
            if (director != null) director.RoomStepped -= OnRoomStepped;
            if (controls != null)
            {
                controls.Disable();
                controls.Dispose();
            }
            foreach (var action in Actions()) action?.Dispose();
            if (panelSettings != null) Destroy(panelSettings);
        }

        void OnRoomStepped(RoomId stepped)
        {
            if (stepped == RoomId.Map) Step();
        }

        /// <summary>One step of the room (the flow's clock calls it; tests may too).</summary>
        public void Step()
        {
            Play(State.Execute(new StepMap(RoomFlowDirector.Seat)));
            Present();
        }

        void Update()
        {
            if (!State.Created || State.Left) return;
            var seat = RoomFlowDirector.Seat;

            // obj_map's A and D key presses, then its any-key press (unless A, D or Esc is held).
            if (aKey.WasPressedThisFrame()) Play(State.Execute(new PressMapA(seat)));
            if (dKey.WasPressedThisFrame()) Play(State.Execute(new PressMapD(seat)));
            if (anyKey.WasPressedThisFrame())
                Play(State.Execute(new PressMapKey(seat, aKey.IsPressed(), dKey.IsPressed(), escapeKey.IsPressed())));

            // obj_btn_upgrade's mouse enter, leave and left pressed events.
            if (TryReadMouse(out var mouse))
            {
                Play(State.Execute(new PointMap(seat, mouse.x, mouse.y)));
                if (leftButton.WasPressedThisFrame()) Play(State.Execute(new PressMap(seat, mouse.x, mouse.y)));
            }
        }

        /// <summary>The mouse in room pixels (HareControls' Player/Aim through the room camera); false with no mouse.</summary>
        bool TryReadMouse(out Vector2 roomPixel)
        {
            roomPixel = default;
            var camera = Camera.main;
            if (camera == null || Mouse.current == null) return false;
            Vector3 screen = controls.Player.Aim.ReadValue<Vector2>();
            screen.z = -camera.transform.position.z;
            Vector2 world = camera.ScreenToWorldPoint(screen);
            roomPixel = ToRoom(world);
            return true;
        }

        /// <summary>The room pixel at <paramref name="world"/> (the room is centred on the world origin, y down).</summary>
        public Vector2 ToRoom(Vector2 world) =>
            new Vector2(world.x * GameClock.PixelsPerUnit + room.Width / 2f, room.Height / 2f - world.y * GameClock.PixelsPerUnit);

        /// <summary>The world point of room pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public Vector2 ToWorld(double x, double y) =>
            new Vector2(GameClock.Px((float)x - room.Width / 2f), GameClock.Px(room.Height / 2f - (float)y));

        void Play(MapResult result)
        {
            var seat = RoomFlowDirector.Seat;
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case MapSoundRequested sound:
                        HareAudio.Play(sound.Sound);
                        break;
                    case MapTipRequested _:
                        var tip = GameContent.Current.Popups.Tip;
                        Tip = TipView.Show(guiLayer, new TipBox(tip, tip.MapText, room.Width, room.Height), room.StepsPerSecond);
                        break;
                    case MapMarkerPinged ping:
                        if (ping.Mission >= 0 && ping.Mission < markers.Count) markers[ping.Mission].Ping(ping.X, ping.Y);
                        break;
                    case MapScenarioSelected selected:
                        if (director != null) director.ExecuteSession(new SelectScenario(seat, selected.Scenario));
                        break;
                    case MapHoverChanged hover:
                        UpgradeMessage.style.display = hover.Hovered ? DisplayStyle.Flex : DisplayStyle.None;
                        break;
                    case MapMissionChosen chosen:
                        if (director != null) director.ExecuteSession(new ChooseMission(seat, chosen.Scenario, chosen.Level));
                        break;
                    case MapRoomRequested go:
                        if (director != null) director.Execute(new GoToRoom(seat, go.Room));
                        break;
                    case MapFadeRequested fade:
                        if (director != null) director.Execute(new FadeToRoom(seat, fade.Room));
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        // ---- Drawing ----

        /// <summary>Draws the state where it stands: the sky's tile, the earth, the markers, the dot and the panel.</summary>
        public void Present()
        {
            if (State == null) return;
            if (Background != null)
            {
                // GameMaker's first tile: background_x mod the image, one image back.
                int x = StartupLayer.Wrap(State.SkyX, backgroundSize.x) - backgroundSize.x;
                int y = -backgroundSize.y;
                Background.transform.position = new Vector3(GameClock.Px(x - room.Width / 2f), GameClock.Px(room.Height / 2f - y), 0f);
            }
            if (Earth != null)
            {
                var at = ToWorld(State.EarthX, State.EarthY);
                Earth.transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, (float)State.EarthAngle));
            }
            if (Ship != null)
            {
                var at = ToWorld(State.ShipX, State.ShipY);
                Ship.transform.position = new Vector3(at.x, at.y, 0f);
            }
            for (int i = 0; i < markers.Count && i < State.Missions.Count; i++)
                markers[i].Present(State.Missions[i].Locked ? lockedColor : Color.white);
            if (WhiteDot != null)
            {
                var dot = State.Dot;
                WhiteDot.enabled = dot != null && !State.Left;
                if (dot != null)
                {
                    var at = ToWorld(dot.X, dot.Y);
                    WhiteDot.transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, (float)dot.ImageAngle));
                    WhiteDot.sprite = HareArt.Frame(State.Tuning.DotSprite, dot.Frame);
                    WhiteDot.color = new Color(1f, 1f, 1f, dot.DrawAlpha);
                }
            }
            if (Panel != null)
            {
                Panel.style.display = State.PanelVisible ? DisplayStyle.Flex : DisplayStyle.None;
                string text = State.PanelDrawText;
                if (text != drawnPanel)
                {
                    drawnPanel = text;
                    HareUi.SetOutlineText(PanelTextElement, text);
                }
            }
        }

        /// <summary>
        /// Every frame: obj_btn_upgrade's colour, and obj_fade_map's black over the Draw-event button (its sprite and
        /// caption darkened by the fade, as the title's menu is); the GUI layer stays over the fade.
        /// </summary>
        void LateUpdate()
        {
            Present();
            float fade = director != null && director.InRoom ? director.Flow.FadeOpacity : 0f;
            float dim = 1f - fade;
            var color = State.UpgradeHovered ? hoverColor : normalColor;
            if (upgradeText != null) upgradeText.style.color = new Color(color.r * dim, color.g * dim, color.b * dim, 1f);
            if (upgradeSprite != null) upgradeSprite.style.unityBackgroundImageTintColor = new Color(dim, dim, dim, 1f);
            if (upgradeMessageText != null) upgradeMessageText.style.color = new Color(dim, dim, dim, 1f);
        }

        void BuildWorld(MapTuning map)
        {
            var sky = new GameObject(map.Background);
            sky.transform.SetParent(transform, false);
            Background = sky.AddComponent<SpriteRenderer>();
            Background.sortingLayerName = HareSortingLayers.Backdrop;
            var background = manifest != null ? manifest.FindBackground(map.Background) : null;
            backgroundSize = background != null ? new Vector2Int(background.width, background.height) : new Vector2Int(room.Width, room.Height);
            if (background == null) Debug.LogWarning("[Unity.Hare] background " + map.Background + " is not imported.");
            else Background.sprite = background.sprite;
            int columns = Mathf.CeilToInt(room.Width / (float)backgroundSize.x) + 2;
            int rows = Mathf.CeilToInt(room.Height / (float)backgroundSize.y) + 1;
            Background.drawMode = SpriteDrawMode.Tiled;
            Background.tileMode = SpriteTileMode.Continuous;
            Background.size = new Vector2(GameClock.Px(columns * backgroundSize.x), GameClock.Px(rows * backgroundSize.y));

            Earth = MakeSprite("obj_earthball", map.EarthSprite, 0);
            Ship = MakeSprite("obj_uss_warren", map.ShipSprite, 1);
            WhiteDot = MakeSprite("obj_white_dot", map.DotSprite, 1);
            WhiteDot.enabled = false;
        }

        /// <summary>obj_map's instance_create for each mission: its marker, drawn where the screen puts it.</summary>
        void BuildMarkers()
        {
            foreach (var m in State.Missions)
            {
                var view = AlertMarkerView.Create(transform, m.Marker, (x, y) => ToWorld(x, y));
                view.name = AlertMarkerView.ObjectName + "_" + m.Scenario;
                markers.Add(view);
            }
        }

        SpriteRenderer MakeSprite(string name, string sprite, int depth)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = HareSortingLayers.Default;
            renderer.sortingOrder = HareArt.SortingOrderForDepth(depth);
            renderer.sprite = HareArt.Frame(sprite);
            if (renderer.sprite == null) Debug.LogWarning("[Unity.Hare] " + sprite + " is not imported.");
            return renderer;
        }

        void BuildPanel(MapTuning map)
        {
            var host = new GameObject("Gui");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            Stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();

            drawLayer = Layer("draw", Stage);
            guiLayer = Layer("draw-gui", Stage);

            // obj_btn_upgrade's Draw: draw_self (spr_button_area_small at its origin), "Upgrades" at (x, y-10),
            // fa_center, fnt_point, size-2 outline; its message at (room_width / 2, room_height - 30) while hovered.
            var b = map.Upgrade;
            upgradeSprite = Image(b.Sprite, b.X - b.XOrigin, b.Y - b.YOrigin, b.Width, b.Height);
            upgradeSprite.name = b.Sprite;
            drawLayer.Add(upgradeSprite);
            UpgradeCaption = HareUi.OutlineText(b.Label, b.Outline, HareTextStyles.MenuText.UssClass, drawLayer);
            UpgradeCaption.name = b.Object;
            UpgradeCaption.style.position = Position.Absolute;
            UpgradeCaption.style.left = b.X;
            UpgradeCaption.style.top = b.Y + b.LabelOffset;
            UpgradeCaption.style.translate = new Translate(Length.Percent(-50), 0);
            UpgradeCaption.style.unityTextAlign = TextAnchor.UpperCenter;
            upgradeText = TopLabel(UpgradeCaption);
            UpgradeMessage = HareUi.OutlineText(b.Message, b.Outline, HareTextStyles.MenuText.UssClass, drawLayer);
            UpgradeMessage.name = b.Object + "-message";
            UpgradeMessage.style.position = Position.Absolute;
            UpgradeMessage.style.left = 0;
            UpgradeMessage.style.width = room.Width;
            UpgradeMessage.style.top = room.Height - b.MessageBottomMargin;
            UpgradeMessage.style.unityTextAlign = TextAnchor.UpperCenter;
            UpgradeMessage.style.display = DisplayStyle.None;
            upgradeMessageText = TopLabel(UpgradeMessage);

            // obj_scenario_details' Draw GUI: spr_tip_box at alpha 0.7, then the text at alpha 1, fa_left, fnt_point,
            // size-1 outline at (x + margin, y + margin).
            Panel = new VisualElement { name = "obj_scenario_details", pickingMode = PickingMode.Ignore };
            Panel.style.position = Position.Absolute;
            Panel.style.left = map.PanelX;
            Panel.style.top = map.PanelY;
            var tip = GameContent.Current.Popups.Tip;
            var panelBox = Image(map.PanelSprite, 0, 0, tip.Width, tip.Height);
            panelBox.style.opacity = map.PanelAlpha;
            Panel.Add(panelBox);
            PanelTextElement = HareUi.OutlineText("", map.PanelOutline, HareTextStyles.MenuText.UssClass, Panel);
            PanelTextElement.style.position = Position.Absolute;
            PanelTextElement.style.left = map.PanelMargin;
            PanelTextElement.style.top = map.PanelMargin;
            PanelTextElement.style.unityTextAlign = TextAnchor.UpperLeft;
            Panel.style.display = DisplayStyle.None;
            guiLayer.Add(Panel);

            // obj_conversation (mode "off" until a mission is reached).
            Conversation = ConversationView.Show(guiLayer, State.Conversation, room.StepsPerSecond);

            pointer = new VisualElement { name = MouseView.ObjectName, pickingMode = PickingMode.Ignore };
            pointer.style.position = Position.Absolute;
            pointer.style.display = DisplayStyle.None;
            Stage.Add(pointer);
        }

        VisualElement Layer(string name, VisualElement parent)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.width = room.Width;
            layer.style.height = room.Height;
            parent.Add(layer);
            return layer;
        }

        VisualElement Image(string sprite, float left, float top, float width, float height)
        {
            var image = new VisualElement { name = sprite, pickingMode = PickingMode.Ignore };
            image.style.position = Position.Absolute;
            image.style.left = left;
            image.style.top = top;
            image.style.width = width;
            image.style.height = height;
            var frame = manifest != null ? manifest.Frame(sprite) : null;
            if (frame != null) image.style.backgroundImage = new StyleBackground(frame);
            else Debug.LogWarning("[Unity.Hare] " + sprite + " is not imported.");
            return image;
        }

        /// <summary>The outline stack's last label, the text drawn over the outline.</summary>
        static Label TopLabel(VisualElement outlineText) => outlineText[outlineText.childCount - 1] as Label;

        /// <summary>obj_mouse's draw_self on the panel: its sprite with its origin on the mouse, over everything.</summary>
        void DrawPointer(MouseView view)
        {
            var sprite = view.Renderer != null ? view.Renderer.sprite : null;
            if (!view.Drawn || sprite == null)
            {
                pointer.style.display = DisplayStyle.None;
                return;
            }
            var rect = sprite.rect;
            pointer.style.display = DisplayStyle.Flex;
            pointer.style.backgroundImage = new StyleBackground(sprite);
            pointer.style.width = rect.width;
            pointer.style.height = rect.height;
            pointer.style.left = view.RoomPoint.x - sprite.pivot.x;
            pointer.style.top = view.RoomPoint.y - (rect.height - sprite.pivot.y);
        }

        static Color ParseColor(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;

        /// <summary>The room's object, as ProjectSetup puts it in the map scene.</summary>
        public static MapRoom Create() => new GameObject(ObjectName).AddComponent<MapRoom>();
    }
}
