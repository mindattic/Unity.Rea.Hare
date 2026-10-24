using UnityEngine;

namespace Unity.Hare.Rendering
{
    /// <summary>
    /// The full-screen layer the room-fade transitions (<c>obj_fade_*</c>, planned UH-T50) and the flashbomb
    /// (<c>obj_flashbomb</c>, planned UH-T67) will drive once their own rules are reverse-engineered and
    /// rebuilt: a sprite that always exactly covers the camera it is parented to, on
    /// <see cref="HareSortingLayers.Overlay"/> so it draws over every other object regardless of that object's
    /// own sorting order. U.HARE-T44 only builds the layer and its colour API; the fade and flash timing and
    /// colours are those tickets' own rules, not invented here. Plain <see cref="SpriteRenderer"/>, so this has
    /// nothing to do with which render pipeline is active.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ScreenOverlayLayer : MonoBehaviour
    {
        static Sprite whitePixel;

        SpriteRenderer spriteRenderer;
        Camera parentCamera;

        /// <summary>The overlay's current colour; alpha 0 (the starting state) is fully invisible.</summary>
        public Color Color
        {
            get => spriteRenderer.color;
            set => spriteRenderer.color = value;
        }

        // Awake (and every other Unity lifecycle call) only runs in Play Mode; ProjectSetup builds the main
        // scene in Edit Mode, so Create() below calls Initialize() itself rather than relying on Awake to set
        // the sprite, sorting layer and starting colour before the scene is saved.
        void Awake() => Initialize();

        void Initialize()
        {
            if (spriteRenderer != null) return;
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sprite = WhitePixel();
            spriteRenderer.sortingLayerName = HareSortingLayers.Overlay;
            spriteRenderer.sortingOrder = short.MaxValue;
            spriteRenderer.color = new Color(0f, 0f, 0f, 0f);
            parentCamera = GetComponentInParent<Camera>();
            FitToCamera();
        }

        void LateUpdate() => FitToCamera();

        void FitToCamera()
        {
            if (parentCamera == null) return;
            float height = parentCamera.orthographicSize * 2f;
            float width = height * parentCamera.aspect;
            transform.localScale = new Vector3(width, height, 1f);
            transform.localPosition = new Vector3(0f, 0f, Mathf.Max(parentCamera.nearClipPlane + 0.01f, 0.01f));
        }

        /// <summary>Builds the overlay as a child of <paramref name="cameraTransform"/>'s camera, initialized immediately.</summary>
        public static ScreenOverlayLayer Create(Transform cameraTransform)
        {
            var go = new GameObject("Screen Overlay");
            go.transform.SetParent(cameraTransform, false);
            var overlay = go.AddComponent<ScreenOverlayLayer>();
            overlay.Initialize();
            return overlay;
        }

        static Sprite WhitePixel()
        {
            if (whitePixel != null) return whitePixel;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "HareOverlayPixel" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whitePixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            whitePixel.name = "HareOverlayPixel";
            return whitePixel;
        }
    }
}
