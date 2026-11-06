using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Pointer;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Input
{
    /// <summary>
    /// obj_mouse drawn (U.HARE-T61): Core's <see cref="GameMouse"/> as one sprite at depth -5000 that sits on the mouse.
    /// The room flow makes one in every room whose controller creates one (<see cref="MouseTuning.Rooms"/>: the title's,
    /// the map's and the upgrade room's spr_pointer, the play rooms' reticle) and it goes with its room's scene.
    /// <para>
    /// Where the mouse is comes from the input path the weapons aim with: in a play room it is the ship's
    /// <see cref="PlayerShipController.AimPoint"/> (GML mouse_x/mouse_y, read from HareControls' Player/Aim), and the
    /// ship's state recolours it every step as obj_player's Step does (green in flight, blue within the jump arc, red
    /// outside it; held while paused). A room with no ship reads the same Player/Aim action itself. With no pointer to read
    /// (the aim comes from a gamepad's stick) it is not drawn: the original had no other way to aim.
    /// </para>
    /// It is a world sprite, so view 0's camera draws it (the minimap too, as GameMaker ran Draw once per view); while
    /// <see cref="GameMouse.OnGui"/> it stays where the mouse is on screen, which is where its Draw GUI put it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MouseView : MonoBehaviour
    {
        public const string ObjectName = "obj_mouse";

        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        HareControls controls;
        RoomDefinition roomDefinition;
        SpriteRenderer spriteRenderer;
        HareArtManifest manifest;

        /// <summary>The room's obj_mouse while it is up (GameMaker has one room, and each room one obj_mouse).</summary>
        public static MouseView Active { get; private set; }

        /// <summary>obj_mouse's state.</summary>
        public GameMouse State { get; private set; }

        /// <summary>The room it was made for.</summary>
        public RoomId Room { get; private set; }

        /// <summary>The sprite drawing it.</summary>
        public SpriteRenderer Renderer => spriteRenderer;

        /// <summary>Whether it is drawn this frame (visible, and a mouse to sit on), by its sprite or by <see cref="DrawOnUi"/>.</summary>
        public bool Drawn { get; private set; }

        /// <summary>The room pixel its sprite's origin is drawn at this frame.</summary>
        public Vector2 RoomPoint { get; private set; }

        /// <summary>
        /// A room whose objects are drawn on a UI Toolkit panel (the title, U.HARE-T26) draws obj_mouse there too, over
        /// that panel as depth -5000 is over every object: when set, the sprite renderer stays off and this is called
        /// every frame after the step, with <see cref="Drawn"/>, <see cref="RoomPoint"/> and <see cref="Renderer"/>'s
        /// sprite set. It is drawn once, there (BIBLE V38).
        /// </summary>
        public Action<MouseView> DrawOnUi;

        /// <summary>
        /// The obj_mouse <paramref name="room"/>'s controller creates, made in <paramref name="scene"/> so it goes with it;
        /// null when the room has none. With <paramref name="parent"/> (a room controller making its own, as obj_title's
        /// Create does) it is made under that object, so it is in the controller's scene even while that scene is loading.
        /// </summary>
        public static MouseView Create(RoomId room, Scene scene, Transform parent = null)
        {
            var content = GameContent.Current;
            var state = GameMouse.ForRoom(content.Mouse, room);
            if (state == null) return null;
            var go = new GameObject(ObjectName);
            if (parent != null) go.transform.SetParent(parent, false);
            else if (scene.IsValid() && scene.isLoaded && go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            var view = go.AddComponent<MouseView>();
            view.Init(state, room, content.Rooms.Get(room));
            return view;
        }

        void Init(GameMouse state, RoomId room, RoomDefinition definition)
        {
            State = state;
            Room = room;
            roomDefinition = definition;
            manifest = HareArtManifest.Load();
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerName = HareSortingLayers.Default;
            spriteRenderer.sortingOrder = HareArt.SortingOrderForDepth(state.Depth);
            spriteRenderer.enabled = false;
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (controls != null)
            {
                controls.Disable();
                controls.Dispose();
                controls = null;
            }
        }

        void LateUpdate() => Draw();

        /// <summary>obj_mouse's Step and Draw for this frame (and obj_player's recolouring of it); runs every LateUpdate.</summary>
        public void Draw()
        {
            if (State == null) return;
            var ship = State.FollowsShip && ArenaRoom.Active != null ? ArenaRoom.Active.Player : null;
            var camera = Camera.main;
            Vector2? world = ship != null ? ship.AimPoint : ReadPointer(camera);
            if (world == null)
            {
                spriteRenderer.enabled = false;
                Drawn = false;
                DrawOnUi?.Invoke(this);
                return;
            }

            Vector2 view = camera != null ? ToRoom(camera.ViewportToWorldPoint(new Vector3(0f, 1f, 0f))) : Vector2.zero;
            Vector2 mouse = ToRoom(world.Value);
            State.Step(mouse.x, mouse.y, view.x, view.y);

            // obj_player's Step, inside if(state != state_leave) { if(!paused) { ... } }.
            if (ship != null && !Gml.Paused)
                State.FollowShip(ship.State, GameMouse.WithinJumpArc(ship.Facing, ship.AimDirection, GameContent.Current.Flight.JumpArcDegrees));

            float x = (float)State.X, y = (float)State.Y;
            if (State.OnGui) { x += view.x; y += view.y; } // its Draw GUI's spot on screen, back in the room
            var at = ToWorld(x, y);
            transform.position = new Vector3(at.x, at.y, 0f);
            RoomPoint = new Vector2(x, y);
            spriteRenderer.sprite = SpriteFor(State.Sprite);
            Drawn = State.Visible && spriteRenderer.sprite != null;
            spriteRenderer.enabled = Drawn && DrawOnUi == null;
            DrawOnUi?.Invoke(this);
        }

        /// <summary>HareControls' Player/Aim (the mouse's screen position) as a world point, for a room with no ship.</summary>
        Vector2? ReadPointer(Camera camera)
        {
            if (camera == null || UnityEngine.InputSystem.Mouse.current == null) return null;
            if (controls == null)
            {
                controls = HareBindings.Create();
                controls.Player.Aim.Enable();
            }
            Vector3 screen = controls.Player.Aim.ReadValue<Vector2>();
            screen.z = -camera.transform.position.z;
            return camera.ScreenToWorldPoint(screen);
        }

        Sprite SpriteFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (sprites.TryGetValue(name, out var sprite)) return sprite;
            sprite = manifest != null ? manifest.Frame(name) : null;
            if (sprite == null) Debug.LogWarning("[Unity.Rea.Hare] " + name + " is not imported.");
            sprites[name] = sprite;
            return sprite;
        }

        int RoomWidth => roomDefinition != null ? roomDefinition.Width : 1920;
        int RoomHeight => roomDefinition != null ? roomDefinition.Height : 1080;

        /// <summary>The room pixel at <paramref name="world"/> (the room is centred on the world origin, y down).</summary>
        public Vector2 ToRoom(Vector2 world) =>
            new Vector2(world.x * GameClock.PixelsPerUnit + RoomWidth / 2f, RoomHeight / 2f - world.y * GameClock.PixelsPerUnit);

        /// <summary>The world point of room pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public Vector2 ToWorld(float x, float y) => new Vector2(GameClock.Px(x - RoomWidth / 2f), GameClock.Px(RoomHeight / 2f - y));
    }
}
