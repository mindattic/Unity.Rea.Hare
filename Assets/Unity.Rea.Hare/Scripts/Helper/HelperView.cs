using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;

namespace Unity.Hare.Helper
{
    /// <summary>
    /// Draws one obj_helper (spr_helper at depth 0, turned to its image_angle) where its Core body is (U.HARE-T66), and
    /// answers which colliders its mask touches: spr_helper's colkind 2 ellipse over its 32 x 32 box, a 32 px circle. It
    /// carries no collider of its own (nothing else collides with obj_helper: its par_projectile and par_enemy collision
    /// events are all commented out), and it changes nothing itself.
    /// </summary>
    public sealed class HelperView : MonoBehaviour
    {
        HelperTuning tuning;

        public HelperHawk Helper { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        public static HelperView Create(HelperHawk helper, HelperTuning tuning)
        {
            var go = new GameObject("obj_helper");
            var view = go.AddComponent<HelperView>();
            view.Helper = helper;
            view.tuning = tuning;
            view.Renderer = HareArt.Show(go, tuning.Sprite, tuning.Depth);
            view.Present();
            return view;
        }

        /// <summary>The mask's centre and radius in the world (the bbox's pixels l..r and t..b about the origin).</summary>
        public Vector2 MaskCentre
        {
            get
            {
                var box = tuning.Box;
                var offset = new Vector2(GameClock.Px((box[0] + box[2] + 1f) / 2f), -GameClock.Px((box[1] + box[3] + 1f) / 2f));
                return (Vector2)transform.position + (Vector2)(transform.rotation * offset);
            }
        }

        public float MaskRadius => GameClock.Px(tuning.Box[2] - tuning.Box[0] + 1f) / 2f;

        /// <summary>Every collider the mask overlaps where the helper is now.</summary>
        public Collider2D[] MaskHits()
        {
            Physics2D.SyncTransforms();
            return Physics2D.OverlapCircleAll(MaskCentre, MaskRadius);
        }

        /// <summary>Puts the view where the field has the helper, turned to its image_angle.</summary>
        public void Present()
        {
            if (Helper == null) return;
            var at = Helper.Position.ToVector2();
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, transform.position.z), Quaternion.Euler(0f, 0f, Helper.ImageAngle));
        }

        void OnDestroy()
        {
            if (Helper != null) HelperDirector.Dispose(Helper.Id, this);
        }
    }
}
