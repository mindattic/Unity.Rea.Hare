using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Popups;
using Unity.Rea.Hare.Story;

namespace Unity.Rea.Hare.Map
{
    /// <summary>The way obj_map turns the earth (GML's global <c>rotate_direction</c>).</summary>
    public enum MapTurn
    {
        /// <summary>A: <c>obj_earthball.image_angle -= turn_speed</c>, the sky scrolling left.</summary>
        Counterclockwise,
        /// <summary>D: <c>image_angle += turn_speed</c>, the sky scrolling right.</summary>
        Clockwise,
    }

    /// <summary>obj_map's <c>mode</c>.</summary>
    public enum MapMode
    {
        Idle,
        Moving,
    }

    /// <summary>One mission marker on the map: obj_alert's rules and what it launches.</summary>
    public sealed class MapMission
    {
        internal MapMission(int index, AlertMission definition, AlertMarker marker, bool locked)
        {
            Index = index;
            Definition = definition;
            Marker = marker;
            Locked = locked;
        }

        /// <summary>Its place in obj_map's <c>mission[]</c>.</summary>
        public int Index { get; }
        public AlertMission Definition { get; }
        /// <summary>obj_alert.</summary>
        public AlertMarker Marker { get; }
        public int Scenario => Definition.ScenarioId;
        public int Level => Definition.Level;
        /// <summary>The boss's mission while any other is not yet won (owner-directed, docs/BIBLE.md V42).</summary>
        public bool Locked { get; }
    }

    /// <summary>
    /// obj_white_dot (spr_dropdot, depth 1): made at the ship by obj_map's launch, it heads for obj_destination a pixel a
    /// step (<c>move_towards_point(destX, destY, 1)</c>), fading by <c>fade_speed</c> as it goes, until
    /// <c>distance_to_point(destX, destY)</c> (from its turned bounding box) is no more than 1, then
    /// <c>room_goto(room_intro)</c>.
    /// </summary>
    public sealed class MapWhiteDot
    {
        readonly MapTuning tuning;

        internal MapWhiteDot(MapTuning tuning, double x, double y, double destX, double destY)
        {
            this.tuning = tuning;
            X = x;
            Y = y;
            DestX = destX;
            DestY = destY;
            Direction = PointDirection(x, y, destX, destY);
            ImageAngle = Direction;
            Alpha = 1d;
        }

        public double X { get; private set; }
        public double Y { get; private set; }
        public double DestX { get; }
        public double DestY { get; }
        /// <summary>GML direction (degrees anticlockwise, y down).</summary>
        public double Direction { get; private set; }
        /// <summary><c>whitedot.image_angle = whitedot.direction</c>, set once by obj_map.</summary>
        public double ImageAngle { get; }
        public double ImageIndex { get; private set; }
        /// <summary>GML image_alpha (it may go below 0; draw_self holds it at 0).</summary>
        public double Alpha { get; private set; }
        public int Frame => tuning.DotFrames <= 0 ? 0 : (int)Math.Floor(ImageIndex) % tuning.DotFrames;
        public float DrawAlpha => (float)Math.Max(0d, Math.Min(1d, Alpha));
        public bool Arrived { get; private set; }

