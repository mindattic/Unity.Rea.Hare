using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Rooms
{
    /// <summary>
    /// obj_startup, played (U.HARE-T51): the startup room's view of Core's <see cref="StartupScreen"/>. ProjectSetup puts
    /// one in the startup scene; on Awake it builds the room's five tiled backgrounds (startup.room.gmx's, from the
    /// imported art) and the note (draw_text_outline_color in fnt_point, red on black, centred at room_height - 60) in
    /// code, so nothing is wired in the Inspector. It steps the screen on the room flow's clock
    /// (<see cref="RoomFlowDirector.RoomStepped"/>, room_speed 60), draws each background where the screen has scrolled
    /// it, and plays what the screen raises: <see cref="FullscreenRequested"/> through the director's fullscreen seam and
    /// <see cref="ContinueRequested"/> as the flow's <see cref="GoToRoom"/>. Any key press (the Input System's any key, F1
    /// included) and the left mouse button while held send <see cref="ContinueFromStartup"/>. It decides no rule itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartupRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object, after the GML object.</summary>
        public const string ObjectName = "obj_startup";

        readonly List<SpriteRenderer> layers = new List<SpriteRenderer>();
        readonly List<Vector2Int> layerSizes = new List<Vector2Int>();
        RoomDefinition room;
        RoomFlowDirector director;
        PanelSettings panelSettings;
        InputAction anyKey, leftButton;

        /// <summary>The room's rules and state.</summary>
        public StartupScreen State { get; private set; }

        /// <summary>The backgrounds' renderers, back to front, one per <see cref="StartupScreen.Layers"/>.</summary>
        public IReadOnlyList<SpriteRenderer> Layers => layers;

        /// <summary>The note's outline-text stack (<see cref="HareUi.OutlineTextColor"/>).</summary>
        public VisualElement Note { get; private set; }

        /// <summary>Every event the screen raised, after it was played.</summary>
        public event Action<StartupEvent> EventPlayed;

        void Awake()
        {
            var content = GameContent.Current;
            room = content.Rooms.Get(RoomId.Startup);
            State = new StartupScreen(content.Startup, room.StepsPerSecond, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks)));
            BuildBackgrounds();
            BuildNote();

            // GML: the key-press event for vk_anykey, and the global left button event (every step it is held).
            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            leftButton = new InputAction("LeftButton", InputActionType.Button, "<Mouse>/leftButton");
        }

        void OnEnable()
        {
            anyKey?.Enable();
            leftButton?.Enable();
        }

        void OnDisable()
        {
            anyKey?.Disable();
            leftButton?.Disable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null) director.RoomStepped += OnRoomStepped;
            Play(State.Execute(new CreateStartup(RoomFlowDirector.Seat)));
            PlaceLayers();
        }

        void OnDestroy()
        {
            if (director != null) director.RoomStepped -= OnRoomStepped;
            anyKey?.Dispose();
            leftButton?.Dispose();
            if (panelSettings != null) Destroy(panelSettings);
        }

        void Update()
        {
            if (State.Continued) return;
            if (anyKey.WasPressedThisFrame() || leftButton.IsPressed())
                Play(State.Execute(new ContinueFromStartup(RoomFlowDirector.Seat)));
        }

        void LateUpdate() => PlaceLayers();

        void OnRoomStepped(RoomId stepped)
        {
            if (stepped == RoomId.Startup) Play(State.Execute(new StepStartup(RoomFlowDirector.Seat)));
        }

        void Play(StartupResult result)
        {
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case FullscreenRequested _:
                        if (director != null) director.SetFullscreen(true);
                        else Screen.fullScreen = true;
                        break;
                    case ContinueRequested request:
                        if (director != null) director.Execute(new GoToRoom(RoomFlowDirector.Seat, request.Room));
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        // ---- The backgrounds ----

        /// <summary>
        /// The room's top-left corner in the world: the room scene's camera looks at the world origin, so the room is
        /// centred on it, and GameMaker's y grows downward from that corner.
        /// </summary>
        public Vector2 RoomOrigin => new Vector2(-GameClock.Px(room.Width) / 2f, GameClock.Px(room.Height) / 2f);

        /// <summary>
        /// One tiled sprite per background, on the backdrop sorting layer in the room's order. Each is drawn one image
        /// wider and taller than the room, so wherever the scroll has left it, its tiles cover the whole room.
        /// </summary>
        void BuildBackgrounds()
        {
            var manifest = HareArtManifest.Load();
            for (int i = 0; i < State.Layers.Count; i++)
            {
                var layer = State.Layers[i];
                var go = new GameObject(layer.Name);
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = HareSortingLayers.Backdrop;
                renderer.sortingOrder = i;

                var background = manifest != null ? manifest.FindBackground(layer.Name) : null;
                var size = background != null ? new Vector2Int(background.width, background.height) : new Vector2Int(room.Width, room.Height);
                if (background == null) Debug.LogWarning("[Unity.Rea.Hare] obj_startup: background " + layer.Name + " is not imported.");
                else renderer.sprite = background.sprite;

                int columns = Mathf.CeilToInt(room.Width / (float)size.x) + 1;
                int rows = Mathf.CeilToInt(room.Height / (float)size.y) + 1;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(GameClock.Px(columns * size.x), GameClock.Px(rows * size.y));
                layers.Add(renderer);
                layerSizes.Add(size);
            }
        }

        /// <summary>
        /// Moves each background to where GameMaker would draw its first tile: background_x mod the image width, one
        /// image back, so the tiles after it reach past the room's far edge (the sprites pivot on their top left). Runs
        /// every LateUpdate.
        /// </summary>
        public void PlaceLayers()
        {
            var origin = RoomOrigin;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = State.Layers[i];
                var size = layerSizes[i];
                int x = StartupLayer.Wrap(layer.X, size.x) - size.x;
                int y = StartupLayer.Wrap(layer.Y, size.y) - size.y;
                layers[i].transform.position = new Vector3(origin.x + GameClock.Px(x), origin.y - GameClock.Px(y), 0f);
            }
        }

        /// <summary>The GML pixel the tile at <paramref name="index"/> starts on, for the tests: (x, y) in room pixels.</summary>
        public Vector2Int TileStart(int index)
        {
            var origin = RoomOrigin;
            var p = layers[index].transform.position;
            return new Vector2Int(Mathf.RoundToInt((p.x - origin.x) * GameClock.PixelsPerUnit), Mathf.RoundToInt((origin.y - p.y) * GameClock.PixelsPerUnit));
        }

        // ---- The note ----

        /// <summary>
        /// The Draw event: <c>draw_set_halign(fa_center); draw_set_font(fnt_point);
        /// draw_text_outline_color(room_width / 2, room_height - 60, 2, strText, c_red, c_black)</c>, each line centred
        /// on the room's middle with its top 60 px above the room's bottom.
        /// </summary>
        void BuildNote()
        {
            var tuning = State.Tuning;
            var host = new GameObject("Note");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            var stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();

            Note = HareUi.OutlineTextColor(tuning.Note, tuning.NoteOutline, ParseColor(tuning.NoteColor, Color.red), ParseColor(tuning.NoteOutlineColor, Color.black), parent: stage);
            Note.name = "startup-note";
            HareTextStyles.Apply(Note, HareTextStyles.MenuText);
            Note.style.position = Position.Absolute;
            Note.style.left = 0;
            Note.style.width = room.Width;
            Note.style.top = room.Height - tuning.NoteBottomMargin;
            Note.style.unityTextAlign = TextAnchor.UpperCenter;
        }

        static Color ParseColor(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out var color) ? color : fallback;

        /// <summary>The room's object, as ProjectSetup puts it in the startup scene.</summary>
        public static StartupRoom Create() => new GameObject(ObjectName).AddComponent<StartupRoom>();
    }
}
