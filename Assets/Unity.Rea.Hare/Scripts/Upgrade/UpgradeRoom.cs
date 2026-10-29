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
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Upgrade
{
    /// <summary>
    /// room_upgrade played (U.HARE-T64): obj_upgrade's way around the garage, the room's view of Core's
    /// <see cref="UpgradeScreen"/>. ProjectSetup puts one in the upgrade scene; it builds, in code, room_upgrade's bg_metal
    /// (tiled, sliding left a pixel a step), obj_btn_map (depth -5: spr_button_area_small, "Observation Deck" white or
    /// cornflower blue, its message under the mouse, dimmed with the Draw event by obj_fade_upgrade) and obj_mouse's
    /// spr_pointer over everything. It sends the mouse and the left button as commands and plays what comes back: the
    /// button's sounds and the fade back to room_map. U.HARE-T24 adds what the garage sells: the weapon icons, the row
    /// labels, the pips (spr_upgrade_circle_empty, filled once bought), the icons' and pips' hover messages and
    /// obj_upgrade's Draw GUI "Skill Points x" line; a pip bought is saved to the campaign at once. It decides no rule
    /// itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object, after the GML object.</summary>
        public const string ObjectName = "obj_upgrade";

        RoomDefinition room;
        RoomFlowDirector director;
        PanelSettings panelSettings;
        HareControls controls;
        InputAction leftButton;
        HareArtManifest manifest;
        Vector2Int backgroundSize;
        Color normalColor, hoverColor;
        VisualElement drawLayer, pointer, buttonSprite;
        Label buttonText, messageText;
        Sprite pipEmpty, pipFilled;
        readonly Dictionary<string, VisualElement> pips = new Dictionary<string, VisualElement>();
        readonly List<VisualElement> dimmedImages = new List<VisualElement>();
        readonly List<Label> dimmedTexts = new List<Label>();

        /// <summary>The room's rules and state.</summary>
        public UpgradeScreen State { get; private set; }

        /// <summary>bg_metal, tiled.</summary>
        public SpriteRenderer Background { get; private set; }

        /// <summary>The panel's 1920x1080 stage.</summary>
        public VisualElement Stage { get; private set; }

        /// <summary>obj_btn_map's caption and its hover message.</summary>
        public VisualElement MapCaption { get; private set; }
        public VisualElement MapMessage { get; private set; }

        /// <summary>The message of the icon or pip under the mouse (hidden while none is).</summary>
        public VisualElement ItemMessage { get; private set; }

        /// <summary>obj_upgrade's Draw GUI "Skill Points x" line.</summary>
        public VisualElement SkillPointsText { get; private set; }

        /// <summary>The pips, by upgrade id.</summary>
        public IReadOnlyDictionary<string, VisualElement> Pips => pips;

        /// <summary>obj_mouse, which obj_upgrade's Create makes.</summary>
        public MouseView Pointer { get; private set; }

        /// <summary>Every event the screen raised, after it was played.</summary>
        public event Action<UpgradeEvent> EventPlayed;

        void Awake()
        {
            var content = GameContent.Current;
            room = content.Rooms.Get(RoomId.Upgrade);
            manifest = HareArtManifest.Load();
            var screen = content.Upgrades.Screen;
            normalColor = ParseColor(screen.MapButton.Color, Color.white);
            hoverColor = ParseColor(screen.MapButton.HoverColor, new Color(100f / 255f, 149f / 255f, 237f / 255f));
            // The garage sells into the running campaign (the director's, saved in campaign.json); with no room flow
            // (a scene opened on its own) it sells into a scratch one.
            var flow = RoomFlowDirector.Active;
            var progress = flow != null && flow.Campaign != null ? flow.Campaign : new CampaignProgress();
            int starting = flow != null && flow.Session != null ? flow.Session.Tuning.StartingSkillPoints : content.Session.StartingSkillPoints;
            State = new UpgradeScreen(content.Upgrades, progress, starting);

            BuildBackground(screen);
            BuildPanel(screen);

            // obj_upgrade's Create: mouse = instance_create(0,0,obj_mouse); mouse.sprite_index = spr_pointer.
            Pointer = MouseView.Create(RoomId.Upgrade, gameObject.scene, transform);
            if (Pointer != null) Pointer.DrawOnUi = DrawPointer;

            controls = new HareControls();
            leftButton = new InputAction("LeftButton", InputActionType.Button, "<Mouse>/leftButton");
        }

        void OnEnable()
        {
            if (controls == null) return;
            controls.Player.Aim.Enable();
            leftButton.Enable();
        }

        void OnDisable()
        {
            if (controls == null) return;
            controls.Player.Aim.Disable();
            leftButton.Disable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null) director.RoomStepped += OnRoomStepped;
            Play(State.Execute(new CreateUpgrade(RoomFlowDirector.Seat)));
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
            leftButton?.Dispose();
            if (panelSettings != null) Destroy(panelSettings);
        }

        void OnRoomStepped(RoomId stepped)
        {
            if (stepped == RoomId.Upgrade) Step();
        }

        /// <summary>One step of the room (the flow's clock calls it; tests may too).</summary>
        public void Step()
        {
            Play(State.Execute(new StepUpgrade(RoomFlowDirector.Seat)));
            Present();
        }

        void Update()
        {
            if (!State.Created) return;
            var seat = RoomFlowDirector.Seat;
            // obj_btn_map's mouse enter, leave and left pressed events.
            if (TryReadMouse(out var mouse))
            {
                Play(State.Execute(new PointUpgrade(seat, mouse.x, mouse.y)));
                if (leftButton.WasPressedThisFrame()) Play(State.Execute(new PressUpgrade(seat, mouse.x, mouse.y)));
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
            roomPixel = new Vector2(world.x * GameClock.PixelsPerUnit + room.Width / 2f, room.Height / 2f - world.y * GameClock.PixelsPerUnit);
            return true;
        }

        /// <summary>Plays what a command raised: the sounds, the hover message and the fade to room_map.</summary>
        public void Play(UpgradeResult result)
        {
            var seat = RoomFlowDirector.Seat;
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case UpgradeSoundRequested sound:
                        HareAudio.Play(sound.Sound);
                        break;
                    case UpgradeHoverChanged hover:
                        MapMessage.style.display = hover.Hovered ? DisplayStyle.Flex : DisplayStyle.None;
                        break;
                    case UpgradeMessageChanged message:
                        if (!string.IsNullOrEmpty(message.Message)) HareUi.SetOutlineText(ItemMessage, message.Message);
                        ItemMessage.style.display = string.IsNullOrEmpty(message.Message) ? DisplayStyle.None : DisplayStyle.Flex;
                        break;
                    case UpgradeBought _:
                        // What obj_upgrade_*'s "//Write to Ini here" meant: the purchase is kept at once.
                        if (director != null) director.SaveCampaign();
                        else CampaignStore.Save(State.Progress);
                        RefreshGarage();
                        break;
                    case UpgradeFadeRequested fade:
                        if (director != null) director.Execute(new FadeToRoom(seat, fade.Room));
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        /// <summary>Draws the state where it stands: bg_metal's first tile.</summary>
        public void Present()
        {
            if (State == null || Background == null) return;
            // GameMaker's first tile: background_x mod the image, one image back.
            int x = StartupLayer.Wrap(State.BackgroundX, backgroundSize.x) - backgroundSize.x;
            int y = -backgroundSize.y;
            Background.transform.position = new Vector3(GameClock.Px(x - room.Width / 2f), GameClock.Px(room.Height / 2f - y), 0f);
        }

        /// <summary>
        /// Every frame: obj_btn_map's colour, and obj_fade_upgrade's black over the Draw-event button (its sprite and
        /// caption darkened by the fade, as the map's Upgrades button is).
        /// </summary>
        void LateUpdate()
        {
            Present();
            float fade = director != null && director.InRoom ? director.Flow.FadeOpacity : 0f;
            float dim = 1f - fade;
            var color = State.MapHovered ? hoverColor : normalColor;
            if (buttonText != null) buttonText.style.color = new Color(color.r * dim, color.g * dim, color.b * dim, 1f);
            if (buttonSprite != null) buttonSprite.style.unityBackgroundImageTintColor = new Color(dim, dim, dim, 1f);
            if (messageText != null) messageText.style.color = new Color(dim, dim, dim, 1f);
            // The icons, pips, labels and their message are Draw-event objects too; the Skill Points line is Draw GUI,
            // over the fade.
            var tint = new Color(dim, dim, dim, 1f);
            foreach (var image in dimmedImages) image.style.unityBackgroundImageTintColor = tint;
            foreach (var text in dimmedTexts) text.style.color = tint;
        }

        /// <summary>Each pip's sprite (filled once bought) and the Skill Points line, from the state.</summary>
        public void RefreshGarage()
        {
            foreach (var pair in pips)
            {
                var frame = State.Owns(pair.Key) ? pipFilled : pipEmpty;
                if (frame != null) pair.Value.style.backgroundImage = new StyleBackground(frame);
            }
            if (SkillPointsText != null)
                HareUi.SetOutlineText(SkillPointsText, State.Tuning.SkillPointsText + State.SkillPoints);
        }

        /// <summary>A Draw-event sprite at its top-left origin, darkened with the fade.</summary>
        VisualElement AddSprite(string name, string sprite, int x, int y, int width, int height)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
            var frame = manifest != null && !string.IsNullOrEmpty(sprite) ? manifest.Frame(sprite) : null;
            if (frame != null) element.style.backgroundImage = new StyleBackground(frame);
            else if (!string.IsNullOrEmpty(sprite)) Debug.LogWarning("[Unity.Hare] " + sprite + " is not imported.");
            drawLayer.Add(element);
            dimmedImages.Add(element);
            return element;
        }

        /// <summary>
        /// What the garage sells: obj_btn_upgrade_vulcan/shotgun and obj_upgrade_missle (draw_self), the obj_label row
        /// captions (fa_left, fnt_point, at (x, y-10)), the pips, and the icons' and pips' shared message line.
        /// </summary>
        void BuildGarage(UpgradeScreenTuning screen)
        {
            pipEmpty = manifest != null ? manifest.Frame(screen.PipEmptySprite) : null;
            pipFilled = manifest != null ? manifest.Frame(screen.PipFilledSprite) : null;
            if (pipEmpty == null) Debug.LogWarning("[Unity.Hare] " + screen.PipEmptySprite + " is not imported.");
            if (pipFilled == null) Debug.LogWarning("[Unity.Hare] " + screen.PipFilledSprite + " is not imported.");

            foreach (var icon in screen.Icons)
                if (icon != null) AddSprite(icon.Object, icon.Sprite, icon.X, icon.Y, screen.SpriteWidth, screen.SpriteHeight);

            foreach (var label in screen.Labels)
            {
                if (label == null) continue;
                var text = HareUi.OutlineText(label.Text, screen.Outline, HareTextStyles.MenuText.UssClass, drawLayer);
                text.name = "obj_label-" + label.Text;
                text.style.position = Position.Absolute;
                text.style.left = label.X;
                text.style.top = label.Y + screen.LabelOffset;
                text.style.unityTextAlign = TextAnchor.UpperLeft;
                dimmedTexts.Add(TopLabel(text));
            }

            foreach (var pip in State.Catalog.Upgrades)
            {
                if (pip == null || pips.ContainsKey(pip.Id)) continue;
                pips[pip.Id] = AddSprite(pip.Object + "-" + pip.Id, null, pip.X, pip.Y, screen.SpriteWidth, screen.SpriteHeight);
            }

            ItemMessage = HareUi.OutlineText(" ", screen.Outline, HareTextStyles.MenuText.UssClass, drawLayer);
            ItemMessage.name = "upgrade-message";
            ItemMessage.style.position = Position.Absolute;
            ItemMessage.style.left = 0;
            ItemMessage.style.width = room.Width;
            ItemMessage.style.top = room.Height - screen.MessageBottomMargin;
            ItemMessage.style.unityTextAlign = TextAnchor.UpperCenter;
            ItemMessage.style.display = DisplayStyle.None;
            dimmedTexts.Add(TopLabel(ItemMessage));
        }

        /// <summary>obj_upgrade's Draw GUI: "Skill Points x" + SKILLPOINTS at (room_width - 200, room_height - 60), fa_center, fnt_score.</summary>
        void BuildSkillPoints(UpgradeScreenTuning screen)
        {
            SkillPointsText = HareUi.OutlineText(screen.SkillPointsText + State.SkillPoints, screen.Outline, HareTextStyles.MenuTitle.UssClass, Stage);
            SkillPointsText.name = "skill-points";
            SkillPointsText.style.position = Position.Absolute;
            SkillPointsText.style.left = room.Width - screen.SkillPointsRightMargin;
            SkillPointsText.style.top = room.Height - screen.SkillPointsBottomMargin;
            SkillPointsText.style.translate = new Translate(Length.Percent(-50), 0);
            SkillPointsText.style.unityTextAlign = TextAnchor.UpperCenter;
        }

        void BuildBackground(UpgradeScreenTuning screen)
        {
            var go = new GameObject(screen.Background);
            go.transform.SetParent(transform, false);
            Background = go.AddComponent<SpriteRenderer>();
            Background.sortingLayerName = HareSortingLayers.Backdrop;
            var background = manifest != null ? manifest.FindBackground(screen.Background) : null;
            backgroundSize = background != null ? new Vector2Int(background.width, background.height) : new Vector2Int(room.Width, room.Height);
            if (background == null) Debug.LogWarning("[Unity.Hare] background " + screen.Background + " is not imported.");
            else Background.sprite = background.sprite;
            int columns = Mathf.CeilToInt(room.Width / (float)backgroundSize.x) + 2;
            int rows = Mathf.CeilToInt(room.Height / (float)backgroundSize.y) + 2;
            Background.drawMode = SpriteDrawMode.Tiled;
            Background.tileMode = SpriteTileMode.Continuous;
            Background.size = new Vector2(GameClock.Px(columns * backgroundSize.x), GameClock.Px(rows * backgroundSize.y));
        }

        void BuildPanel(UpgradeScreenTuning screen)
        {
            var host = new GameObject("Gui");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            Stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();

            drawLayer = new VisualElement { name = "draw", pickingMode = PickingMode.Ignore };
            drawLayer.style.position = Position.Absolute;
            drawLayer.style.left = 0;
            drawLayer.style.top = 0;
            drawLayer.style.width = room.Width;
            drawLayer.style.height = room.Height;
            Stage.Add(drawLayer);

            // The garage's depth-0 objects under obj_btn_map's depth -5.
            BuildGarage(screen);

            // obj_btn_map's Draw: draw_self (spr_button_area_small at its origin), "Observation Deck" at (x, y-10),
            // fa_center, fnt_point, size-2 outline; its message at (room_width / 2, room_height - 30) while hovered.
            var b = screen.MapButton;
            buttonSprite = new VisualElement { name = b.Sprite, pickingMode = PickingMode.Ignore };
            buttonSprite.style.position = Position.Absolute;
            buttonSprite.style.left = b.X - b.XOrigin;
            buttonSprite.style.top = b.Y - b.YOrigin;
            buttonSprite.style.width = b.Width;
            buttonSprite.style.height = b.Height;
            var frame = manifest != null ? manifest.Frame(b.Sprite) : null;
            if (frame != null) buttonSprite.style.backgroundImage = new StyleBackground(frame);
            else Debug.LogWarning("[Unity.Hare] " + b.Sprite + " is not imported.");
            drawLayer.Add(buttonSprite);

            MapCaption = HareUi.OutlineText(b.Label, b.Outline, HareTextStyles.MenuText.UssClass, drawLayer);
            MapCaption.name = b.Object;
            MapCaption.style.position = Position.Absolute;
            MapCaption.style.left = b.X;
            MapCaption.style.top = b.Y + b.LabelOffset;
            MapCaption.style.translate = new Translate(Length.Percent(-50), 0);
            MapCaption.style.unityTextAlign = TextAnchor.UpperCenter;
            buttonText = TopLabel(MapCaption);

            MapMessage = HareUi.OutlineText(b.Message, b.Outline, HareTextStyles.MenuText.UssClass, drawLayer);
            MapMessage.name = b.Object + "-message";
            MapMessage.style.position = Position.Absolute;
            MapMessage.style.left = 0;
            MapMessage.style.width = room.Width;
            MapMessage.style.top = room.Height - b.MessageBottomMargin;
            MapMessage.style.unityTextAlign = TextAnchor.UpperCenter;
            MapMessage.style.display = DisplayStyle.None;
            messageText = TopLabel(MapMessage);

            BuildSkillPoints(screen);
            RefreshGarage();

            pointer =new VisualElement { name = MouseView.ObjectName, pickingMode = PickingMode.Ignore };
            pointer.style.position = Position.Absolute;
            pointer.style.display = DisplayStyle.None;
            Stage.Add(pointer);
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

        /// <summary>The room's object, as ProjectSetup puts it in the upgrade scene.</summary>
        public static UpgradeRoom Create() => new GameObject(ObjectName).AddComponent<UpgradeRoom>();
    }
}
