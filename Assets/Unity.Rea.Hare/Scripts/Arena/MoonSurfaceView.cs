using System;
using UnityEngine;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// obj_moon_surface (U.HARE-T58): spr_moon_surface at depth 1, risen into its room by Core's <see cref="MoonSurface"/>
    /// once something turns it on, stepped once a GameMaker step. Nothing in the original creates one (the title's moon is
    /// obj_stand_title's own draw_sprite), so nothing here does either; it is ported for whatever brings it in, as
    /// obj_oval_explosion is. Its arrival is the original's <c>with(obj_drop_emitter) instance_destroy()</c>, raised as
    /// <see cref="DropEmittersCleared"/> for the title's drop emitter (U.HARE-T62) to answer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MoonSurfaceView : MonoBehaviour
    {
        SpriteRenderer sprite;
        Func<float, float, Vector2> toWorld;
        float x;
        int roomHeight;
        double steps;

        /// <summary>The rules.</summary>
        public MoonSurface State { get; private set; }

        /// <summary>The moon's renderer.</summary>
        public SpriteRenderer Sprite => sprite;

        /// <summary>Raised when a moon arrives: the original destroys every obj_drop_emitter then.</summary>
        public static event Action DropEmittersCleared;

        /// <summary>
        /// instance_create(x, y, obj_moon_surface) in a room <paramref name="roomHeight"/> tall, room pixels placed in the
        /// world by <paramref name="toWorld"/> (the room's own conversion).
        /// </summary>
        public static MoonSurfaceView Create(float x, float y, int roomHeight, Func<float, float, Vector2> toWorld)
        {
            var definition = GameContent.Current.Arena.MoonSurface;
            var go = new GameObject("obj_moon_surface");
            var view = go.AddComponent<MoonSurfaceView>();
            view.State = new MoonSurface(definition, y);
            view.x = x;
            view.roomHeight = roomHeight;
            view.toWorld = toWorld ?? ((px, py) => new Vector2(Gml.Px(px), -Gml.Px(py)));
            view.sprite = HareArt.Show(go, definition.Sprite, definition.Depth);
            view.sprite.sortingLayerName = HareSortingLayers.Default;
            view.Place();
            return view;
        }

        /// <summary>phase = "on".</summary>
        public void TurnOn() => State.TurnOn();

        void FixedUpdate()
        {
            steps += Time.fixedDeltaTime * Gml.StepsPerSecond;
            while (steps >= 1.0)
            {
                steps -= 1.0;
                Step();
            }
        }

        /// <summary>One GameMaker step: the Step event and the motion after it.</summary>
        public void Step()
        {
            if (State == null) return;
            if (State.Step(roomHeight)) DropEmittersCleared?.Invoke();
            Place();
        }

        void Place()
        {
            var at = toWorld(x, State.Y);
            transform.position = new Vector3(at.x, at.y, transform.position.z);
        }
    }
}
