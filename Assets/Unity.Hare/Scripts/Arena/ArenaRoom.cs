using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Content;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Player;
using Unity.Hare.Rendering;
using Unity.Hare.Rocks;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// The arena, played (U.HARE-T52): the play rooms' view of Core's <see cref="ArenaWorld"/>, as obj_game, the room's
    /// views and backgrounds and obj_player's boundary rules make it. ProjectSetup puts one (named obj_game, after the
    /// room's controller) in room_gamescreen, room_sidescroll and room_shaft; on Awake it builds, in code, the level's
    /// background layers from the imported art, view 1 (the minimap camera, drawn into its 300x200 port) and the
    /// 1920x1080 HUD overlay the HUD objects draw on, so nothing is wired in the Inspector.
    ///
    /// Every room step (the room flow's clock, <see cref="RoomFlowDirector.RoomStepped"/> at room_speed 120; its own when
    /// the room runs without the flow) it sends the arena, in GameMaker's order, obj_player's boundary check, the views'
    /// follow and the backgrounds' scroll, and plays what comes back: a wrap moves the ship (and, with randomize_on_loop,
    /// random_positions scatters the rocks and enemies), the buffer zone costs a life through the ship's
    /// <see cref="PlayerHealth.LoseLifeOutOfBounds"/>, from the session's LIVES under the room flow (reset_player flies it
    /// back in from <see cref="SpawnPoint"/>; with none left the flow runs game_restart). The M
    /// key (HareControls' Meta/Map) toggles the minimap. Each frame the room camera is put on view 0, the minimap camera on
    /// view 1, and the layers where the arena has scrolled them. It decides no rule itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaRoom : MonoBehaviour
    {
        /// <summary>The name ProjectSetup gives the room's object: the room's controller in the original.</summary>
        public const string ObjectName = "obj_game";

        readonly List<SpriteRenderer> layers = new List<SpriteRenderer>();
        readonly List<Vector2Int> layerSizes = new List<Vector2Int>();
        RoomFlowDirector director;
        HareControls controls;
        PanelSettings panelSettings;
        Transform layerRoot;
        double stepAccumulator;
        RoomDefinition roomDefinition;
        ContentRegistry content;
        PlayerHealth health;

        /// <summary>The room's rules and state.</summary>
        public ArenaWorld State { get; private set; }

        /// <summary>The room this arena is.</summary>
        public RoomId Room { get; private set; }

        /// <summary>The background slots' renderers, back to front, one per <see cref="ArenaWorld.Layers"/> (null for an empty slot).</summary>
        public IReadOnlyList<SpriteRenderer> Layers => layers;

        /// <summary>View 0's camera: the room camera, tagged MainCamera.</summary>
        public Camera ViewCamera { get; private set; }

        /// <summary>View 1's camera, the minimap, enabled while show_map is set.</summary>
        public Camera MinimapCamera { get; private set; }

        /// <summary>The 1920x1080 GUI layer (obj_game's and the HUD objects' Draw GUI events draw here).</summary>
        public VisualElement Hud { get; private set; }

        /// <summary>The ship the room follows (GML: player).</summary>
        public PlayerShipController Player { get; private set; }

        /// <summary>obj_player's Draw: its arrows and leap line.</summary>
        public PlayerArrows Arrows { get; private set; }

        /// <summary>Where reset_player puts the ship: <c>(room_width / 2, -sprite_height)</c>.</summary>
        public Transform SpawnPoint { get; private set; }

        /// <summary>Every event the arena raised, after it was played.</summary>
        public event Action<ArenaEvent> EventPlayed;

        /// <summary>The seat local play acts for.</summary>
        static PlayerId Seat => RoomFlowDirector.Seat;

        void Awake()
        {
            content = GameContent.Current;
            Room = RoomList.TryParse(gameObject.scene.name, out var room) && content.Arena.RoomFor(room) != null ? room : RoomId.GameScreen;
            roomDefinition = content.Rooms.Get(Room);
            ViewCamera = FindRoomCamera();
            BuildMinimap();
            BuildHud();
            Begin(content.Session.StartingLevel);

            // GML: obj_game's key-press event for M (77).
            controls = new HareControls();
            controls.Meta.Map.Enable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null) director.RoomStepped += OnRoomStepped;
            AttachPlayer(FindPlayer());
            Play(State.Execute(new CreateArena(Seat)));
            Present();
        }

        void OnDestroy()
        {
            if (director != null) director.RoomStepped -= OnRoomStepped;
            DetachHealth();
            if (controls != null)
            {
                controls.Meta.Map.Disable();
                controls.Dispose();
                controls = null;
            }
            if (panelSettings != null) Destroy(panelSettings);
        }

        /// <summary>
        /// Sets the room up for <paramref name="level"/> (the level script's setting half: its backgrounds, their speeds and
        /// its loops; setup_level's background_x/y = 0): the setting that level plays in this room, or the first level that
        /// plays here when it plays elsewhere. The level setup (U.HARE-T57) calls this as current_level changes.
        /// </summary>
        public void Begin(int level)
        {
            var setting = content.Arena.SettingFor(level, Room);
            bool created = State != null && State.Created;
            State = new ArenaWorld(content.Arena, roomDefinition, setting, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks)));
            if (SpawnPoint == null)
            {
                SpawnPoint = new GameObject("spawnpoint").transform;
                SpawnPoint.SetParent(transform, false);
            }
            SpawnPoint.position = ToWorld(State.SpawnX, State.SpawnY);
            BuildLayers();
            Pickups.PickupDirector.ClearPickups(); // setup_level: with(par_pickup) instance_destroy() (U.HARE-T23)
            if (created) Play(State.Execute(new CreateArena(Seat)));
        }

        /// <summary>
        /// Follows <paramref name="ship"/> from now on and plays its death and respawn (U.HARE-T54): reset_player flies it
        /// in from <see cref="SpawnPoint"/>; under the room flow its LIVES are the session's (<see cref="SessionLives"/>,
        /// so a death and a flight out of the arena spend the same LIVES) and game over is the flow's game_restart; a hit
        /// puts obj_losthealth on the HUD where the lost pip was.
        /// </summary>
        public void AttachPlayer(PlayerShipController ship)
        {
            DetachHealth();
            Player = ship;
            if (ship == null) return;
            ship.InViewCheck = InView; // par_rock's landing needs in_view(other.x, other.y) (U.HARE-T53)
            health = ship.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.RespawnPoint = SpawnPoint;
                health.Damaged += OnPlayerDamaged;
                var flow = director != null ? director : RoomFlowDirector.Active;
                if (flow != null)
                {
                    health.UseLives(new SessionLives(() => flow.Session, Seat, flow.PlaySession));
                    health.GameRestart = flow.RestartGame;
                }
            }
            if (Arrows == null) Arrows = PlayerArrows.Create(this);
        }

        void DetachHealth()
        {
            if (health == null) return;
            health.Damaged -= OnPlayerDamaged;
            health = null;
        }

        /// <summary>player_hurt's obj_losthealth at <c>column2 + HP * width</c> on the GUI layer.</summary>
        void OnPlayerDamaged(int healthLeft)
        {
            if (Hud != null) LostHealthView.Show(Hud, new LostHealthPip(healthLeft, content.Arena.HudHeight, content.Health));
        }

        void Update()
        {
            if (controls != null && controls.Meta.Map.WasPressedThisFrame()) Play(State.Execute(new ToggleMinimap(Seat)));

            // A room scene loaded without the room flow (a test, a tool) still runs on its own room_speed clock.
            if (director == null)
            {
                stepAccumulator += Time.deltaTime * roomDefinition.StepsPerSecond;
                int steps = Math.Min((int)Math.Floor(stepAccumulator), content.Rooms.MaxStepsPerFrame);
                stepAccumulator -= Math.Floor(stepAccumulator);
                for (int i = 0; i < steps; i++) Step();
            }
        }

        void LateUpdate() => Present();

        void OnRoomStepped(RoomId stepped)
        {
            if (stepped == Room) Step();
        }

        /// <summary>
        /// One room step, in GameMaker's order: obj_player's Step boundary check, the views following it, then the
        /// backgrounds' scroll.
        /// </summary>
        public void Step()
        {
            if (Player != null && !PlayerIsDead())
            {
                var at = PlayerRoomPosition();
                Play(State.Execute(new CheckPlayerBounds(Seat, at.x, at.y)));
            }
            if (Player != null)
            {
                var at = PlayerRoomPosition();
                Play(State.Execute(new FollowPlayer(Seat, at.x, at.y)));
            }
            Play(State.Execute(new StepArena(Seat)));
        }

        bool PlayerIsDead()
        {
            var health = Player.GetComponent<PlayerHealth>();
            return health != null && (health.IsDead || health.IsGameOver);
        }

        void Play(ArenaResult result)
        {
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case MinimapToggled toggled:
                        if (MinimapCamera != null) MinimapCamera.enabled = toggled.Visible;
                        break;
                    case PlayerWrapped wrapped:
                        MovePlayer(ToWorld(wrapped.X, wrapped.Y));
                        if (wrapped.RandomizePositions) RandomPositions();
                        break;
                    case PlayerOutOfBounds _:
                        var health = Player != null ? Player.GetComponent<PlayerHealth>() : null;
                        if (health != null) health.LoseLifeOutOfBounds();
                        break;
                }
                EventPlayed?.Invoke(e);
            }
        }

        void MovePlayer(Vector2 world)
        {
            if (Player == null) return;
            var t = Player.transform;
            t.position = new Vector3(world.x, world.y, t.position.z);
            var body = Player.GetComponent<Rigidbody2D>();
            if (body != null) body.position = world;
        }

        /// <summary>random_positions: every rock, then every structure (none yet, U.HARE-T56), then every enemy.</summary>
        void RandomPositions()
        {
            var rocks = new List<Rock>();
            foreach (var rock in FindObjectsByType<Rock>(FindObjectsSortMode.InstanceID))
                if (rock != null && !rock.IsGone) rocks.Add(rock);
            var enemies = new List<int>();
            foreach (var body in EnemyDirector.World.Enemies)
                if (body != null && !body.IsDead) enemies.Add(body.Id);

            var result = State.Execute(new ScatterObjects(Seat, rocks.Count + enemies.Count));
            foreach (var e in result.Events)
            {
                if (e is PositionsScattered scattered)
                {
                    for (int i = 0; i < scattered.Points.Length; i++)
                    {
                        var world = ToWorld(scattered.Points[i].X, scattered.Points[i].Y);
                        if (i < rocks.Count)
                        {
                            var t = rocks[i].transform;
                            t.position = new Vector3(world.x, world.y, t.position.z);
                            var body = rocks[i].GetComponent<Rigidbody2D>();
                            if (body != null) body.position = world;
                        }
                        else EnemyDirector.Execute(new PlaceBody(enemies[i - rocks.Count], world.ToVec2()));
                    }
                }
                EventPlayed?.Invoke(e);
            }
        }

        // ---- Room pixels and the world ----

        /// <summary>
        /// A room pixel (GameMaker's: from the room's top left, y growing downward) in the world: the room is centred on
        /// the world origin, 100 px to a unit.
        /// </summary>
        public Vector2 ToWorld(float x, float y) =>
            new Vector2(GameClock.Px(x - State.Width / 2f), GameClock.Px(State.Height / 2f - y));

        /// <summary>The room pixel at world point <paramref name="world"/>.</summary>
        public Vector2 ToRoom(Vector2 world) =>
            new Vector2(world.x * GameClock.PixelsPerUnit + State.Width / 2f, State.Height / 2f - world.y * GameClock.PixelsPerUnit);

        /// <summary>GML in_view(x, y) for world point <paramref name="world"/>: strictly inside view 0.</summary>
        public bool InView(Vector2 world)
        {
            if (State == null) return true;
            var at = ToRoom(world);
            var view = State.View;
            return PerchProbe.InView(at.x, at.y, view.X, view.Y, view.Width, view.Height);
        }

        /// <summary>The ship's position in room pixels.</summary>
        public Vector2 PlayerRoomPosition() => Player != null ? ToRoom(Player.transform.position) : Vector2.zero;

        // ---- Drawing ----

        /// <summary>Puts the cameras on the views and the layers where the arena has scrolled them. Runs every LateUpdate.</summary>
        public void Present()
        {
            if (State == null) return;
            PlaceCamera(ViewCamera, State.View);
            PlaceCamera(MinimapCamera, State.Minimap);
            if (MinimapCamera != null) MinimapCamera.enabled = State.Minimap.Visible;
            PlaceLayers();
        }

        void PlaceCamera(Camera camera, ArenaView view)
        {
            if (camera == null) return;
            var centre = ToWorld(view.CenterX, view.CenterY);
            var t = camera.transform;
            t.position = new Vector3(centre.x, centre.y, t.position.z);
            camera.orthographicSize = GameClock.Px(view.Height) / 2f;
        }

        Camera FindRoomCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                    if (camera.CompareTag("MainCamera")) return camera;
            return Camera.main;
        }

        PlayerShipController FindPlayer()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                var ship = root.GetComponentInChildren<PlayerShipController>();
                if (ship != null) return ship;
            }
            return null;
        }

        /// <summary>View 1: a second room camera drawn into the minimap's port over the room camera, off until show_map.</summary>
        void BuildMinimap()
        {
            var definition = content.Arena.RoomFor(Room).Minimap;
            var go = new GameObject("Minimap Camera (view 1)");
            if (ViewCamera != null) go.transform.SetParent(ViewCamera.transform.parent, false);
            go.transform.position = new Vector3(0f, 0f, ViewCamera != null ? ViewCamera.transform.position.z : -10f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = GameClock.Px(definition.Height) / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ViewCamera != null ? ViewCamera.backgroundColor : Color.black;
            camera.nearClipPlane = ViewCamera != null ? ViewCamera.nearClipPlane : 0.3f;
            camera.farClipPlane = ViewCamera != null ? ViewCamera.farClipPlane : 100f;
            camera.depth = (ViewCamera != null ? ViewCamera.depth : 0f) + 1f;
            go.AddComponent<LetterboxCamera>().SetPort(new Rect(definition.PortX, definition.PortY, definition.PortWidth, definition.PortHeight));
            camera.enabled = false;
            MinimapCamera = camera;
        }

        /// <summary>The GUI layer: a 1920x1080 stage (view 0's size) kept on the letterboxed screen by <see cref="HareUiFit"/>.</summary>
        void BuildHud()
        {
            var host = new GameObject("HUD");
            host.transform.SetParent(transform, false);
            var document = HarePanel.Attach(host);
            panelSettings = document.panelSettings;
            var stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>();

            Hud = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            Hud.style.position = Position.Absolute;
            Hud.style.left = 0;
            Hud.style.top = 0;
            Hud.style.width = content.Arena.HudWidth;
            Hud.style.height = content.Arena.HudHeight;
            stage.Add(Hud);
        }

        /// <summary>
        /// One sprite per background slot that holds a background, on the backdrop sorting layer in slot order. A tiled
        /// axis is drawn one tile wider than the room, so wherever the scroll has left it, its tiles cover the room; a
        /// stretched background's tile is the whole room.
        /// </summary>
        void BuildLayers()
        {
            if (layerRoot != null) Destroy(layerRoot.gameObject);
            layers.Clear();
            layerSizes.Clear();
            layerRoot = new GameObject("Backgrounds").transform;
            layerRoot.SetParent(transform, false);

            var manifest = HareArtManifest.Load();
            foreach (var layer in State.Layers)
            {
                if (!layer.Drawn)
                {
                    layers.Add(null);
                    layerSizes.Add(Vector2Int.zero);
                    continue;
                }
                var go = new GameObject(layer.Name);
                go.transform.SetParent(layerRoot, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = HareSortingLayers.Backdrop;
                renderer.sortingOrder = layer.Index;

                var background = manifest != null ? manifest.FindBackground(layer.Name) : null;
                var image = background != null ? new Vector2Int(background.width, background.height) : new Vector2Int(State.Width, State.Height);
                if (background == null) Debug.LogWarning("[Unity.Hare] arena: background " + layer.Name + " is not imported.");
                else renderer.sprite = background.sprite;

                // The tile: the image, or the room for a stretched background.
                var tile = layer.Stretch ? new Vector2Int(State.Width, State.Height) : image;
                int columns = layer.HTiled ? Mathf.CeilToInt(State.Width / (float)tile.x) + 1 : 1;
                int rows = layer.VTiled ? Mathf.CeilToInt(State.Height / (float)tile.y) + 1 : 1;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(GameClock.Px(columns * image.x), GameClock.Px(rows * image.y));
                go.transform.localScale = new Vector3(tile.x / (float)image.x, tile.y / (float)image.y, 1f);
                layers.Add(renderer);
                layerSizes.Add(tile);
            }
        }

        /// <summary>
        /// Moves each layer to where GameMaker draws its first tile: on a tiled axis background_x mod the tile, one tile
        /// back (the sprites pivot on their top left); on an untiled one, background_x itself.
        /// </summary>
        void PlaceLayers()
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] == null) continue;
                var start = TileStart(i);
                var world = ToWorld(start.x, start.y);
                layers[i].transform.position = new Vector3(world.x, world.y, 0f);
            }
        }

        /// <summary>The room pixel the drawn layer at slot <paramref name="index"/> starts at (its first tile's top left).</summary>
        public Vector2Int TileStart(int index)
        {
            var layer = State.Layers[index];
            var tile = layerSizes[index];
            int x = layer.HTiled ? Wrap(layer.X, tile.x) - tile.x : layer.X;
            int y = layer.VTiled ? Wrap(layer.Y, tile.y) - tile.y : layer.Y;
            return new Vector2Int(x, y);
        }

        static int Wrap(int position, int size) => size <= 0 ? 0 : ((position % size) + size) % size;

        /// <summary>The room's object, as ProjectSetup puts it in each play room's scene.</summary>
        public static ArenaRoom Create() => new GameObject(ObjectName).AddComponent<ArenaRoom>();
    }
}
