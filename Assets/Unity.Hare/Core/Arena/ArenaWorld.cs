using System;
using System.Collections.Generic;
using Unity.Hare.Flow;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// One of the room's background slots as it stands: what is in it, how it is drawn, and where GameMaker has scrolled
    /// it to (background_x/background_y, pixels, y growing downward).
    /// </summary>
    public sealed class ArenaLayer
    {
        internal ArenaLayer(int index) => Index = index;

        /// <summary>The slot (GML: the i in background_index[i]); 0 is drawn at the back.</summary>
        public int Index { get; }
        /// <summary>The GML background, empty when the slot holds none.</summary>
        public string Name { get; internal set; } = "";
        public bool Visible { get; internal set; }
        public bool HTiled { get; internal set; }
        public bool VTiled { get; internal set; }
        public bool Stretch { get; internal set; }
        /// <summary>GML: background_x[i].</summary>
        public int X { get; internal set; }
        /// <summary>GML: background_y[i].</summary>
        public int Y { get; internal set; }
        /// <summary>GML: background_hspeed[i], pixels a step.</summary>
        public int HSpeed { get; internal set; }
        /// <summary>GML: background_vspeed[i], pixels a step.</summary>
        public int VSpeed { get; internal set; }

        /// <summary>Whether anything is drawn for this slot: it is visible and holds a background.</summary>
        public bool Drawn => Visible && !string.IsNullOrEmpty(Name);
    }

    /// <summary>
    /// One of the room's views as it stands: where in the room it looks (view_xview/view_yview, pixels from the room's top
    /// left) and whether it is drawn (view_visible).
    /// </summary>
    public sealed class ArenaView
    {
        internal ArenaView(ArenaViewDefinition definition) => Definition = definition;

        public ArenaViewDefinition Definition { get; }
        /// <summary>GML: view_xview.</summary>
        public float X { get; internal set; }
        /// <summary>GML: view_yview.</summary>
        public float Y { get; internal set; }
        /// <summary>GML: view_visible.</summary>
        public bool Visible { get; internal set; }
        public int Width => Definition.Width;
        public int Height => Definition.Height;
        /// <summary>The middle of what the view shows, room pixels.</summary>
        public float CenterX => X + Width / 2f;
        public float CenterY => Y + Height / 2f;

        /// <summary>
        /// GameMaker's view following its object, once a step (the runner's view update, after the Step events): the view
        /// centres on the object when its border is half the view or more, otherwise moves just enough to keep the object
        /// the border's distance from its edges; it is then kept inside the room (its left/top edge last, so a view wider
        /// than the room shows the room's left/top), and moves at most hspeed/vspeed pixels this step (-1: no limit). The
        /// object's position counts in whole pixels.
        /// </summary>
        internal void Follow(float targetX, float targetY, int roomWidth, int roomHeight)
        {
            X = FollowAxis(X, (int)targetX, Width, Definition.HBorder, Definition.HSpeed, roomWidth);
            Y = FollowAxis(Y, (int)targetY, Height, Definition.VBorder, Definition.VSpeed, roomHeight);
        }

        /// <summary>One axis of <see cref="Follow"/>: the view's new start along it.</summary>
        public static float FollowAxis(float current, int target, int size, int border, int speed, int roomSize)
        {
            float start = current;
            if (2 * border >= size) start = target - size / 2f;
            else if (target - border < current) start = target - border;
            else if (target + border > current + size) start = target + border - size;

            if (start + size > roomSize) start = roomSize - size;
            if (start < 0f) start = 0f;

            if (speed >= 0)
            {
                if (start < current && current - start > speed) start = current - speed;
                if (start > current && start - current > speed) start = current + speed;
            }
            return start;
        }
    }

    /// <summary>
    /// The arena, engine-free: a play room (room_gamescreen and the side-scroll and shaft rooms) as obj_game, the room's
    /// views and backgrounds and obj_player's boundary rules make it. There is no global instance: the room's view owns
    /// one. State changes only through <see cref="Execute"/>, which runs an <see cref="ArenaCommand"/> and returns the
    /// <see cref="ArenaEvent"/>s it raised; presentation draws the <see cref="Layers"/> and <see cref="Views"/> and plays the
    /// events, and never writes back. All positions are GameMaker's: room pixels from the top left, y growing downward.
    ///
    /// The original, read from room_gamescreen.room.gmx (and its two sibling rooms), obj_game, obj_player's Step and Draw,
    /// the level scripts, setup_level, reset_player and random_positions:
    /// <list type="bullet">
    /// <item>The room is room_width x room_height (3840x2160 for room_gamescreen) at room_speed 120. The level script fills
    /// background slot 0 (and slot 1, noone except for the generator) and sets slot 0's background_hspeed/vspeed, which
    /// GameMaker adds to background_x/background_y every step; setup_level put both back at 0 first.</item>
    /// <item>View 0 (1920x1080) follows obj_player, centred on it (its borders are as large as the view), kept inside the
    /// room, at most 40 px a step in room_gamescreen. View 1, the minimap (3000x2000 drawn 300x200), follows it the same
    /// way with no speed limit, drawn only while show_map is set: obj_game's Create sets view_visible[1] from show_map and
    /// the M key flips it.</item>
    /// <item>obj_player's Step: with loop_horizontal, past <c>-sprite_width</c> it reappears at <c>room_width + sprite_width</c>
    /// and past <c>room_width + sprite_width</c> at <c>-sprite_width</c>; without, past <c>-buffer_zone</c> or
    /// <c>room_width + buffer_zone</c> it costs a life (<c>if(LIVES &gt; 0) LIVES -= 1; else game_restart();</c>) and
    /// reset_player puts it at <c>(room_width / 2, -sprite_height)</c>. Vertically the same, except that a loop_vertical wrap
    /// happens past the buffer zone (to <c>room_height + sprite_height</c> or <c>-sprite_height</c>). Every wrap with
    /// randomize_on_loop runs random_positions: every rock, structure and enemy, in that order, moves to
    /// <c>(irandom_range(0, room_width), irandom_range(0, room_height))</c>.</item>
    /// </list>
    /// </summary>
    public sealed class ArenaWorld
    {
        readonly List<ArenaEvent> events = new List<ArenaEvent>();
        readonly ArenaLayer[] layers;
        readonly ArenaView[] views;
        readonly IRandomSource random;
        PlayerId issuer;

        /// <param name="tuning">The arena data (the registry's <c>arena</c> section).</param>
        /// <param name="room">The room's size and speed (the registry's <c>rooms</c> section).</param>
        /// <param name="setting">The level script that set the room up.</param>
        /// <param name="random">Where random_positions' irandom_range comes from.</param>
        public ArenaWorld(ArenaTuning tuning, RoomDefinition room, LevelSetting setting, IRandomSource random)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Room = room ?? throw new ArgumentNullException(nameof(room));
            Setting = setting ?? throw new ArgumentNullException(nameof(setting));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            Definition = tuning.RoomFor(room.Room) ?? throw new ArgumentException($"{room.Room} is not a play room.", nameof(room));

            layers = new ArenaLayer[Definition.Backgrounds.Length];
            for (int i = 0; i < layers.Length; i++) layers[i] = new ArenaLayer(i);
            views = new[] { new ArenaView(Definition.View), new ArenaView(Definition.Minimap) };
            Reflection = new SurfaceReflection(Definition.Reflection, Room.Height);
            SetUp();
        }

        /// <summary>The room's obj_surface_reflection (U.HARE-T58); <see cref="SurfaceReflection.Active"/> is false in a room without one.</summary>
        public SurfaceReflection Reflection { get; }

        public ArenaTuning Tuning { get; }
        public RoomDefinition Room { get; }
        public ArenaRoomDefinition Definition { get; }
        public LevelSetting Setting { get; }

        /// <summary>GML: room_width.</summary>
        public int Width => Room.Width;
        /// <summary>GML: room_height.</summary>
        public int Height => Room.Height;

        /// <summary>The background slots, back to front.</summary>
        public IReadOnlyList<ArenaLayer> Layers => layers;

        /// <summary>View 0 then view 1.</summary>
        public IReadOnlyList<ArenaView> Views => views;
        /// <summary>View 0, the screen.</summary>
        public ArenaView View => views[0];
        /// <summary>View 1, the minimap.</summary>
        public ArenaView Minimap => views[1];

        /// <summary>GML: show_map.</summary>
        public bool ShowMap { get; private set; }

        /// <summary>Whether obj_game's Create has run.</summary>
        public bool Created { get; private set; }

        public bool LoopHorizontal => Setting.LoopHorizontal;
        public bool LoopVertical => Setting.LoopVertical;
        public bool RandomizeOnLoop => Setting.RandomizeOnLoop;

        /// <summary>reset_player's <c>x = room_width/2</c>.</summary>
        public float SpawnX => Width / 2;
        /// <summary>reset_player's <c>y = -sprite_get_height(asset_leap)</c>: just above the room.</summary>
        public float SpawnY => -Tuning.PlayerSize;

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public ArenaResult Execute(ArenaCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<ArenaEvent>() : events.ToArray();
            events.Clear();
            return new ArenaResult(accepted, raised);
        }

        void Emit(ArenaEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        /// <summary>The room's own backgrounds and views, then the level script's setting over them (setup_level's background_x/y = 0 included).</summary>
        void SetUp()
        {
            for (int i = 0; i < layers.Length; i++)
            {
                var slot = Definition.Backgrounds[i];
                var layer = layers[i];
                layer.Name = slot.Name ?? "";
                layer.Visible = slot.Visible;
                layer.HTiled = slot.HTiled;
                layer.VTiled = slot.VTiled;
                layer.Stretch = slot.Stretch;
                layer.X = 0;
                layer.Y = 0;
                layer.HSpeed = 0;
                layer.VSpeed = 0;
            }

            var back = layers[0];
            back.Name = Setting.Background ?? "";
            back.HSpeed = Setting.HSpeed;
            back.VSpeed = Setting.VSpeed;
            if (Setting.Tiled)
            {
                back.HTiled = true;
                back.VTiled = true;
            }
            if (layers.Length > 1) layers[1].Name = Setting.Overlay ?? "";

            foreach (var view in views)
            {
                view.X = 0f;
                view.Y = 0f;
            }
            View.Visible = true;
            Minimap.Visible = false;
        }

        // ---- Rules the commands call ----

        /// <summary>obj_game's Create: <c>view_visible[1] = show_map</c>. Once only.</summary>
        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            ShowMap = Tuning.ShowMapOnStart;
            Minimap.Visible = ShowMap;
            Emit(new MinimapToggled { Visible = ShowMap });
            return true;
        }

        /// <summary>One step's background scroll: <c>background_x[i] += background_hspeed[i]</c>, and the same for y.</summary>
        internal bool Step()
        {
            if (!Created) return false;
            foreach (var layer in layers)
            {
                layer.X += layer.HSpeed;
                layer.Y += layer.VSpeed;
            }
            return true;
        }

        /// <summary>The views' update after the Step events: each follows the player at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        internal bool Follow(float x, float y)
        {
            if (!Created) return false;
            foreach (var view in views) view.Follow(x, y, Width, Height);
            return true;
        }

        /// <summary>obj_game's M key: <c>show_map = !show_map; view_visible[1] = show_map</c>.</summary>
        internal bool ToggleMinimap()
        {
            ShowMap = !ShowMap;
            Minimap.Visible = ShowMap;
            Emit(new MinimapToggled { Visible = ShowMap });
            return true;
        }

        /// <summary>
        /// obj_player's Step, "Check Boundries", for a player at (<paramref name="x"/>, <paramref name="y"/>): the
        /// horizontal rule, then the vertical one on wherever the horizontal one left it.
        /// </summary>
        internal bool CheckPlayer(float x, float y)
        {
            int size = Tuning.PlayerSize;
            int buffer = Tuning.BufferZone;
            bool moved = false, left = false;

            if (LoopHorizontal)
            {
                if (x < -size) { x = Width + size; moved = true; Wrap(x, y, horizontal: true); }
                else if (x > Width + size) { x = -size; moved = true; Wrap(x, y, horizontal: true); }
            }
            else if (x < -buffer || x > Width + buffer)
            {
                x = SpawnX;
                y = SpawnY;
                left = true;
                Emit(new PlayerOutOfBounds { X = x, Y = y });
            }

            if (LoopVertical)
            {
                if (y < -buffer) { y = Height + size; moved = true; Wrap(x, y, horizontal: false); }
                else if (y > Height + buffer) { y = -size; moved = true; Wrap(x, y, horizontal: false); }
            }
            else if (y < -buffer || y > Height + buffer)
            {
                x = SpawnX;
                y = SpawnY;
                left = true;
                Emit(new PlayerOutOfBounds { X = x, Y = y });
            }

            return moved || left;
        }

        /// <summary>
        /// obj_surface_reflection's step for a ship at (<paramref name="x"/>, <paramref name="y"/>) turned to
        /// <paramref name="imageAngle"/> and heading <paramref name="direction"/>, in GameMaker's order: Alarm 0 (every
        /// rate steps, a <see cref="SurfaceSplashed"/> while the ship is within the splash band), then the Step (the
        /// reflection follows, and a ship below the water line is put back on it skimming level,
        /// <see cref="ShipSkimmedSurface"/>). Refused before Create and in a room without a reflection.
        /// </summary>
        internal bool StepReflection(float x, float y, float imageAngle, float direction)
        {
            if (!Created || !Reflection.Active) return false;
            var def = Reflection.Definition;
            if (Reflection.TickSplashAlarm() && Reflection.Splashes(y))
                Emit(new SurfaceSplashed { X = Reflection.SplashX(x, imageAngle), Y = Reflection.SurfaceY, Size = def.SplashSize, Color = def.SplashColor });

            Reflection.Follow(x, y, imageAngle);
            if (y > Reflection.SurfaceY)
                Emit(new ShipSkimmedSurface { X = x, Y = Reflection.SurfaceY, Direction = SurfaceReflection.SkimDirection(direction) });
            return true;
        }

        void Wrap(float x, float y, bool horizontal) =>
            Emit(new PlayerWrapped { X = x, Y = y, Horizontal = horizontal, RandomizePositions = RandomizeOnLoop });

        /// <summary>random_positions for <paramref name="count"/> objects, in the order the view lists them (rocks, structures, enemies).</summary>
        internal bool Scatter(int count)
        {
            if (count < 0) return false;
            var points = new Vec2[count];
            for (int i = 0; i < count; i++)
            {
                int px = random.IRandom(0, Width);
                int py = random.IRandom(0, Height);
                points[i] = new Vec2(px, py);
            }
            Emit(new PositionsScattered { Points = points });
            return true;
        }

        // ---- What obj_player's Draw reads ----

        /// <summary>Whether (<paramref name="x"/>, <paramref name="y"/>) is outside the room.</summary>
        public bool Outside(float x, float y) => x < 0f || x > Width || y < 0f || y > Height;

        /// <summary>
        /// obj_bullet's and obj_rocket's Step (U.HARE-T21): a projectile at (<paramref name="x"/>, <paramref name="y"/>)
        /// is destroyed once it is more than buffer_zone (400 px) outside this room on any side:
        /// <c>(x + buffer_zone &lt; 0) || (x - buffer_zone &gt; room_width) || (y + buffer_zone &lt; 0) || (y - buffer_zone &gt; room_height)</c>.
        /// The rule is the room's, so a shot crosses the whole 12000 px side-scroll or the 12000 px shaft before it goes;
        /// projectiles never wrap, whatever the level's loop flags.
        /// </summary>
        public bool OutOfPlay(float x, float y) => OutOfPlay(x, y, Width, Height, Tuning.BufferZone);

        /// <summary><see cref="OutOfPlay(float, float)"/> for any room size and buffer.</summary>
        public static bool OutOfPlay(float x, float y, int roomWidth, int roomHeight, int bufferZone) =>
            x + bufferZone < 0f || x - bufferZone > roomWidth || y + bufferZone < 0f || y - bufferZone > roomHeight;

        /// <summary>
        /// obj_player's Draw, "Draw Offscreen Arrow": where spr_big_arrow goes for a player at (<paramref name="x"/>,
        /// <paramref name="y"/>) â€” <paramref name="inset"/> pixels inside the edge it left by (left, then right, then top,
        /// then bottom), level with it along that edge â€” or false while it is inside the room.
        /// </summary>
        public static bool OffscreenArrow(float x, float y, int roomWidth, int roomHeight, int inset, out Vec2 at)
        {
            if (x < 0f) { at = new Vec2(inset, y); return true; }
            if (x > roomWidth) { at = new Vec2(roomWidth - inset, y); return true; }
            if (y < 0f) { at = new Vec2(x, inset); return true; }
            if (y > roomHeight) { at = new Vec2(x, roomHeight - inset); return true; }
            at = default;
            return false;
        }

        /// <summary>
        /// draw_dottedline(x1, y1, x2, y2, dash, ...): the dashes, each <paramref name="dash"/> pixels long along the line
        /// with a gap as long after it, the first at the start; the last may run past the end, as GameMaker draws it.
        /// Each dash is (from, to).
        /// </summary>
        public static List<(Vec2 From, Vec2 To)> DottedLine(Vec2 from, Vec2 to, float dash)
        {
            var dashes = new List<(Vec2, Vec2)>();
            if (!(dash > 0f)) return dashes;
            float dx = to.X - from.X, dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0f) return dashes;
            var step = new Vec2(dx / length * dash, dy / length * dash);
            int c = 0;
            for (float m = 0f; m < length; m += dash * 2f)
            {
                dashes.Add((from + step * c, from + step * (c + 1)));
                c += 2;
            }
            return dashes;
        }
    }

    /// <summary>What <see cref="ArenaWorld.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct ArenaResult
    {
        public ArenaResult(bool accepted, IReadOnlyList<ArenaEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<ArenaEvent> Events { get; }
    }
}
