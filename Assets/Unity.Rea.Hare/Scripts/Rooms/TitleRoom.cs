using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Audio;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Rooms
{
    /// <summary>
    /// obj_title and the menu, played (U.HARE-T26, U.HARE-T62): the title room's view of Core's <see cref="TitleScreen"/>.
    /// ProjectSetup puts one in the title scene; it makes the room's obj_mouse (spr_pointer, hidden until obj_logo) as
    /// obj_title's Create does, and builds everything it draws in code. In the world, under the fade:
    /// <list type="bullet">
    /// <item>the backgrounds, the clouds and the falling rabbit with its smoke (<see cref="DropSceneView"/>);</item>
    /// <item>obj_stand_title's Draw: spr_moon_surface, spr_eyeback, spr_pupil following the mouse, then spr_stand;</item>
    /// <item>obj_logo (spr_logo at its image_alpha).</item>
    /// </list>
    /// On a 1920x1080 UI panel over them, in GameMaker's draw order:
    /// <list type="bullet">
    /// <item>the menu's captions (each obj_btn_*'s Draw: <c>draw_text_outline_color_alpha</c> at <c>obj_logo.image_alpha</c>,
    /// centred on x, white or c_cornflowerblue) and the hovered button's message at <c>room_height - 30</c>; these are
    /// Draw-event text at depth -5, under obj_fade_title (depth -100), so each frame their colour is darkened by the
    /// fade's opacity, which is exactly the black spr_black stretched over them;</item>
    /// <item>obj_yesno_dialog's Draw GUI (spr_tip_box at 0.7, its text and message) and its Yes and No, over the fade;</item>
    /// <item>obj_mouse (depth -5000), over all of it, drawn once (<see cref="MouseView.DrawOnUi"/>).</item>
    /// </list>
    /// It steps the screen on the room flow's clock (<see cref="RoomFlowDirector.RoomStepped"/>), sends the mouse,
    /// the left button and the keys as commands, and plays what comes back: sounds through <see cref="HareAudio"/>, the
    /// game mode as the session's command, the fade as the flow's <see cref="FadeToRoom"/>, and <c>game_end()</c> as
    /// <see cref="QuitGame"/>. It decides no rule itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TitleRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object, after the GML object.</summary>
        public const string ObjectName = "obj_title";

        /// <summary>
        /// GML game_end(): quits the player. In the Editor (and so in a test run) it only logs, so it never stops the
        /// Editor's play mode or a test. Tests replace it to watch the quit.
        /// </summary>
        public static Action QuitGame = DefaultQuit;

        readonly Dictionary<TitleButton, ButtonView> views = new Dictionary<TitleButton, ButtonView>();
        RoomDefinition room;
        RoomFlowDirector director;
        PanelSettings panelSettings;
        HareControls controls;
        InputAction anyKey, leftButton, f1Key, escapeKey, enterKey;
        Color normalColor, hoverColor;
        VisualElement menuLayer, guiLayer, dialogView, pointer;
        HareArtManifest manifest;
        DropSceneView sceneView;
        SpriteRenderer moon, eye, pupil, stand, logo;
        Vector2 lastMouse;
        VisualElement creditsLink, creditsView;

        sealed class ButtonView
        {
            public VisualElement Caption;
            public Label Text;
            public VisualElement Message;
            public Label MessageText;
        }

        /// <summary>The room's rules and state.</summary>
        public TitleScreen State { get; private set; }

        /// <summary>obj_mouse, which obj_title's Create makes.</summary>
        public MouseView Pointer { get; private set; }

        /// <summary>The panel's 1920x1080 stage.</summary>
        public VisualElement Stage { get; private set; }

        /// <summary>The menu's Draw-event layer (captions and hover messages, under the fade).</summary>
        public VisualElement MenuLayer => menuLayer;

        /// <summary>The GUI layer (the dialog, its Yes and No and their hover messages, over the fade).</summary>
        public VisualElement GuiLayer => guiLayer;

        /// <summary>The dialog as drawn, or null.</summary>
        public VisualElement DialogView => dialogView;

        /// <summary>obj_mouse as drawn on the panel.</summary>
        public VisualElement PointerElement => pointer;

        /// <summary>The backgrounds, clouds and falling rabbit as drawn.</summary>
        public DropSceneView SceneView => sceneView;

        /// <summary>obj_stand_title's sprites as drawn: spr_stand, spr_moon_surface, spr_eyeback and spr_pupil.</summary>
        public SpriteRenderer Stand => stand;
        public SpriteRenderer Moon => moon;
        public SpriteRenderer Eye => eye;
        public SpriteRenderer Pupil => pupil;

        /// <summary>obj_logo as drawn (off until the menu is shown).</summary>
        public SpriteRenderer Logo => logo;

        /// <summary>Every event the screen raised, after it was played.</summary>
        public event Action<TitleEvent> EventPlayed;

        void Awake()
        {
            var content = GameContent.Current;
            room = content.Rooms.Get(RoomId.Title);
            State = new TitleScreen(content.Title, room.Width, room.Height, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks)));
            manifest = HareArtManifest.Load();
            normalColor = ParseColor(content.Title.Color, Color.white);
            hoverColor = ParseColor(content.Title.HoverColor, new Color(100f / 255f, 149f / 255f, 237f / 255f));
            BuildWorld();
            BuildPanel();

            // obj_title's Create: mouse = instance_create(0,0,obj_mouse); mouse.sprite_index = spr_pointer; mouse.visible = false;
            Pointer = MouseView.Create(RoomId.Title, gameObject.scene, transform);
            if (Pointer != null) Pointer.DrawOnUi = DrawPointer;

            controls = new HareControls();
            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            leftButton = new InputAction("LeftButton", InputActionType.Button, "<Mouse>/leftButton");
            f1Key = new InputAction("F1", InputActionType.Button, "<Keyboard>/f1");
            escapeKey = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            enterKey = new InputAction("Enter", InputActionType.Button);
            enterKey.AddBinding("<Keyboard>/enter");
            enterKey.AddBinding("<Keyboard>/numpadEnter");
        }

        IEnumerable<InputAction> Actions()
        {
            yield return anyKey;
            yield return leftButton;
            yield return f1Key;
            yield return escapeKey;
            yield return enterKey;
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
            Play(State.Execute(new CreateTitle(RoomFlowDirector.Seat)));
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
            if (stepped == RoomId.Title) Play(State.Execute(new StepTitle(RoomFlowDirector.Seat)));
        }

        void Update()
        {
            if (!State.Created) return;
            var seat = RoomFlowDirector.Seat;

            // The credits screen (U.HARE-T69, BIBLE V52), a deviation: the original has no credits room. While it is
            // open it takes every click and Esc, and the title underneath hears nothing.
            if (creditsView != null)
            {
                if (escapeKey.WasPressedThisFrame() || leftButton.WasPressedThisFrame()) CloseCredits();
                return;
            }
            if (creditsLink != null && State.Dialog == null && TryReadMouse(out var creditsMouse))
            {
                bool over = creditsMouse.x >= room.Width - CreditsLinkWidth && creditsMouse.y >= room.Height - CreditsLinkHeight;
                SetCreditsLinkHover(over);
                if (over && leftButton.WasPressedThisFrame())
                {
                    OpenCredits();
                    return;
                }
            }

            // obj_yesno_dialog's key presses: Esc closes it, Enter ends the game.
            if (State.Dialog != null)
            {
                if (escapeKey.WasPressedThisFrame()) Play(State.Execute(new EscapeTitleDialog(seat)));
                else if (enterKey.WasPressedThisFrame()) Play(State.Execute(new ConfirmTitleDialog(seat)));
            }

            // The buttons' mouse enter, leave and left pressed events.
            if (TryReadMouse(out var mouse))
            {
                lastMouse = mouse;
                Play(State.Execute(new PointTitle(seat, mouse.x, mouse.y)));
                if (leftButton.WasPressedThisFrame()) Play(State.Execute(new PressTitle(seat, mouse.x, mouse.y, FadeIdle)));
            }

            // obj_title: the key press for any key (if !keyboard_check(vk_f1)) and the global left button
            // (if !keyboard_check(vk_f1) && !keyboard_check(vk_escape)) turn obj_stand_title on.
            if (!State.MenuShown && !State.Stand.On && !f1Key.IsPressed()
                && (anyKey.WasPressedThisFrame() || (leftButton.IsPressed() && !escapeKey.IsPressed())))
                Play(State.Execute(new CallTitleMenu(seat)));
        }

        /// <summary><c>obj_fade_title.mode == "off"</c>; with no room flow (a scene opened on its own) there is no fade to wait for.</summary>
        bool FadeIdle => director == null || director.Flow.Fade == null || director.Flow.Fade.Mode == FadeMode.Off;

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
        public Vector2 ToWorld(float x, float y) => new Vector2(GameClock.Px(x - room.Width / 2f), GameClock.Px(room.Height / 2f - y));

        void Play(TitleResult result)
        {
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case TitleSoundRequested sound:
                        HareAudio.Play(sound.Sound);
                        break;
                    case TitleSmokePuffed smoke:
                        sceneView.Smoke(smoke.X, smoke.Y, smoke.Size, smoke.Color);
                        break;
                    case MenuShown _:
                        BuildMenu();
                        BuildCreditsLink();
                        if (Pointer != null) Pointer.State.SetVisible(true); // obj_logo: obj_mouse.visible = true
                        break;
                    case TitleHoverChanged hover:
                        if (views.TryGetValue(hover.Button, out var view)) view.Message.style.display = hover.Hovered ? DisplayStyle.Flex : DisplayStyle.None;
                        break;
                    case TitleModeChosen mode:
                        if (director != null) director.ExecuteSession(ModeCommand(mode));
                        break;
                    case TitleFadeRequested fade:
                        // A level button's room_gamescreen is where its level script plays (level_04 the shaft, U.HARE-T64).
                        if (director != null) director.Execute(new FadeToRoom(RoomFlowDirector.Seat, EntryRoom(director, fade.Room), fade.OnlyWhenIdle));
                        break;
                    case TitleDialogOpened opened:
                        BuildDialog(opened.Dialog);
                        break;
                    case TitleDialogClosed _:
                        RemoveDialog();
                        break;
                    case TitlePointerOnGuiChanged gui:
                        if (Pointer != null) Pointer.State.SetOnGui(gui.OnGui);
                        break;
                    case QuitRequested _:
                        QuitGame?.Invoke();
                        break;
                    case TitleRoomRequested go:
                        if (director != null) director.Execute(new GoToRoom(RoomFlowDirector.Seat, go.Room));
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        /// <summary>The play room the session's level script runs in, for a fade to a play room (<see cref="Arena.LevelSetup.EntryRoom"/>).</summary>
        internal static RoomId EntryRoom(RoomFlowDirector director, RoomId requested)
        {
            var content = GameContent.Current;
            return Arena.LevelSetup.EntryRoom(content.Arena, content.Story, director.Session, requested);
        }

        static SessionCommand ModeCommand(TitleModeChosen mode)
        {
            var seat = RoomFlowDirector.Seat;
            switch (mode.Mode)
            {
                case TitleMode.Quickstart: return new ChooseQuickstart(seat, mode.Level);
                case TitleMode.Generator: return new ChooseGenerator(seat, mode.Difficulty);
                default: return new ChooseCampaign(seat);
            }
        }

        static void DefaultQuit()
        {
            if (Application.isEditor)
            {
                Debug.Log("[Unity.Hare] game_end(): the player would quit here; the Editor keeps running.");
                return;
            }
            Application.Quit();
        }

        // ---- Drawing ----

        /// <summary>
        /// The room's world: the drop scene (backgrounds, clouds, the rabbit), obj_stand_title's four sprites in its Draw's
        /// order (moon, eye, pupil, then itself at the room's half scale) and obj_logo, hidden until it exists.
        /// </summary>
        void BuildWorld()
        {
            sceneView = DropSceneView.Create(transform, State.Scene);
            var s = State.Tuning.Stand;
            moon = sceneView.MakeSprite("spr_moon_surface", s.MoonSprite, DropSceneView.MoonOrder);
            eye = sceneView.MakeSprite("spr_eyeback", s.EyeSprite, DropSceneView.EyeOrder, (float)s.EyeScale);
            pupil = sceneView.MakeSprite("spr_pupil", s.PupilSprite, DropSceneView.PupilOrder, (float)s.EyeScale);
            stand = sceneView.MakeSprite("obj_stand_title", s.Sprite, DropSceneView.StandOrder, (float)s.Scale);
            logo = sceneView.MakeSprite("obj_logo", State.Tuning.Logo.Sprite, DropSceneView.LogoOrder);
            logo.enabled = false;
            DrawStand();
        }

        /// <summary>
        /// obj_stand_title's Draw (<c>draw_sprite(spr_moon_surface,0,0,y+250)</c>, spr_eyeback at <c>(x-6, y-368)</c>,
        /// spr_pupil at <c>(x-12+mouse_x/120, y-370+mouse_y/60)</c>, <c>draw_self()</c>) and obj_logo's <c>draw_self()</c>
        /// at its image_alpha. mouse_x and mouse_y are the room's mouse whether or not obj_mouse is visible.
        /// </summary>
        void DrawStand()
        {
            var st = State.Stand;
            sceneView.Place(moon, st.MoonX, st.MoonY);
            sceneView.Place(eye, st.EyeX, st.EyeY);
            st.PupilAt(lastMouse.x, lastMouse.y, out double px, out double py);
            sceneView.Place(pupil, px, py);
            sceneView.Place(stand, st.X, st.Y);
            logo.enabled = State.MenuShown && logo.sprite != null;
            if (State.MenuShown)
            {
                sceneView.Place(logo, State.LogoX, State.LogoY);
                logo.color = new Color(1f, 1f, 1f, (float)Math.Max(0d, Math.Min(1d, State.MenuAlpha)));
            }
        }

        void BuildPanel()
        {
            var host = new GameObject("Menu");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            Stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();

            menuLayer = Layer("draw", Stage);
            guiLayer = Layer("draw-gui", Stage);
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

        /// <summary>obj_logo's instance_create for each button: its caption and (hidden until hovered) message.</summary>
        void BuildMenu()
        {
            foreach (var button in State.Menu) AddButton(button, menuLayer);
        }

        void AddButton(TitleButton button, VisualElement layer)
        {
            var d = button.Definition;
            var tuning = State.Tuning;
            var caption = HareUi.OutlineText(d.Label, tuning.Outline, (d.LargeLabel ? HareTextStyles.MenuTitle : HareTextStyles.MenuText).UssClass, layer);
            caption.name = d.Object;
            caption.style.position = Position.Absolute;
            caption.style.left = (float)button.X;
            caption.style.top = (float)button.Y + d.LabelOffset;
            caption.style.translate = new Translate(Length.Percent(-50), 0);
            caption.style.unityTextAlign = TextAnchor.UpperCenter;

            // draw_text_outline(room_width / 2, room_height - 30, 2, message) in fnt_point, white, at alpha 1.
            var message = HareUi.OutlineText(d.Message, tuning.Outline, HareTextStyles.MenuText.UssClass, layer);
            message.name = d.Object + "-message";
            message.style.position = Position.Absolute;
            message.style.left = 0;
            message.style.width = room.Width;
            message.style.top = room.Height - tuning.MessageBottomMargin;
            message.style.unityTextAlign = TextAnchor.UpperCenter;
            message.style.display = button.Hovered ? DisplayStyle.Flex : DisplayStyle.None;

            views[button] = new ButtonView { Caption = caption, Text = TopLabel(caption), Message = message, MessageText = TopLabel(message) };
        }

        const float CreditsLinkWidth = 260f, CreditsLinkHeight = 70f;

        /// <summary>The title's Credits link, bottom right (U.HARE-T69).</summary>
        void BuildCreditsLink()
        {
            if (creditsLink != null) return;
            creditsLink = HareUi.OutlineText("Credits", 2, HareTextStyles.MenuText.UssClass, menuLayer);
            creditsLink.name = "credits-link";
            creditsLink.style.position = Position.Absolute;
            creditsLink.style.right = 30;
            creditsLink.style.bottom = 20;
        }

        void SetCreditsLinkHover(bool hover)
        {
            var text = TopLabel(creditsLink);
            if (text != null) text.style.color = hover ? hoverColor : normalColor;
        }

        void OpenCredits()
        {
            if (creditsView != null) return;
            HareAudio.Play("sfx_click");
            creditsView = CreditsView.Build(guiLayer, room.Width, room.Height);
        }

        void CloseCredits()
        {
            if (creditsView == null) return;
            creditsView.RemoveFromHierarchy();
            creditsView = null;
        }

        /// <summary>The outline stack's last label, the text drawn over the outline.</summary>
        static Label TopLabel(VisualElement outlineText) => outlineText[outlineText.childCount - 1] as Label;

        /// <summary>
        /// obj_yesno_dialog's Draw GUI: <c>draw_set_alpha(0.7); draw_self();</c> (spr_tip_box, origin top left), then at
        /// alpha 1, centred, size-1 outline: <c>text</c> in fnt_score at <c>(x + width/2, y + margin)</c> and
        /// <c>message</c> in fnt_point at <c>(x + width/2, y + height/2)</c>; then its Yes and No (depth -101) over it.
        /// </summary>
        void BuildDialog(TitleDialog dialog)
        {
            RemoveDialog();
            var d = dialog.Definition;
            dialogView = new VisualElement { name = d.Object, pickingMode = PickingMode.Ignore };
            dialogView.style.position = Position.Absolute;
            dialogView.style.left = 0;
            dialogView.style.top = 0;
            dialogView.style.width = room.Width;
            dialogView.style.height = room.Height;
            guiLayer.Add(dialogView);

            var box = new VisualElement { name = d.Sprite, pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = (float)dialog.X;
            box.style.top = (float)dialog.Y;
            box.style.width = d.Width;
            box.style.height = d.Height;
            box.style.opacity = d.Alpha;
            var sprite = manifest != null ? manifest.Frame(d.Sprite) : null;
            if (sprite != null) box.style.backgroundImage = new StyleBackground(sprite);
            else Debug.LogWarning("[Unity.Hare] " + d.Sprite + " is not imported.");
            dialogView.Add(box);

            Centred(dialog.Text, HareTextStyles.MenuTitle, dialog.X + d.Width / 2d, dialog.Y + d.Margin, d.Outline, "text");
            Centred(dialog.Message, HareTextStyles.MenuText, dialog.X + d.Width / 2d, dialog.Y + d.Height / 2d, d.Outline, "message");
            AddButton(dialog.Yes, dialogView);
            AddButton(dialog.No, dialogView);
        }

        void Centred(string text, HareTextStyles.Style style, double x, double y, int outline, string name)
        {
            var element = HareUi.OutlineText(text, outline, style.UssClass, dialogView);
            element.name = name;
            element.style.position = Position.Absolute;
            element.style.left = (float)x;
            element.style.top = (float)y;
            element.style.translate = new Translate(Length.Percent(-50), 0);
            element.style.unityTextAlign = TextAnchor.UpperCenter;
        }

        void RemoveDialog()
        {
            if (dialogView == null) return;
            var gone = new List<TitleButton>();
            foreach (var pair in views)
                if (pair.Key.InDialog) gone.Add(pair.Key);
            foreach (var button in gone) views.Remove(button);
            dialogView.RemoveFromHierarchy();
            dialogView = null;
        }

        /// <summary>
        /// Every frame: each caption's colour and alpha (obj_logo's image_alpha for the menu, 1 for the dialog's), and
        /// obj_fade_title's black over the Draw-event text: a pixel the text covers at alpha a ends up
        /// <c>(text * a + under * (1 - a)) * (1 - fade)</c>, which is the text's colour times (1 - fade) drawn over the
        /// camera's already darkened picture.
        /// </summary>
        void LateUpdate()
        {
            DrawStand();
            float fade = director != null && director.InRoom ? director.Flow.FadeOpacity : 0f;
            float menuAlpha = (float)Math.Max(0d, Math.Min(1d, State.MenuAlpha));
            foreach (var pair in views)
            {
                var button = pair.Key;
                var view = pair.Value;
                float dim = button.InDialog ? 1f : 1f - fade;
                var color = button.Hovered ? hoverColor : normalColor;
                if (view.Text != null) view.Text.style.color = new Color(color.r * dim, color.g * dim, color.b * dim, 1f);
                view.Caption.style.opacity = button.InDialog ? 1f : menuAlpha;
                if (view.MessageText != null) view.MessageText.style.color = new Color(dim, dim, dim, 1f);
            }
        }

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

        /// <summary>The room's object, as ProjectSetup puts it in the title scene.</summary>
        public static TitleRoom Create() => new GameObject(ObjectName).AddComponent<TitleRoom>();
    }
}