        /// <summary>GML distance_to_point: from the instance's bounding box (turned by image_angle) to the point.</summary>
        public double DistanceToPoint(double px, double py)
        {
            tuning.DotMask.Bounds(X, Y, ImageAngle, out int left, out int top, out int right, out int bottom);
            double dx = px < left ? left - px : px > right ? px - right : 0d;
            double dy = py < top ? top - py : py > bottom ? py - bottom : 0d;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>One step; true the step it arrives (and every step after).</summary>
        internal bool Step()
        {
            if (Arrived) return true;
            if (DistanceToPoint(DestX, DestY) > tuning.DotArriveDistance)
            {
                Direction = PointDirection(X, Y, DestX, DestY);
                X += AlertMarker.LengthDirX(tuning.DotSpeed, Direction);
                Y += AlertMarker.LengthDirY(tuning.DotSpeed, Direction);
                Alpha -= tuning.DotFadeSpeed;
            }
            else Arrived = true;
            if (tuning.DotFrames > 0)
            {
                ImageIndex += tuning.DotImageSpeed;
                if (ImageIndex >= tuning.DotFrames) ImageIndex -= tuning.DotFrames;
            }
            return Arrived;
        }

        /// <summary>GML point_direction: degrees anticlockwise from the x axis, y down, 0..360.</summary>
        public static double PointDirection(double x1, double y1, double x2, double y2)
        {
            double degrees = Math.Atan2(-(y2 - y1), x2 - x1) * 180d / Math.PI;
            return degrees < 0d ? degrees + 360d : degrees;
        }
    }

    /// <summary>
    /// room_map, engine-free (U.HARE-T19): obj_map's mission select over obj_earthball, with obj_destination, the mission
    /// markers (obj_alert), obj_white_dot, obj_scenario_details, obj_btn_upgrade, the comm box (obj_conversation) and
    /// the tip. There is no global instance: the map room's view owns one. State changes only through
    /// <see cref="Execute"/>; presentation draws the state and plays the events (sounds, pings, the tip, the session's
    /// scenario and mission, the room change) and never writes back.
    ///
    /// A and D (only while idle and before a launch) set <c>mode = "moving"</c> and the turn; each step obj_destination
    /// clears the scenario, hides the panel and turns the earth 2 degrees until it touches (pixel-exact, spr_destination's
    /// strip against spr_alertmarker's frame) a marker it did not last stop on, which idles the map, stops the sky, shows
    /// "Scenario N" and slides the comm box in. Any other key, with a scenario reached, sends the white dot from the
    /// ship to obj_destination; when it arrives the map goes to room_intro.
    ///
    /// Owner-directed changes (docs/BIBLE.md V42): there is a marker for every one of the original's ten levels, and the
    /// dot's arrival hands the reached mission and its level on (<see cref="MapMissionChosen"/>), where the original
    /// discarded it and room_intro always started a quick game at level 1; the boss's mission is locked until every
    /// other is won. Fixed defect (D24): entering the map clears an old <c>current_scenario</c>, so a key cannot launch a
    /// mission no marker shows.
    ///
    /// Each step runs as GameMaker orders it: the markers' Alarm 0 (pings, while idle), obj_earthball's Step (idle,
    /// <c>image_angle -= auto_speed</c>), obj_scanner's Step (its sway and flicker, <see cref="MapScanner"/>),
    /// obj_destination's Step (moving: the turn), the markers' Step (their place on
    /// the rim), obj_white_dot's Step, then obj_destination's collision with obj_alert, then background_x[0] by its
    /// hspeed.
    /// </summary>
    public sealed class MapScreen
    {
        readonly List<MapEvent> events = new List<MapEvent>();
        readonly List<MapMission> missions = new List<MapMission>();
        readonly HashSet<int> won;
        readonly Func<int, string> levelName;
        readonly IRandomSource random;
        PlayerId issuer;

        /// <param name="tuning">The map data (the registry's <c>map</c> section).</param>
        /// <param name="alert">The markers and their missions (the registry's <c>popups.Alert</c>).</param>
        /// <param name="conversation">The comm box obj_map makes, or null (none drawn).</param>
        /// <param name="wonScenarios">The missions already won (the campaign's save), for the boss's lock.</param>
        /// <param name="levelName">The level_name of a level (the arena section's), for the panel.</param>
        /// <param name="roomWidth">room_width, 1920.</param>
        /// <param name="roomHeight">room_height, 1080.</param>
        /// <param name="random">Where obj_scanner's flicker (irandom_range) comes from; null for a fixed seed.</param>
        public MapScreen(MapTuning tuning, AlertDefinition alert, ConversationBox conversation, IEnumerable<int> wonScenarios,
            Func<int, string> levelName, int roomWidth, int roomHeight, IRandomSource random = null)
        {
            this.random = random ?? new SeededRandom(1);
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Alert = alert ?? throw new ArgumentNullException(nameof(alert));
            Conversation = conversation;
            won = new HashSet<int>(wonScenarios ?? Array.Empty<int>());
            this.levelName = levelName ?? (_ => "");
            RoomWidth = roomWidth;
            RoomHeight = roomHeight;
        }

