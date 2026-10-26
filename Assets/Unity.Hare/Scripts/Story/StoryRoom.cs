using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Audio;
using Unity.Hare.Content;
using Unity.Hare.Effects;
using Unity.Hare.Flow;
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Story
{
    /// <summary>
    /// room_story played (U.HARE-T28): obj_story, obj_typewriter_story and obj_scroll_world, the story room's view of
    /// Core's <see cref="StoryScreen"/>. ProjectSetup puts one in the story scene; it builds bg_nightsky (tiled, on the
    /// backdrop layer), the earth (spr_earthball at depth 0, turning on its origin) and a 1920x1080 GUI panel for the
    /// typewriter (fnt_medium, <see cref="HareTextStyles.StoryTypewriter"/>, size-1 outline at (30, 60)) in code. It steps
    /// the screen on the room flow's clock (<see cref="RoomFlowDirector.RoomStepped"/>), sends each key press as
    /// <see cref="PressStoryKey"/> (with whether Esc is held), and plays what comes back: sfx_type2 restarted for each
    /// typed character, the campaign through the session and obj_fade_story's fade to room_map through the flow. Esc to
    /// room_title is the room flow's (the rooms table's EscapeToTitle). It decides no rule itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object, after the GML object.</summary>
        public const string ObjectName = "obj_story";

        RoomDefinition room;
        RoomFlowDirector director;
        PanelSettings panelSettings;
        InputAction anyKey, escape;
        Vector2Int backgroundSize;
        string drawn;

        /// <summary>The room's rules and state.</summary>
        public StoryScreen State { get; private set; }

        /// <summary>bg_nightsky, tiled.</summary>
        public SpriteRenderer Background { get; private set; }

        /// <summary>obj_scroll_world's spr_earthball.</summary>
        public SpriteRenderer Earth { get; private set; }

        /// <summary>The panel's 1920x1080 stage, the GUI layer the typewriter draws on.</summary>
        public VisualElement Stage { get; private set; }

        /// <summary>obj_typewriter_story's text as drawn.</summary>
        public VisualElement TypewriterText { get; private set; }

        /// <summary>Every event the screen raised, after it was played.</summary>
        public event Action<StoryEvent> EventPlayed;

        void Awake()
        {
            var content = GameContent.Current;
            room = content.Rooms.Get(RoomId.Story);
            State = new StoryScreen(content.Story, content.Dialogue.Script(content.Story.Script), room.Width, room.Height);
            BuildWorld(content.Story);
            BuildPanel(content.Story);
            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            escape = new InputAction("Escape", InputActionType.Button, "<Keyboard>/escape");
        }

        void OnEnable()
        {
            anyKey?.Enable();
            escape?.Enable();
        }

        void OnDisable()
        {
            anyKey?.Disable();
            escape?.Disable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null) director.RoomStepped += OnRoomStepped;
            Play(State.Execute(new CreateStory(RoomFlowDirector.Seat)));
            Present();
        }

        void OnDestroy()
        {
            if (director != null) director.RoomStepped -= OnRoomStepped;
            anyKey?.Dispose();
            escape?.Dispose();
            if (panelSettings != null) Destroy(panelSettings);
        }

        void OnRoomStepped(RoomId stepped)
        {
            if (stepped == RoomId.Story) Play(State.Execute(new StepStory(RoomFlowDirector.Seat)));
        }

        void Update()
        {
            // obj_story's key press for any key: if (!keyboard_check(vk_escape)).
            if (State.Created && anyKey.WasPressedThisFrame())
                Play(State.Execute(new PressStoryKey(RoomFlowDirector.Seat, escape.IsPressed())));
        }

        void LateUpdate() => Present();

        void Play(StoryResult result)
        {
            var seat = RoomFlowDirector.Seat;
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case StoryTypeSounded typed:
                        HareAudio.Restart(typed.Sound);
                        break;
                    case StoryCampaignChosen _:
                        if (director != null) director.ExecuteSession(new ChooseCampaign(seat));
                        break;
                    case StoryFadeRequested fade:
                        if (director != null) director.Execute(new FadeToRoom(seat, fade.Room));
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        // ---- Drawing ----

        /// <summary>The world point of room pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public Vector2 ToWorld(double x, double y) =>
            new Vector2(GameClock.Px((float)x - room.Width / 2f), GameClock.Px(room.Height / 2f - (float)y));

        /// <summary>Draws the state where it stands: the sky's tile, the earth and the typed text.</summary>
        public void Present()
        {
            if (State == null) return;
            if (Background != null)
            {
                // GameMaker's first tile: background_y mod the image, one image back.
                int y = StartupLayer.Wrap(State.BackgroundY, backgroundSize.y) - backgroundSize.y;
                int x = -backgroundSize.x;
                Background.transform.position = new Vector3(GameClock.Px(x - room.Width / 2f), GameClock.Px(room.Height / 2f - y), 0f);
            }
            if (Earth != null)
            {
                var at = ToWorld(State.EarthX, State.EarthY);
                Earth.transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, (float)State.EarthAngle));
            }
            string text = State.Typewriter.DrawText;
            if (text != drawn)
            {
                drawn = text;
                HareUi.SetOutlineText(TypewriterText, text);
            }
        }

        /// <summary>The room pixel the sky's tile starts on (tests).</summary>
        public Vector2Int BackgroundTileStart()
        {
            var p = Background.transform.position;
            return new Vector2Int(Mathf.RoundToInt(p.x * GameClock.PixelsPerUnit + room.Width / 2f), Mathf.RoundToInt(room.Height / 2f - p.y * GameClock.PixelsPerUnit));
        }

        void BuildWorld(StoryTuning tuning)
        {
            var manifest = HareArtManifest.Load();

            var sky = new GameObject(tuning.Background);
            sky.transform.SetParent(transform, false);
            Background = sky.AddComponent<SpriteRenderer>();
            Background.sortingLayerName = HareSortingLayers.Backdrop;
            var background = manifest != null ? manifest.FindBackground(tuning.Background) : null;
            backgroundSize = background != null ? new Vector2Int(background.width, background.height) : new Vector2Int(room.Width, room.Height);
            if (background == null) Debug.LogWarning("[Unity.Hare] background " + tuning.Background + " is not imported.");
            else Background.sprite = background.sprite;
            int columns = Mathf.CeilToInt(room.Width / (float)backgroundSize.x) + 1;
            int rows = Mathf.CeilToInt(room.Height / (float)backgroundSize.y) + 1;
            Background.drawMode = SpriteDrawMode.Tiled;
            Background.tileMode = SpriteTileMode.Continuous;
            Background.size = new Vector2(GameClock.Px(columns * backgroundSize.x), GameClock.Px(rows * backgroundSize.y));

            var earth = new GameObject("obj_scroll_world");
            earth.transform.SetParent(transform, false);
            Earth = earth.AddComponent<SpriteRenderer>();
            Earth.sortingLayerName = HareSortingLayers.Default;
            Earth.sortingOrder = HareArt.SortingOrderForDepth(0);
            Earth.sprite = manifest != null ? manifest.Frame(tuning.EarthSprite) : null;
            if (Earth.sprite == null) Debug.LogWarning("[Unity.Hare] " + tuning.EarthSprite + " is not imported.");
        }

        void BuildPanel(StoryTuning tuning)
        {
            var host = new GameObject("Gui");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            Stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();

            // obj_typewriter_story's Draw GUI: fa_left, c_white, fnt_medium, draw_text_outline_alpha(x, y, 1, output, alpha).
            var t = tuning.Typewriter;
            TypewriterText = HareUi.OutlineTextAlpha("", t.Outline, (float)State.Typewriter.DrawAlpha, HareTextStyles.StoryTypewriter.UssClass, Stage);
            TypewriterText.name = t.Object;
            TypewriterText.style.position = Position.Absolute;
            TypewriterText.style.left = t.X;
            TypewriterText.style.top = t.Y;
            TypewriterText.style.unityTextAlign = TextAnchor.UpperLeft;
        }

        /// <summary>The room's object, as ProjectSetup puts it in the story scene.</summary>
        public static StoryRoom Create() => new GameObject(ObjectName).AddComponent<StoryRoom>();
    }
}
