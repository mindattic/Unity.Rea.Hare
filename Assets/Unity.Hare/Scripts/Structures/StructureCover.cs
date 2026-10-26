using UnityEngine;
using Unity.Hare.Effects;

namespace Unity.Hare.Structures
{
    /// <summary>
    /// Ports par_structure_cover (gmlHARE, depth -1, U.HARE-T56): the detailed spr_structure1_cover art drawn over its
    /// <see cref="Structure"/>, hiding the structure's plain silhouette (and, one depth above obj_player's 0, the feet of a
    /// ship perched on its fuzzy edge). setup_level makes it at the structure's x, y with its image_angle and
    /// <c>cover.parent = structure</c>; it has no mask and takes no part in any collision.
    /// <para>Its Step's <c>x = parent.x; y = parent.y</c> is commented out in the original, so when random_positions moved
    /// a structure (level_05's randomize_on_loop) its cover stayed behind, showing the bare silhouette in one place and
    /// the art over empty sky in another. Fixed (BIBLE): the cover follows its structure, every step and on a move.</para>
    /// </summary>
    public class StructureCover : MonoBehaviour
    {
        public const int Depth = -1;
        public const string SpriteName = "spr_structure1_cover";

        bool gone;

        /// <summary>GML parent: the structure it covers.</summary>
        public Structure Parent { get; private set; }

        public SpriteRenderer Renderer { get; private set; }

        public bool IsGone => gone;

        internal static StructureCover Create(Structure parent)
        {
            var go = new GameObject("StructureCover");
            var cover = go.AddComponent<StructureCover>();
            cover.Parent = parent;
            cover.Renderer = HareArt.Show(go, SpriteName, Depth);
            cover.Follow();
            return cover;
        }

        void LateUpdate() => Follow();

        /// <summary>Sits on its structure at its image_angle.</summary>
        public void Follow()
        {
            if (gone || Parent == null || Parent.IsGone) return;
            var t = Parent.transform;
            transform.SetPositionAndRotation(new Vector3(t.position.x, t.position.y, transform.position.z), t.rotation);
        }

        internal void Clear()
        {
            if (gone) return;
            gone = true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
