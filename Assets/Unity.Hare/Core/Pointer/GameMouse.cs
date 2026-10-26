using Unity.Hare.Flow;
using Unity.Hare.Player;

namespace Unity.Hare.Pointer
{
    /// <summary>
    /// obj_mouse (U.HARE-T61), engine-free, in room pixels (y down). Create: <c>on_gui = false</c>, its sprite
    /// spr_reticle_blue at depth -5000; the room's controller then sets the sprite and visibility
    /// (<see cref="MouseRoomDefinition"/>). Step: <c>if(!on_gui) { x = mouse_x; y = mouse_y } else { x = mouse_x -
    /// view_xview; y = mouse_y - view_yview }</c>, with no paused check, so it follows the mouse while the game is paused.
    /// Draw: draw_self; Draw GUI: draw_self while on_gui. obj_player's Step recolours it (<see cref="FollowShip"/>).
    /// </summary>
    public sealed class GameMouse
    {
        public GameMouse(MouseTuning tuning, MouseRoomDefinition room)
        {
            Tuning = tuning ?? new MouseTuning();
            Room = room;
            Sprite = room != null && !string.IsNullOrEmpty(room.Sprite) ? room.Sprite : Tuning.Sprite;
            Visible = room == null || room.Visible;
        }

        /// <summary>The obj_mouse <paramref name="room"/>'s controller creates, or null when it creates none.</summary>
        public static GameMouse ForRoom(MouseTuning tuning, RoomId room)
        {
            var definition = tuning?.RoomFor(room);
            return definition == null ? null : new GameMouse(tuning, definition);
        }

        public MouseTuning Tuning { get; }
        /// <summary>The room it was made for (null for one made outside any room's setup).</summary>
        public MouseRoomDefinition Room { get; }
        /// <summary>GML sprite_index.</summary>
        public string Sprite { get; private set; }
        /// <summary>GML visible.</summary>
        public bool Visible { get; private set; }
        /// <summary>GML on_gui: obj_yesno_dialog sets it while it is up, so the pointer draws in GUI space.</summary>
        public bool OnGui { get; private set; }
        /// <summary>GML x, y: room pixels, or GUI pixels while <see cref="OnGui"/>.</summary>
        public double X { get; private set; }
        public double Y { get; private set; }
        /// <summary>Whether the room's ship recolours it (the play rooms).</summary>
        public bool FollowsShip => Room != null && Room.FollowsShip;
        /// <summary>The depth it draws at.</summary>
        public int Depth => Tuning.Depth;

        /// <summary>obj_logo's Create: <c>obj_mouse.visible = true</c> (and any other <c>visible</c> write).</summary>
        public void SetVisible(bool visible) => Visible = visible;

        /// <summary>obj_yesno_dialog's Create (<c>mouse.on_gui = true</c>) and obj_btn_no (<c>mouse.on_gui = false</c>).</summary>
        public void SetOnGui(bool onGui) => OnGui = onGui;

        /// <summary>A controller's <c>mouse.sprite_index = ...</c>.</summary>
        public void SetSprite(string sprite)
        {
            if (!string.IsNullOrEmpty(sprite)) Sprite = sprite;
        }

        /// <summary>
        /// One Step: the mouse at room pixel (<paramref name="mouseX"/>, <paramref name="mouseY"/>) with view 0's top left at
        /// (<paramref name="viewX"/>, <paramref name="viewY"/>).
        /// </summary>
        public void Step(double mouseX, double mouseY, double viewX, double viewY)
        {
            if (!OnGui)
            {
                X = mouseX;
                Y = mouseY;
            }
            else
            {
                X = mouseX - viewX;
                Y = mouseY - viewY;
            }
        }

        /// <summary>
        /// obj_player's Step, after <c>mouse_angle = point_direction(x, y, mouse_x, mouse_y)</c>: <c>if(state !=
        /// state_moving)</c> spr_reticle_blue when the aim is within jump_arc of image_angle (can_jump), else
        /// spr_reticle_red; in the air spr_reticle_green. The block sits inside <c>if(state != state_leave) { if(!paused)
        /// {</c>, so a leaving ship (and the paused game) leaves the colour as it was; a dead ship (state_dead is not
        /// state_moving) still takes the arc test.
        /// </summary>
        public void FollowShip(PlayerFlightState state, bool withinJumpArc)
        {
            var sprite = ReticleFor(Tuning, state, withinJumpArc);
            if (sprite != null) Sprite = sprite;
        }

        /// <summary>The reticle obj_player's Step leaves on obj_mouse for a ship in <paramref name="state"/>; null for no change.</summary>
        public static string ReticleFor(MouseTuning tuning, PlayerFlightState state, bool withinJumpArc)
        {
            tuning = tuning ?? new MouseTuning();
            switch (state)
            {
                case PlayerFlightState.Moving: return tuning.FlyingSprite;
                case PlayerFlightState.Idle:
                case PlayerFlightState.Dead: return withinJumpArc ? tuning.CanLeapSprite : tuning.CannotLeapSprite;
                default: return null;
            }
        }

        /// <summary>
        /// obj_player's can_jump test: <c>abs(mouse_angle - image_angle) &lt;= jump_arc || abs(mouse_angle - image_angle)
        /// &gt;= 360 - jump_arc</c>, the same test as <see cref="ShipFlight.CanLeap"/> without its perched check.
        /// </summary>
        public static bool WithinJumpArc(float facing, float aimDirection, float jumpArcDegrees) =>
            System.Math.Abs(Maths.DeltaAngle(facing, aimDirection)) <= jumpArcDegrees;
    }
}
