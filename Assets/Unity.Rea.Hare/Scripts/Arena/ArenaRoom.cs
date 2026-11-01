using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Audio;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Input;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Popups;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.Rocks;
using Unity.Rea.Hare.Session;
using Unity.Rea.Hare.Structures;
using Unity.Rea.Hare.UI;
using Unity.Rea.Hare.Weapons;
using Unity.Rea.Hare.Weather;

namespace Unity.Rea.Hare.Arena
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
    ///
    /// And it is the level's end (U.HARE-T22): set_level_complete's obj_level_results (<see cref="Results"/>, drawn by
    /// <see cref="LevelResultsView"/> with obj_healthbar's <see cref="Clock"/> and KILLS, which it counts from the enemies'
    /// deaths), Enter's <see cref="ContinueFromResults"/>, and the leaving ship's steering and exit off the top, which sets
    /// up the next level with the score zeroed (docs/BIBLE.md V31).
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
        InputAction enterKey;
        bool leftLevel;

        /// <summary>
        /// obj_level_results (U.HARE-T22): set_level_complete's panel while it is up, else null. Enter
        /// (<see cref="ContinueFromResults"/>) sends the ship off and takes it away.
        /// </summary>
        public LevelResults Results { get; private set; }

        /// <summary>The panel as drawn on the HUD (null while there is none).</summary>
        public LevelResultsView ResultsView { get; private set; }

        /// <summary>
        /// obj_healthbar's minutes:seconds (strTime), made with the room (obj_game's Create makes one per room) and counted on
        /// every unpaused second the session counts; the results panel's "Time" (U.HARE-T22).
        /// </summary>
        public HudClock Clock { get; private set; } = new HudClock();

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

        /// <summary>
        /// obj_game's four HUD objects (U.HARE-T25): obj_healthbar, obj_weapon_gui, obj_energybar and obj_speedometer,
        /// stepped every room step and drawn by <see cref="HudView"/> every frame.
        /// </summary>
        public GameHud GameHud { get; private set; }

        /// <summary>The gameplay HUD as drawn on <see cref="Hud"/>.</summary>
        public GameHudView HudView { get; private set; }

        /// <summary>
        /// The room's obj_float_text (U.HARE-T27): every score's show_float_text_ext, stepped every room step and drawn by
        /// <see cref="FloatTextView"/> where view 0 shows them.
        /// </summary>
        public FloatTextField FloatTexts { get; private set; }

        /// <summary>The floating texts as drawn on <see cref="Hud"/>, beneath the HUD objects.</summary>
        public FloatTextView FloatTextView { get; private set; }

        /// <summary>
        /// The room's obj_flashbomb (U.HARE-T67): obj_player's F sets one off for the room's ship, each room step steps them
        /// (their sweep round the ship), and <see cref="FlashbombViews"/> draws them.
        /// </summary>
        public FlashbombField Flashbombs { get; private set; }

        readonly Dictionary<int, FlashbombView> flashbombViews = new Dictionary<int, FlashbombView>();

        /// <summary>The flashbombs as drawn, by id.</summary>
        public IReadOnlyDictionary<int, FlashbombView> FlashbombViews => flashbombViews;

        /// <summary>Every flashbomb event, after it was played.</summary>
        public event Action<FlashbombEvent> FlashbombPlayed;

        /// <summary>The level's par_structure and their covers (U.HARE-T56).</summary>
        public StructureField Structures { get; private set; }

        /// <summary>The room's obj_surface_reflection as drawn (U.HARE-T58); null in a room without one.</summary>
        public SurfaceReflectionView ReflectionView { get; private set; }

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
                if (ownSession == null)
                {
                    ownSession = new GameSession(content);
                    SessionSettings.Seed(ownSession, Seat);
                }
                return ownSession;
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

            // obj_game's own keys (U.HARE-T60), all HareControls' Meta actions: the key presses M (77), F12 (123), P (80),
            // R (82), U (85) and F11 (122), and the held keypad + (107) and - (109). Esc and F1 belong to the room flow.
            controls = new HareControls();
            foreach (var action in GameKeys) action.Enable();
            Flashbombs = new FlashbombField(content.Arena.Flashbomb);

            // GML: obj_level_results' key-press event for vk_enter (13), which the numpad's Enter sends too.
            enterKey = new InputAction("Enter", InputActionType.Button);
            enterKey.AddBinding("<Keyboard>/enter");
            enterKey.AddBinding("<Keyboard>/numpadEnter");
            enterKey.Enable();
            EnemyDirector.Killed += OnEnemyKilled;
            EnemyDirector.Scored += OnEnemyScored;
            Rock.Removed += OnRockRemoved;
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
            EnemyDirector.Killed -= OnEnemyKilled;
            EnemyDirector.Scored -= OnEnemyScored;
            Rock.Removed -= OnRockRemoved;
            if (enterKey != null)
            {
                enterKey.Disable();
                enterKey.Dispose();
                enterKey = null;
            }
            DetachHealth();
            if (volumeUnsaved) SaveVolume();
            if (controls != null)
            {
                foreach (var action in GameKeys) action.Disable();
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
            var setting = LevelSetup.SettingFor(content.Arena, content.Story, session, level, Room);
            bool created = State != null && State.Created;
            State = new ArenaWorld(content.Arena, roomDefinition, setting, new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks)));
            SpawnPoint.position = ToWorld(State.SpawnX, State.SpawnY);
            BuildLayers();
            if (State.Reflection.Active && ReflectionView == null)
                ReflectionView = SurfaceReflectionView.Create(transform, State.Reflection.Definition.Depth); // the room's obj_surface_reflection
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
            RemoveResults(); // Clear Objects' with(obj_level_results) instance_destroy()
            leftLevel = false;

            // instance_create(0,0, obj_levelstart): its Create plays sfx_switch; it draws the globals the script sets below.
            Banner = new LevelStartBanner(session.LevelName, session.ObjectiveText, content.Arena);
            HareAudio.Play(content.Arena.LevelStartSound);

            // player = instance_create(0,0, obj_player); reset_player(); (the scene's ship, once attached)
            SetUpPlayer();

            // level_complete = false; paused = false; jukebox = instance_create(0,0,obj_jukebox) (U.HARE-T29, BIBLE D33/V51);
            // then the level script's Meta Data and Objective blocks.
            Gml.Paused = false;
            EnsureJukebox();
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

        /// <summary>
        /// The garage's bought pips on the ship this room follows (U.HARE-T24, docs/BIBLE.md V49): the vulcan's rate, the
        /// shotgun's reload and spread and the rockets' blast, from the campaign save. Nothing bought (or no room flow):
        /// the original's ship.
        /// </summary>
        void FitGarageUpgrades()
        {
            if (Player == null) return;
            var flow = director != null ? director : RoomFlowDirector.Active;
            var upgrades = flow != null && flow.Campaign != null ? flow.ShipUpgrades : ShipUpgrades.None;
            var seat = PlayerSeat.Of(Player.gameObject);
            seat.Send(new FitUpgrades(seat.Id, upgrades));
        }

        /// <summary>setup_level's new obj_player and reset_player, on the ship this room follows (no ship, nothing).</summary>
        void SetUpPlayer()
        {
            if (Player == null) return;
            var ship = Player.GetComponent<PlayerHealth>();
            if (ship == null) return;
            ship.RespawnPoint = SpawnPoint;
            ship.SetUp();
            FitGarageUpgrades();
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
                Debug.LogWarning("[Unity.Rea.Hare] " + objective.TargetObject + " is not ported, so this level's o_defeat objective cannot be met.");
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
        void OnLevelAdvance() => AdvanceLevel(resetScore: false);

        /// <summary>
        /// <c>current_level += 1; setup_level()</c>, after <c>current_score = 0; level_complete = false</c> when
        /// <paramref name="resetScore"/> (the leaving ship's exit off the top, U.HARE-T22; the fade-in does not): the next
        /// level here, or the level script's room_goto when it plays in another room. Like the original it goes straight
        /// on to the next level (docs/BIBLE.md V31), except that a map mission's level, once won, goes back to room_map
        /// instead (U.HARE-T64, owner-directed, V42: otherwise winning level_07 would run straight into the locked boss).
        /// The score reset still runs first either way (U.HARE-T111 fix): otherwise a mission won without going on to
        /// setup_level never zeroed current_score, and the next mission chosen from the map (ChooseMission does not zero
        /// it either) started with the previous one's score.
        /// </summary>
        void AdvanceLevel(bool resetScore)
        {
            var flow = Flow;
            var back = LevelSetup.RoomAfterLevel(Session);
            RunSession(new AdvanceLevel(Seat, resetScore));
            var session = Session;
            if (back.HasValue && flow != null)
            {
                RunSession(new Unity.Rea.Hare.Session.SetPaused(Seat, false));
                flow.Execute(new GoToRoom(Seat, back.Value));
                return;
            }

            var elsewhere = LevelSetup.RoomGoto(content.Arena, content.Story, session, session.Level, Room);
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

        /// <summary>The room's obj_jukebox (U.HARE-T29): made by the first setup_level, then kept for the room's later levels.</summary>
        public Jukebox Jukebox { get; private set; }
        IRandomSource jukeboxRandom;

        /// <summary>setup_level's obj_jukebox: its Step's play_random_music runs through the session, which picks the track.</summary>
        void EnsureJukebox()
        {
            if (Jukebox != null) return;
            jukeboxRandom = new SeededRandom(unchecked((int)DateTime.UtcNow.Ticks) ^ 0x7D2B);
            Jukebox = Jukebox.Ensure(transform);
            Jukebox.TrackWanted = () =>
            {
                RunSession(new PlayRandomMusic(Seat, jukeboxRandom)); // its MusicTrackChosen plays the track
                return HareAudio.IsMusicPlaying;
            };
        }

        /// <summary>What the session raised that obj_game and set_level_complete play.</summary>
        void OnSessionEvent(SessionEvent e)
        {
            switch (e)
            {
                case MusicTrackChosen track:
                    HareAudio.PlayMusic(track.Sound); // play_random_music: audio_play_sound(current_track, 1, 0)
                    break;
                case TimeLimitWarning _:
                    HareAudio.Play(content.Arena.TimeLimitBeepSound); // obj_game's Step: audio_play_sound(sfx_beep, 1, 0)
                    break;
                case PausedChanged paused:
                    Gml.Paused = paused.Paused;
                    break;
                case MasterVolumeChanged volume:
                    HareAudio.MasterVolume = volume.Volume; // sound_global_volume(master_volume)
                    volumeUnsaved = true;
                    break;
                case SecondElapsed _:
                    Clock.Tick(); // obj_healthbar's alarm: seconds += 1 ... strTime
                    break;
                case Unity.Rea.Hare.Session.LevelCompleted completed:
                    ShowResults(); // set_level_complete: instance_create(0,0, obj_level_results) at (screen_width/2, screen_height/4)
                    LevelCompleted?.Invoke(completed.Level);
                    break;
            }
        }

        /// <summary>set_level_complete's obj_level_results, drawn on the HUD with the room's strTime and KILLS.</summary>
        void ShowResults()
        {
            RemoveResults();
            Results = new LevelResults(content.Arena.Results);
            if (Hud != null) ResultsView = LevelResultsView.Show(Hud, Results, content.Arena, () => Clock.Text, () => Session.Kills);
        }

        void RemoveResults()
        {
            ResultsView?.RemoveFromHierarchy();
            ResultsView = null;
            Results = null;
        }

        /// <summary>
        /// obj_level_results' Enter (U.HARE-T22): <c>paused = false; player.state = state_leave; player.exit_x = player.x;
        /// player.invulnerable = true; audio_play_sound(sfx_boost, 1, 0); effect_create_below(ef_ring, player.x, player.y, 1,
        /// c_white); instance_destroy()</c>. The ship then flies off the top (<see cref="Step"/>) and the next level is set up.
        /// False when no results panel is up.
        /// </summary>
        public bool ContinueFromResults()
        {
            if (Results == null) return false;
            var definition = Results.Definition;
            RemoveResults();
            RunSession(new Unity.Rea.Hare.Session.SetPaused(Seat, false));
            HareAudio.Play(definition.ContinueSound);
            if (Player == null)
            {
                AdvanceLevel(resetScore: true); // no ship to fly off: straight on, as its exit would
                return true;
            }
            Player.Leave();
            Play(State.Execute(new SendPlayerOff(Seat, PlayerRoomPosition().x)));
            ColorUtility.TryParseHtmlString(definition.ContinueRingColor, out var ring);
            GmlEffects.CreateBelow(GmlEffectKind.Ring, Player.transform.position, definition.ContinueRingSize, ring);
            return true;
        }

        /// <summary>
        /// An enemy died: <c>show_float_text_ext(x, y, string(n), true, 0.01, 1); current_score += n</c> when its death
        /// scores (U.HARE-T27), and KILLS += 1 for an enemy the ship's weapons destroyed (docs/BIBLE.md D11).
        /// </summary>
        void OnEnemyKilled(EnemyKill kill)
        {
            if (Active != this) return;
            if (kill.AwardsScore && kill.Points > 0) Score(kill.Position.ToVector2(), kill.Points);
            if (kill.CountsAsKill) RunSession(new RecordKill(Seat));
        }

        /// <summary>Points an enemy scored without dying (a Scrap Mental hull piece shot loose, the boss's defeat; U.HARE-T68).</summary>
        void OnEnemyScored(Vector2 world, int points)
        {
            if (Active != this || points <= 0) return;
            Score(world, points);
        }

        /// <summary>par_rock destroyed by the ship's shot or a rocket: its 10 points and their float text (U.HARE-T27).</summary>
        void OnRockRemoved(Rock rock, RockGone gone)
        {
            if (Active != this || !gone.Destroyed || !gone.AwardsScore || gone.Points <= 0) return;
            Score(gone.Position, gone.Points);
        }

        /// <summary>
        /// The pair every scoring death in the original runs (U.HARE-T27): <c>show_float_text_ext(x, y, string(points),
        /// true, 0.01, 1)</c> at <paramref name="world"/>, then <c>current_score += points</c> through the session.
        /// </summary>
        public void Score(Vector2 world, int points)
        {
            if (State != null && FloatTexts != null)
            {
                var at = ToRoom(world);
                FloatTexts.ShowScore(at.x, at.y, points);
            }
            RunSession(new AddScore(Seat, points));
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
            shipSeat = PlayerSeat.Of(ship.gameObject);
            shipSeat.EventRaised += OnShipEvent;
        }

        PlayerSeat shipSeat;

        void DetachHealth()
        {
            if (shipSeat != null)
            {
                shipSeat.EventRaised -= OnShipEvent;
                shipSeat = null;
            }
            if (health == null) return;
            health.Damaged -= OnPlayerDamaged;
            health = null;
        }

        /// <summary>
        /// player_boost and player_brake each <c>instance_create(0, 0, obj_booster_schematic / obj_brake_schematic)</c>
        /// when they go through: the HUD's fading schematic (U.HARE-T92).
        /// </summary>
        void OnShipEvent(PlayerEvent e)
        {
            if (GameHud == null) return;
            if (e is BoostStarted) GameHud.ShowSchematic(SchematicKind.Booster);
            else if (e is BrakeStarted) GameHud.ShowSchematic(SchematicKind.Brake);
        }

        /// <summary>player_hurt's obj_losthealth at <c>column2 + HP * width</c> on the GUI layer.</summary>
        void OnPlayerDamaged(int healthLeft)
        {
            if (Hud != null) LostHealthView.Show(Hud, new LostHealthPip(healthLeft, content.Arena.HudHeight, content.Health));
        }

        /// <summary>The Meta actions obj_game answers (Esc's Menu and F1's Fullscreen are the room flow's).</summary>
        InputAction[] GameKeys => new[]
        {
            controls.Meta.Map, controls.Meta.Debug, controls.Meta.Pause, controls.Meta.Restart, controls.Meta.Units,
            controls.Meta.AddScore, controls.Meta.VolumeUp, controls.Meta.VolumeDown, controls.Meta.Flashbomb,
        };

        /// <summary>
        /// obj_game's key-press events (U.HARE-T60), each through its HareControls Meta action: M <c>show_map =
        /// !show_map</c>; F12 <c>debug = !debug</c>; P <c>if(!level_complete) paused = !paused</c>; U <c>use_metric =
        /// !use_metric</c> (saved to the profile); F11 <c>current_score += 100</c> (a debug key: no float text, but the
        /// points objective counts it); R <c>reset_player(); setup_level()</c>. The keypad's +/- are held keys, stepped
        /// with the room (<see cref="StepVolume"/>); what they changed is saved once both are let go.
        /// </summary>
        void ReadGameKeys()
        {
            if (controls == null) return;
            var meta = controls.Meta;
            if (meta.Map.WasPressedThisFrame()) Play(State.Execute(new ToggleMinimap(Seat)));
            if (meta.Debug.WasPressedThisFrame()) RunSession(new ToggleDebug(Seat));
            if (meta.Pause.WasPressedThisFrame()) RunSession(new TogglePause(Seat));
            if (meta.Units.WasPressedThisFrame())
            {
                RunSession(new ToggleUnits(Seat));
                SessionSettings.SaveUseMetric(Session.UseMetric);
            }
            if (meta.AddScore.WasPressedThisFrame()) RunSession(new AddScore(Seat, content.Session.DebugScorePoints));
            if (meta.Restart.WasPressedThisFrame()) RestartLevel();
            if (meta.Flashbomb.WasPressedThisFrame()) SetOffFlashbomb();
            if (volumeUnsaved && !meta.VolumeUp.IsPressed() && !meta.VolumeDown.IsPressed()) SaveVolume();
        }

        /// <summary>
        /// obj_game's keyboard events for the keypad's + (107) and - (109), run every step either is held:
        /// <c>master_volume</c> up or down a step within 0..1, then <c>sound_global_volume(master_volume)</c>
        /// (<see cref="HareAudio.MasterVolume"/>, played from the session's <see cref="MasterVolumeChanged"/>).
        /// </summary>
        void StepVolume()
        {
            if (controls == null) return;
            if (controls.Meta.VolumeUp.IsPressed()) RunSession(new StepMasterVolume(Seat, +1));
            if (controls.Meta.VolumeDown.IsPressed()) RunSession(new StepMasterVolume(Seat, -1));
        }

        bool volumeUnsaved;

        void SaveVolume()
        {
            volumeUnsaved = false;
            HareAudio.SaveMasterVolume();
        }

        /// <summary>
        /// obj_player's key press F (70): <c>flashbomb = instance_create(0, 0, obj_flashbomb)</c> for the room's ship. The
        /// original has no ammo, cooldown, paused or dead check on it, so neither does this: every press is another bomb.
        /// </summary>
        public void SetOffFlashbomb()
        {
            if (Player == null || Flashbombs == null) return; // obj_player's own key event: no ship, no bomb
            PlayFlashbombs(Flashbombs.Execute(new SetOffFlashbomb(Seat)));
        }

        /// <summary>
        /// Plays what the flashbombs did: Create's obj_rabbit_schematic and sfx_flashbomb and the bomb's view; each Step's
        /// sweep round the ship (<see cref="FlashbombSweep"/>, in view 0); and the bomb's end.
        /// </summary>
        void PlayFlashbombs(FlashbombResult result)
        {
            foreach (var e in result.Events)
            {
                switch (e)
                {
                    case FlashbombSetOff setOff:
                        flashbombViews[setOff.Bomb.Id] = FlashbombView.Create(transform, setOff.Bomb, Flashbombs.Definition);
                        GameHud?.ShowSchematic(SchematicKind.Rabbit);
                        HareAudio.Play(setOff.Sound);
                        break;
                    case FlashbombSwept swept:
                        if (Player != null)
                            FlashbombSweep.Sweep(Player.transform.position, GameClock.Px((float)swept.Radius), InView);
                        break;
                    case FlashbombGone gone:
                        if (flashbombViews.TryGetValue(gone.Bomb.Id, out var view))
                        {
                            flashbombViews.Remove(gone.Bomb.Id);
                            if (view != null) Destroy(view.gameObject);
                        }
                        break;
                }
                FlashbombPlayed?.Invoke(e);
            }
        }

        void DrawFlashbombs()
        {
            if (flashbombViews.Count == 0 || State == null) return;
            var view = State.View;
            var topLeft = ToWorld(view.X, view.Y);
            Vector2? centre = Player != null ? (Vector2)Player.transform.position : (Vector2?)null;
            foreach (var v in flashbombViews.Values)
                if (v != null) v.Draw(topLeft, view.Width, view.Height, centre);
        }

        /// <summary>obj_game's R key press: <c>reset_player(); setup_level();</c>, the level this room is playing, again.</summary>
        public void RestartLevel()
        {
            SetUpPlayer();
            Begin(Session.Level);
        }

        void Update()
        {
            ReadGameKeys();
            CountFrame();
            if (Results != null && enterKey != null && enterKey.WasPressedThisFrame()) ContinueFromResults();

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

        /// <summary>GML fps, for the debug overlay: the frames drawn in the last whole second (GameMaker updates it once a second).</summary>
        public int Fps { get; private set; }
        int framesThisSecond;
        float fpsTimer;

        void CountFrame()
        {
            framesThisSecond++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer < 1f) return;
            Fps = framesThisSecond;
            framesThisSecond = 0;
            fpsTimer = fpsTimer >= 2f ? 0f : fpsTimer - 1f;
        }

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
            bool leaving = Player != null && State.PlayerLeaving;
            if (Player != null && !leaving && !PlayerIsDead())
            {
                var at = PlayerRoomPosition();
                Play(State.Execute(new CheckPlayerBounds(Seat, at.x, at.y)));
            }
            if (leaving)
            {
                // state_leave's Step instead of the boundary check (U.HARE-T22): exit_angle, then off the top.
                var at = PlayerRoomPosition();
                Play(State.Execute(new StepLeave(Seat, at.x, at.y)));
            }
            if (Player != null && State.Reflection.Active)
            {
                // obj_surface_reflection's Alarm 0 and Step (U.HARE-T58): the splash, the reflection, the water's floor.
                var at = PlayerRoomPosition();
                Play(State.Execute(new StepReflection(Seat, at.x, at.y, Player.Facing, Player.Direction)));
            }
            if (Player != null)
            {
                var at = PlayerRoomPosition();
                Play(State.Execute(new FollowPlayer(Seat, at.x, at.y)));
            }
            StepVolume(); // obj_game's held keypad +/- (U.HARE-T60)
            Play(State.Execute(new StepArena(Seat)));
            if (Weather != null) Weather.Step();
            FloatTexts?.Step(); // every obj_float_text's Step (U.HARE-T27)
            GameHud?.Step(ReadHud()); // obj_speedometer's Step, obj_energybar's End Step (U.HARE-T25)
            if (Flashbombs != null) PlayFlashbombs(Flashbombs.Execute(new StepFlashbombs())); // every obj_flashbomb's Alarm 0 and Step (U.HARE-T67)

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

            // The leaving ship went off the top: current_score = 0; level_complete = false; current_level += 1; setup_level().
            if (leftLevel)
            {
                leftLevel = false;
                AdvanceLevel(resetScore: true);
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
                    case ShipSkimmedSurface skimmed:
                        MovePlayer(ToWorld(skimmed.X, skimmed.Y)); // player.y = surface_y
                        if (Player != null) Player.SkimSurface(skimmed.Direction);
                        break;
                    case SurfaceSplashed splash:
                        ColorUtility.TryParseHtmlString(splash.Color, out var color);
                        GmlEffects.CreateBelow(GmlEffectKind.Firework, ToWorld(splash.X, splash.Y), splash.Size, color);
                        break;
                    case ExitAimed aimed:
                        if (Player != null) Player.AimExit(aimed.Angle);
                        break;
                    case PlayerLeftLevel _:
                        leftLevel = true; // set up at the end of this step, once everything else has run
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
            ResultsView?.Refresh();
            if (HudView != null && GameHud != null) HudView.Refresh(GameHud.Draw(ReadHud()));
            FloatTextView?.Refresh(State.View.X, State.View.Y);
            PlaceCamera(ViewCamera, State.View);
            PlaceCamera(MinimapCamera, State.Minimap);
            if (MinimapCamera != null) MinimapCamera.enabled = State.Minimap.Visible;
            PlaceLayers();
            DrawFlashbombs();
            if (ReflectionView != null) ReflectionView.Draw(State.Reflection, Player, ToWorld(State.Reflection.X, State.Reflection.Y));
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

            // obj_game's Create: healthbar, weapon_gui, energybar, speedometer (U.HARE-T25).
            GameHud = new GameHud(content.Arena.Hud, content.Health, content.Arena.HudWidth, content.Arena.HudHeight);
            HudView = GameHudView.Show(Hud, GameHud);

            // The room's obj_float_text, drawn where view 0 shows them, under the HUD objects (U.HARE-T27).
            FloatTexts = new FloatTextField(content.Popups.FloatText);
            FloatTextView = FloatTextView.Show(Hud, FloatTexts, content.Arena.HudWidth, content.Arena.HudHeight);
        }

        /// <summary>
        /// What the HUD objects read: the ship (player.state, speed, HP, MAXHP, its ammo pools and weapons, obj_energybar's
        /// value), LIVES, COINS, obj_healthbar's strTime, use_metric, paused and keyboard_check(vk_control).
        /// </summary>
        public HudReading ReadHud()
        {
            var session = Session;
            var reading = new HudReading
            {
                Clock = Clock.Text,
                Coins = session.Coins,
                Lives = session.Lives,
                UseMetric = session.UseMetric,
                Paused = Gml.Paused,
                MaxEnergy = content.Flight.MaxEnergy,
                Score = session.Score,
                Objective = session.ObjectiveText,
                Debug = session.Debug,
                Fps = Fps,
                TrackName = session.TrackName,
            };
            if (Player == null) return reading;
            var ship = PlayerSeat.Of(Player.gameObject).Ship;
            reading.HasPlayer = true;
            if (reading.Debug)
            {
                var at = PlayerRoomPosition();
                reading.X = at.x;
                reading.Y = at.y;
                reading.Direction = ship.Flight.Direction;
                reading.ImageAngle = ship.Flight.Facing;
                reading.Spin = ship.Flight.Spin / roomDefinition.StepsPerSecond; // spin is degrees a step in GML
                if (ship.Flight.CurrentRock.HasValue && Player.Perch is Rock rock && rock != null)
                {
                    var rockAt = ToRoom(rock.Position);
                    reading.Rock = ship.Flight.CurrentRock.Value;
                    reading.RockX = rockAt.x;
                    reading.RockY = rockAt.y;
                    reading.RockSpin = rock.Spin;
                }
            }
            reading.State = ship.Flight.State;
            reading.Speed = ship.Flight.Speed;
            reading.Energy = ship.Flight.Energy;
            reading.MaxEnergy = ship.Flight.Tuning.MaxEnergy;
            reading.Health = ship.Health.Health;
            reading.MaxHealth = ship.Health.MaxHealth;
            reading.Lives = ship.Health.Lives;
            reading.ControlHeld = Player.Controls.Player.Alternate.IsPressed();
            reading.GroundWeapon = ship.Ground.Current;
            reading.AirWeapon = ship.Air.Current;
            reading.GroundPrimaryAmmo = ship.Ammo.Current(AmmoPool.GroundPrimary);
            reading.GroundPrimaryMax = ship.Ammo.Max(AmmoPool.GroundPrimary);
            reading.GroundSecondaryAmmo = ship.Ammo.Current(AmmoPool.GroundSecondary);
            reading.GroundSecondaryMax = ship.Ammo.Max(AmmoPool.GroundSecondary);
            reading.AirPrimaryAmmo = ship.Ammo.Current(AmmoPool.AirPrimary);
            reading.AirPrimaryMax = ship.Ammo.Max(AmmoPool.AirPrimary);
            reading.AirSecondaryAmmo = ship.Ammo.Current(AmmoPool.AirSecondary);
            reading.AirSecondaryMax = ship.Ammo.Max(AmmoPool.AirSecondary);
            return reading;
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
                if (background == null) Debug.LogWarning("[Unity.Rea.Hare] arena: background " + layer.Name + " is not imported.");
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
