using UnityEngine;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Rendering;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// obj_surface_reflection's Draw (U.HARE-T58): <c>draw_sprite_ext(player.sprite_index, 0, player.x, y, xscale, yscale,
    /// angle, c_white, alpha)</c>, the ship's pose (frame 0) at Core's <see cref="SurfaceReflection"/> place, angle, scale
    /// and alpha, at depth 2 (behind everything at depth 0 and 1, over the backgrounds). <see cref="ArenaRoom"/> builds one
    /// in a room whose reflection is active and puts it where the arena's state says every frame; it never changes the game.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SurfaceReflectionView : MonoBehaviour
    {
        SpriteRenderer sprite;

        /// <summary>The reflection's renderer.</summary>
        public SpriteRenderer Sprite => sprite;

        /// <summary>A new reflection under <paramref name="parent"/>, drawn at <paramref name="depth"/>.</summary>
        public static SurfaceReflectionView Create(Transform parent, int depth)
        {
            var go = new GameObject("obj_surface_reflection");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<SurfaceReflectionView>();
            view.sprite = go.AddComponent<SpriteRenderer>();
            view.sprite.sortingLayerName = HareSortingLayers.Default;
            view.sprite.sortingOrder = HareArt.SortingOrderForDepth(depth);
            view.sprite.enabled = false;
            return view;
        }

        /// <summary>
        /// Draws <paramref name="reflection"/> of <paramref name="ship"/> at world point <paramref name="at"/>: hidden
        /// until the reflection has a ship to follow.
        /// </summary>
        public void Draw(SurfaceReflection reflection, PlayerShipController ship, Vector2 at)
        {
            if (sprite == null) return;
            var look = ship != null ? ship.GetComponent<PlayerLook>() : null;
            var pose = look != null && look.Body != null ? look.Body.sprite : null;
            sprite.enabled = reflection != null && reflection.Active && reflection.Placed && pose != null;
            if (!sprite.enabled) return;
            sprite.sprite = pose; // player.sprite_index, frame 0, whether or not obj_player itself is drawn
            sprite.color = new Color(1f, 1f, 1f, reflection.Alpha);
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, reflection.Angle));
            transform.localScale = new Vector3(reflection.XScale, reflection.YScale, 1f);
        }
    }
}