        public MapTuning Tuning { get; }
        public AlertDefinition Alert { get; }
        public int RoomWidth { get; }
        public int RoomHeight { get; }

        /// <summary>obj_conversation, or null.</summary>
        public ConversationBox Conversation { get; }

        /// <summary>Whether the room's Create has run.</summary>
        public bool Created { get; private set; }

        /// <summary>obj_map's mode.</summary>
        public MapMode Mode { get; private set; } = MapMode.Idle;

        /// <summary>GML's global rotate_direction (globals.gml starts it counterclockwise).</summary>
        public MapTurn Turning { get; private set; } = MapTurn.Counterclockwise;

        /// <summary>GML's global current_mission, cycled by A and D over the missions (nothing else reads it).</summary>
        public int CurrentMission { get; private set; }

        /// <summary>GML's global current_scenario: the reached mission's scenario, -1 for none.</summary>
        public int CurrentScenario { get; private set; } = -1;

        /// <summary>obj_destination's last_scenario.</summary>
        public int LastScenario { get; private set; } = -1;

        /// <summary>obj_earthball's x, y and image_angle.</summary>
        public double EarthX { get; private set; }
        public double EarthY { get; private set; }
        public double EarthAngle { get; private set; }

        /// <summary>obj_earthball's auto_speed (0 once the dot is launched).</summary>
        public double AutoSpeed { get; private set; }

        /// <summary>obj_uss_warren's place.</summary>
        public double ShipX { get; private set; }
        public double ShipY { get; private set; }

        /// <summary>obj_scanner (made by Create; U.HARE-T63).</summary>
        public MapScanner Scanner { get; private set; }

        /// <summary>obj_destination's place (never drawn).</summary>
        public double DestinationX { get; private set; }
        public double DestinationY { get; private set; }

        /// <summary>background_hspeed[0] and background_x[0] (bg_nightsky, tiled).</summary>
        public int SkySpeed { get; private set; }
        public int SkyX { get; private set; }

        /// <summary>obj_scenario_details' visible and text (GML: # is a new line).</summary>
        public bool PanelVisible { get; private set; }
        public string PanelText { get; private set; } = "";
        public string PanelDrawText => GmlString.Lines(PanelText);

        /// <summary>obj_btn_upgrade's mouseover.</summary>
        public bool UpgradeHovered { get; private set; }

        /// <summary>obj_white_dot, once launched (whitedot_exists).</summary>
        public MapWhiteDot Dot { get; private set; }

        /// <summary>The dot arrived and the room is on its way to room_intro.</summary>
        public bool Left { get; private set; }

        /// <summary>The mission markers, in obj_map's mission[] order.</summary>
        public IReadOnlyList<MapMission> Missions => missions;

        /// <summary>The mission whose scenario is current, or null.</summary>
        public MapMission Reached => FindScenario(CurrentScenario);

        /// <summary>Whether the boss's mission is still locked: some other mission is not yet won.</summary>
        public bool BossLocked
        {
            get
            {
                if (Alert.Missions == null) return false;
                foreach (var m in Alert.Missions)
                    if (m != null && !m.Boss && !won.Contains(m.ScenarioId)) return true;
                return false;
            }
        }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public MapResult Execute(MapCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<MapEvent>() : events.ToArray();
            events.Clear();
            return new MapResult(accepted, raised);
        }

