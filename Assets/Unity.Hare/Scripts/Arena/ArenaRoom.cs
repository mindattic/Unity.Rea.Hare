using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Audio;
using Unity.Hare.Content;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Player;
using Unity.Hare.Rendering;
using Unity.Hare.Rocks;
using Unity.Hare.Session;
using Unity.Hare.Structures;
using Unity.Hare.UI;
using Unity.Hare.Weapons;
using Unity.Hare.Weather;

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
    ///
    /// It is also obj_game's Create and Step around the level (U.HARE-T57): <see cref="Begin"/> is setup_level, run when
    /// the room comes up and again whenever the room flow's fade asks for the next level
    /// (<see cref="RoomFlowDirector.LevelAdvance"/>: <c>current_level += 1; setup_level()</c>, or the level script's
    /// room_goto when the next level plays in another room). Every room step it ticks the session's clock and objective
    /// (obj_healthbar's second, obj_game's objective check), steps obj_levelstart, and watches level_08's target; it plays
    /// the session's sfx_beep countdown and keeps the runtime's paused flag on the session's.
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
        GameSession ownSession;
        RoomFlowDirector subscribed;
        bool warnedTarget;

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

        /// <summary>The level's par_structure and their covers (U.HARE-T56).</summary>
        public StructureField Structures { get; private set; }

        /// <summary>The level's weather (U.HARE-T18): the room's obj_cloud_emitter and whatever else the level names.</summary>
        public WeatherView Weather { get; private set; }

        /// <summary>The level's par_rock (U.HARE-T57): setup_level's irandom_range(rock_min, rock_max) and move_rocks.</summary>
        public RockField Rocks { get; private set; }

        /// <summary>obj_levelstart, the level's banner (U.HARE-T57); null once it has faded.</summary>
        public LevelStartBanner Banner { get; private set; }

        /// <summary>The banner as drawn on the HUD (null once gone).</summary>
        public LevelStartView BannerView { get; private set; }

        /// <summary>What setup_level's enemy blocks placed: the par_enemy_emitter vortices and the mimics.</summary>
        public EnemyPlacement Enemies { get; private set; }

        /// <summary>Generator mode's obj_wormhole_emitter (null in the other modes, and once its list is spent).</summary>
        public WormholeEmitter Generator { get; private set; }

        /// <summary>The o_defeat target the level script spawned (level_08's obj_scrapmental), while it exists.</summary>
        public GameObject Target { get; private set; }

        /// <summary>Whether the level script's target was spawned (its absence then means it was defeated).</summary>
        public bool HasTarget { get; private set; }

        /// <summary>
        /// globals.gml's state this room plays: the room flow's session, or, for a room run without the flow (a test, a
        /// tool), one of its own.
        /// </summary>
        public GameSession Session
        {
            get
            {
                var flow = Flow;
                if (flow != null) return flow.Session;
                return ownSession ?? (ownSession = new GameSession(content));
            }
        }

        RoomFlowDirector Flow => director != null ? director : RoomFlowDirector.Active;

        /// <summary>set_level_complete ran (the objective was met, or the wormhole emitter's list ran out); the argument is the level.</summary>
        public event Action<int> LevelCompleted;

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

        /// <summary>
        /// The play room now running (GameMaker has one room at a time), for what asks room_width/room_height and
        /// buffer_zone from anywhere in it: obj_bullet's and obj_rocket's out-of-play check (U.HARE-T21). Null outside a
        /// play room.
        /// </summary>
        public static ArenaRoom Active { get; private set; }

        /// <summary>
        /// Whether the world point <paramref name="world"/> is past the active play room's buffer zone
        /// (<see cref="ArenaWorld.OutOfPlay(float, float)"/>); null when no play room is running, so the caller keeps its
        /// own stand-in.
        /// </summary>
        public static bool? OutOfPlay(Vector2 world)
        {
            var arena = Active;
            if (arena == null || arena.State == null) return null;
            var at = arena.ToRoom(world);
            return arena.State.OutOfPlay(at.x, at.y);
        }

        void OnEnable() => Active = this;

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void Awake()
        {
            content = GameContent.Current;
            Room = RoomList.TryParse(gameObject.scene.name, out var room) && content.Arena.RoomFor(room) != null ? room : RoomId.GameScreen;
            roomDefinition = content.Rooms.Get(Room);
            ViewCamera = FindRoomCamera();
            BuildMinimap();
            BuildHud();
            SpawnPoint = new GameObject("spawnpoint").transform;
            SpawnPoint.SetParent(transform, false);

            // GML: obj_game's key-press event for M (77).
            controls = new HareControls();
            controls.Meta.Map.Enable();
        }

        void Start()
        {
            director = RoomFlowDirector.Active;
            if (director != null)
            {
                director.RoomStepped += OnRoomStepped;
                director.LevelAdvance += OnLevelAdvance;
                director.SessionEventPlayed += OnSessionEvent;
                subscribed = director;
            }
            // obj_game's Create: setup_level (the ship it makes is the scene's own, flown in once it is attached).
            Begin(Session.Level);
            AttachPlayer(FindPlayer());
            SetUpPlayer();
            Play(State.Execute(new CreateArena(Seat)));
            Present();
        }

        void OnDestroy()
        {
            if (director != null) director.RoomStepped -= OnRoomStepped;
            if (subscribed != null)
            {
                subscribed.LevelAdvance -= OnLevelAdvance;
                subscribed.SessionEventPlayed -= OnSessionEvent;
                subscribed = null;
            }
            if (Generator != null) Generator.LevelCompleted -= OnGeneratorCompleted;
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
        /// setup_level for <paramref name="level"/> (U.HARE-T57; generator mode runs level_enemygenerator whatever the level),
        /// in the original's order: the setting's backgrounds, their speeds and loops (U.HARE-T52; a room opened on its own
        /// plays the first level that plays in it); "Clear Objects" (<see cref="LevelReset"/>, the last level's target and
        /// obj_levelstart); a new obj_levelstart (sfx_switch); the ship flown in by reset_player; <c>level_complete = false;
        /// paused = false</c>; the level script's name, objective and stationary_percent (the session's
        /// <see cref="BeginLevel"/>) and level_08's boss; in generator mode generate_enemy_list(difficulty) and the
        /// obj_wormhole_emitter; then irandom_range(rock_min, rock_max) rocks, the structures (U.HARE-T56) and the enemy
        /// blocks' vortices and mimics, each with its move_rocks half. The weather starts after (U.HARE-T18).
        /// </summary>
        public void Begin(int level)
        {
            var session = Session;
            var setting = LevelSetup.SettingFor(content.Arena, session.Mode, level, Room);
            bool created = State != null && State.Created;
            State = new ArenaWorld(content.Arena, roomDefinition, setting, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks)));
            SpawnPoint.position = ToWorld(State.SpawnX, State.SpawnY);
            BuildLayers();
            var room = RoomRect();

            // Clear Objects: with(par_rock) ... with(par_pickup) (U.HARE-T23, U.HARE-T56), with(obj_levelstart).
            LevelReset.ClearObjects();
            ClearTarget();
            if (Generator != null)
            {
                Generator.LevelCompleted -= OnGeneratorCompleted;
                Generator = null;
            }
            BannerView?.RemoveFromHierarchy();
            BannerView = null;

            // instance_create(0,0, obj_levelstart): its Create plays sfx_switch; it draws the globals the script sets below.
            Banner = new LevelStartBanner(session.LevelName, session.ObjectiveText, content.Arena);
            HareAudio.Play(content.Arena.LevelStartSound);

            // player = instance_create(0,0, obj_player); reset_player(); (the scene's ship, once attached)
            SetUpPlayer();

            // level_complete = false; paused = false; then the level script's Meta Data and Objective blocks.
            Gml.Paused = false;
            RunSession(new BeginLevel(Seat, setting.Objective.ToObjective(), setting.Name, setting.StationaryPercent));
            Gml.Paused = Session.IsPaused;
            Banner.SetText(Session.LevelName, Session.ObjectiveText);
            if (Hud != null) BannerView = LevelStartView.Show(Hud, Banner, content.Arena);
            SpawnTarget(setting.Objective);

            // gm_generator: generate_enemy_list(difficulty); instance_create(obj_wormhole_emitter) (D6's swapped arguments fixed).
            if (session.Mode == GameMode.Generator && ReferenceEquals(setting, content.Arena.GeneratorLevel))
            {
                Generator = WormholeEmitter.Create(session.Difficulty, room, Player);
                Generator.transform.SetParent(transform, true);
                Generator.LevelCompleted += OnGeneratorCompleted;
            }

            // Generate Rocks and Generate Structures, each with its move_rocks half.
            if (Rocks == null)
            {
                Rocks = RockField.CreateField();
                Rocks.transform.SetParent(transform, false);
            }
            Rocks.SetRoom(room);
            Rocks.Setup(setting.RockMin, setting.RockMax, Session.StationaryPercent);
            if (Structures == null)
            {
                Structures = StructureField.CreateField();
                Structures.transform.SetParent(transform, false);
            }
            Structures.SetRoom(room);
            Structures.Setup(setting.StructureMin, setting.StructureMax);

            // Generate Hydras ... Trilobytes: a par_enemy_emitter per roll (a mimic itself), anywhere in the room.
            Enemies = setting.ToComposition().Place(room, Player);

            if (Weather == null)
            {
                Weather = WeatherView.Create(State.Width, State.Height, ToWorld, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks) ^ 0x5EA7));
                Weather.transform.SetParent(transform, false);
            }
            Weather.Begin(setting.Weather); // the room's obj_cloud_emitter (and any other emitter the level names), U.HARE-T18
            if (created) Play(State.Execute(new CreateArena(Seat)));
        }

        /// <summary>The room in world units (the room is centred on the world origin).</summary>
        public Rect RoomRect() =>
            new Rect(GameClock.Px(-State.Width / 2f), GameClock.Px(-State.Height / 2f), GameClock.Px(State.Width), GameClock.Px(State.Height));

        /// <summary>setup_level's new obj_player and reset_player, on the ship this room follows (no ship, nothing).</summary>
        void SetUpPlayer()
        {
            if (Player == null) return;
            var ship = Player.GetComponent<PlayerHealth>();
            if (ship == null) return;
            ship.RespawnPoint = SpawnPoint;
            ship.SetUp();
        }

        /// <summary>level_08's Spawn Boss block: instance_create(room_width/2, room_height/2, target), and target_id = it.</summary>
        void SpawnTarget(LevelObjectiveDefinition objective)
        {
            HasTarget = false;
            if (objective == null || objective.Kind != ObjectiveKind.Defeat || string.IsNullOrEmpty(objective.TargetObject)) return;
            Target = LevelTargets.Spawn(objective.TargetObject, ToWorld(State.Width / 2f, State.Height / 2f));
            HasTarget = Target != null;
            if (!HasTarget && !warnedTarget)
            {
                warnedTarget = true;
                Debug.LogWarning("[Unity.Hare] " + objective.TargetObject + " is not ported yet (U.HARE-T68), so this level's o_defeat objective cannot be met.");
            }
        }

        /// <summary>The last level's target goes with with(par_enemy) (obj_scrapmental is an enemy).</summary>
        void ClearTarget()
        {
            if (Target != null)
            {
                if (Application.isPlaying) Destroy(Target); else DestroyImmediate(Target);
            }
            Target = null;
            HasTarget = false;
        }

        /// <summary>The room flow's fade reached black: <c>current_level += 1; setup_level();</c>.</summary>
        void OnLevelAdvance()
        {
            RunSession(new AdvanceLevel(Seat, resetScore: false));
            var session = Session;
            var elsewhere = LevelSetup.RoomGoto(content.Arena, session.Mode, session.Level, Room);
            var flow = Flow;
            if (elsewhere.HasValue && flow != null)
            {
                // setup_level got as far as the level script here before its if(room != room_x) room_goto(room_x): the
                // obj_levelstart and obj_player it made played sfx_switch and sfx_flyover, then the room change took them
                // away and that room's obj_game runs setup_level again.
                HareAudio.Play(content.Arena.LevelStartSound);
                SetUpPlayer();
                flow.Execute(new GoToRoom(Seat, elsewhere.Value));
                return;
            }
            Begin(session.Level);
        }

        void OnGeneratorCompleted(WormholeEmitter emitter)
        {
            if (emitter == Generator) Generator = null;
            RunSession(new CompleteLevel(Seat)); // obj_wormhole_emitter: set_level_complete()
        }

        /// <summary>
        /// Runs a session command: through the room flow, which plays its events to every listener (this room among them),
        /// or on the room's own session.
        /// </summary>
        SessionResult RunSession(SessionCommand command)
        {
            var flow = Flow;
            if (flow != null)
            {
                var played = flow.ExecuteSession(command);
                if (subscribed != flow)
                    foreach (var e in played.Events) OnSessionEvent(e);
                return played;
            }
            var result = Session.Execute(command);
            foreach (var e in result.Events) OnSessionEvent(e);
            return result;
        }

        /// <summary>What the session raised that obj_game and set_level_complete play.</summary>
        void OnSessionEvent(SessionEvent e)
        {
            switch (e)
            {
                case TimeLimitWarning _:
                    HareAudio.Play(content.Arena.TimeLimitBeepSound); // obj_game's Step: audio_play_sound(sfx_beep, 1, 0)
                    break;
                case PausedChanged paused:
                    Gml.Paused = paused.Paused;
                    break;
                case Unity.Hare.Session.LevelCompleted completed:
                    LevelCompleted?.Invoke(completed.Level); // obj_level_results is the results screen's (its own ticket)
                    break;
            }
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
        /// backgrounds' scroll; then the weather's step (U.HARE-T18); then level_08's target, the session's clock and
        /// objective, and obj_levelstart's alarms (U.HARE-T57).
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
            if (Weather != null) Weather.Step();

            // obj_game's objective check against level_08's target, then obj_healthbar's one-second alarm and the rest of
            // obj_game's check (U.HARE-T57).
            var session = Session;
            if (session.Objective == ObjectiveKind.Defeat && HasTarget && Target == null && !session.TargetDefeated)
                RunSession(new DefeatTarget(Seat)); // !instance_exists(target_id)
            RunSession(new TickSession(Seat, GameClock.Seconds(1)));

            // obj_levelstart's alarms.
            if (Banner != null && !Banner.Step())
            {
                BannerView?.Refresh();
                BannerView = null;
                Banner = null;
            }
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

        /// <summary>random_positions: every rock, then every structure (its cover with it, U.HARE-T56), then every enemy.</summary>
        void RandomPositions()
        {
            var rocks = new List<Rock>();
            foreach (var rock in FindObjectsByType<Rock>(FindObjectsSortMode.InstanceID))
                if (rock != null && !rock.IsGone) rocks.Add(rock);
            var structures = new List<Structure>();
            foreach (var structure in FindObjectsByType<Structure>(FindObjectsSortMode.InstanceID))
                if (structure != null && !structure.IsGone) structures.Add(structure);
            var enemies = new List<int>();
            foreach (var body in EnemyDirector.World.Enemies)
                if (body != null && !body.IsDead) enemies.Add(body.Id);

            var result = State.Execute(new ScatterObjects(Seat, rocks.Count + structures.Count + enemies.Count));
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
                        else if (i < rocks.Count + structures.Count) structures[i - rocks.Count].MoveTo(world);
                        else EnemyDirector.Execute(new PlaceBody(enemies[i - rocks.Count - structures.Count], world.ToVec2()));
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
            BannerView?.Refresh();
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