        void Emit(MapEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        MapMission FindScenario(int scenario)
        {
            if (scenario < 0) return null;
            foreach (var m in missions)
                if (m.Scenario == scenario) return m;
            return null;
        }

        // ---- Rules the commands call ----

        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            var t = Tuning;
            // obj_earthball: x = room_width/2; y = room_height; turn_speed = 2; auto_speed = 0.01.
            EarthX = RoomWidth / 2d;
            EarthY = t.EarthY(RoomHeight);
            EarthAngle = 0d;
            AutoSpeed = t.AutoSpeed;
            ShipX = t.ShipX;
            ShipY = t.ShipY(RoomHeight);
            // obj_scanner: y = room_height - earth/2 - spr_scanner_beam + 50; image_alpha = 0.2; the sweep at rest.
            Scanner = new MapScanner(t, RoomHeight, random);
            // obj_destination: last_scenario = -1; y = room_height - sprite_get_height(spr_earthball)/2.
            DestinationX = t.DestinationX;
            DestinationY = t.DestinationY(RoomHeight);
            LastScenario = -1;
            // obj_map: mode = "idle", the missions, the comm box (mode "off") and the tip.
            Mode = MapMode.Idle;
            SkySpeed = 0;
            SkyX = 0;
            bool bossLocked = BossLocked;
            if (Alert.Missions != null)
                for (int i = 0; i < Alert.Missions.Length; i++)
                {
                    var m = Alert.Missions[i];
                    missions.Add(new MapMission(i, m, new AlertMarker(Alert, m.Degree, m.ScenarioId), m.Boss && bossLocked));
                }
            if (CurrentMission >= missions.Count) CurrentMission = 0;
            // D24: current_scenario is a global the original never clears here, so a scenario reached before the map was
            // left (for the garage) would launch with no marker under obj_destination. The map starts with none.
            CurrentScenario = -1;
            Emit(new MapScenarioSelected { Scenario = -1 });
            Emit(new MapTipRequested());
            // obj_scenario_details: visible = false.
            PanelVisible = false;
            PanelText = "";
            return true;
        }

        internal bool Step()
        {
            if (!Created) return false;
            bool idleAtAlarms = Mode == MapMode.Idle;

            // obj_earthball's Step.
            if (Mode == MapMode.Idle) EarthAngle = StoryScreen.NormalizeAngle(EarthAngle - AutoSpeed);

            // obj_scanner's Step: the sway and the flicker, whatever the map does.
            Scanner.Step();

            // obj_destination's Step.
            if (Mode == MapMode.Moving)
            {
                PanelVisible = false;
                SetScenario(-1);
                EarthAngle += Turning == MapTurn.Counterclockwise ? -Tuning.TurnSpeed : Tuning.TurnSpeed;
                EarthAngle = StoryScreen.NormalizeAngle(EarthAngle);
            }

            // The markers: Alarm 0 (they ran before every Step, as GameMaker runs alarms), then their Step.
            foreach (var m in missions)
                if (m.Marker.Step(EarthX, EarthY, EarthAngle, idleAtAlarms, out double px, out double py))
                    Emit(new MapMarkerPinged { Mission = m.Index, X = px, Y = py, Size = Alert.PingSize, Color = Alert.PingColor });

            // obj_white_dot's Step.
            if (Dot != null && !Left && Dot.Step())
            {
                Left = true;
                var reached = Reached;
                if (reached != null) Emit(new MapMissionChosen { Scenario = reached.Scenario, Level = reached.Level });
                Emit(new MapRoomRequested { Room = Tuning.LaunchRoom });
            }

            // obj_destination's collision with obj_alert.
            foreach (var m in missions)
            {
                if (m.Scenario == LastScenario) continue;
                if (!GmlPreciseMask.Collide(Tuning.DestinationMask, 0, DestinationX, DestinationY, 0d,
                        Tuning.MarkerMask, m.Marker.Frame, m.Marker.X, m.Marker.Y, m.Marker.ImageAngle)) continue;
                LastScenario = m.Scenario;
                Mode = MapMode.Idle;
                SkySpeed = 0;
                PanelVisible = true;
                PanelText = PanelFor(m);
                SetScenario(m.Scenario);
                SlideConversation(true);
                Emit(new MapMissionReached { Mission = m.Index, Scenario = m.Scenario, Level = m.Level, Locked = m.Locked });
            }

            SkyX += SkySpeed;
            return true;
        }

        /// <summary>The panel's text for <paramref name="mission"/>: "Scenario N", its level's name, and the lock.</summary>
        public string PanelFor(MapMission mission)
        {
            string text = string.Format(CultureInfo.InvariantCulture, Tuning.PanelText ?? "", mission.Scenario, levelName(mission.Level) ?? "");
            return mission.Locked ? text + Tuning.LockedText : text;
        }

        internal bool Turn(MapTurn turn)
        {
            if (!Created || Mode != MapMode.Idle || Dot != null) return false;
            Emit(new MapSoundRequested { Sound = Tuning.SwitchSound });
            Mode = MapMode.Moving;
            Turning = turn;
            int count = Math.Max(1, missions.Count);
            if (turn == MapTurn.Clockwise)
            {
                // D: background_hspeed[0] = 1; if(current_mission > 0) current_mission -= 1; else current_mission = mission_count-1.
                SkySpeed = Tuning.SkyScroll;
                CurrentMission = CurrentMission > 0 ? CurrentMission - 1 : count - 1;
            }
            else
            {
                // A: background_hspeed[0] = -1; if(current_mission < mission_count-1) current_mission += 1; else current_mission = 0.
                SkySpeed = -Tuning.SkyScroll;
                CurrentMission = CurrentMission < count - 1 ? CurrentMission + 1 : 0;
            }
            SlideConversation(false);
            return true;
        }

        internal bool Launch(bool aHeld, bool dHeld, bool escapeHeld)
        {
            if (!Created || aHeld || dHeld || escapeHeld) return false;
            if (CurrentScenario == -1 || Dot != null) return false;
            var reached = Reached;
            if (reached != null && reached.Locked)
            {
                Emit(new MapLaunchRefused { Scenario = reached.Scenario });
                return false;
            }
            Dot = new MapWhiteDot(Tuning, ShipX, ShipY, DestinationX, DestinationY);
            AutoSpeed = 0d;
            Emit(new MapSoundRequested { Sound = Tuning.LaunchSound });
            Emit(new MapLaunched { Scenario = CurrentScenario, Level = reached?.Level ?? 0 });
            return true;
        }

        internal bool Point(double x, double y)
        {
            if (!Created) return false;
            bool over = Tuning.Upgrade.Contains(x, y);
            if (over == UpgradeHovered) return false;
            UpgradeHovered = over;
            if (over) Emit(new MapSoundRequested { Sound = Tuning.Upgrade.HoverSound });
            Emit(new MapHoverChanged { Hovered = over });
            return true;
        }

        internal bool Press(double x, double y)
        {
            if (!Created || !Tuning.Upgrade.Contains(x, y)) return false;
            Emit(new MapSoundRequested { Sound = Tuning.Upgrade.ClickSound });
            Emit(new MapFadeRequested { Room = Tuning.Upgrade.Room });
            return true;
        }

        void SetScenario(int scenario)
        {
            if (CurrentScenario == scenario) return;
            CurrentScenario = scenario;
            Emit(new MapScenarioSelected { Scenario = scenario });
        }

        void SlideConversation(bool slideIn)
        {
            if (Conversation != null)
            {
                if (slideIn) Conversation.SlideIn();
                else Conversation.SlideOut();
            }
            Emit(new MapConversationSlid { In = slideIn });
        }
    }

    /// <summary>What <see cref="MapScreen.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct MapResult
    {
        public MapResult(bool accepted, IReadOnlyList<MapEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<MapEvent> Events { get; }
    }
}
