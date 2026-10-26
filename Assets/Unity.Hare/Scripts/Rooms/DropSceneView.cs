using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Art;
using Unity.Hare.Effects;
using Unity.Hare.Rendering;

namespace Unity.Hare.Rooms
{
    /// <summary>
    /// Draws Core's <see cref="DropScene"/> (U.HARE-T62), room_title's and room_intro's shared half: the backgrounds
    /// (bg_nightsky tiled and scrolling, bg_gradient3 tiled over it, on the backdrop sorting layer, as GameMaker draws
    /// backgrounds under every instance), obj_drop_cloud's clouds at their alpha, and the falling rabbit (spr_drop at the
    /// room's half scale), each with its sprite's GML origin on its x, y. It also plays the drop's smoke
    /// (<see cref="Smoke"/>: effect_create_below's ef_smokeup, under every sprite). It reads the scene and decides
    /// nothing; its room (<see cref="TitleRoom"/>, <see cref="IntroRoom"/>) steps it.
    ///
    /// The instances are all at depth 0, where GameMaker leaves their order to the runner; they are drawn here with the
    /// newer under the older (the clouds under the rabbit), which is how the runner orders same-depth instances it adds
    /// to its draw list, and keeps the rabbit, the one thing the eye follows, in front (BIBLE V40).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DropSceneView : MonoBehaviour
    {
        /// <summary>Sorting orders within depth 0, back to front.</summary>
        public const int CloudOrder = 0, LogoOrder = 1, DropOrder = 2, MoonOrder = 3, EyeOrder = 4, PupilOrder = 5, StandOrder = 6;

        readonly List<SpriteRenderer> layers = new List<SpriteRenderer>();
        readonly List<Vector2Int> layerSizes = new List<Vector2Int>();
        readonly List<SpriteRenderer> clouds = new List<SpriteRenderer>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        HareArtManifest manifest;
        SpriteRenderer drop;

        /// <summary>The scene drawn.</summary>
        public DropScene Scene { get; private set; }

        /// <summary>The backgrounds' renderers, back to front.</summary>
        public IReadOnlyList<SpriteRenderer> Layers => layers;

        /// <summary>The cloud renderers, drawing or idle.</summary>
        public IReadOnlyList<SpriteRenderer> Clouds => clouds;

        /// <summary>The rabbit's renderer (off once it is destroyed).</summary>
        public SpriteRenderer Drop => drop;

        /// <summary>Builds the view of <paramref name="scene"/> under <paramref name="parent"/>.</summary>
        public static DropSceneView Create(Transform parent, DropScene scene)
        {
            var go = new GameObject("DropScene");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DropSceneView>();
            view.Init(scene);
            return view;
        }

        void Init(DropScene scene)
        {
            Scene = scene ?? throw new ArgumentNullException(nameof(scene));
            manifest = HareArtManifest.Load();
            BuildBackgrounds();
            drop = MakeSprite("obj_drop", Scene.Tuning.DropSprite, DropOrder, (float)Scene.Tuning.DropScale);
            Present();
        }

        void LateUpdate() => Present();

        // ---- Coordinates ----

        /// <summary>The room's top-left corner in the world (the room camera looks at its middle).</summary>
        public Vector2 RoomOrigin => new Vector2(-GameClock.Px(Scene.RoomWidth) / 2f, GameClock.Px(Scene.RoomHeight) / 2f);

        /// <summary>The world point of room pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public Vector2 ToWorld(double x, double y) =>
            new Vector2(GameClock.Px((float)x - Scene.RoomWidth / 2f), GameClock.Px(Scene.RoomHeight / 2f - (float)y));

        /// <summary>The room pixel at <paramref name="world"/>.</summary>
        public Vector2 ToRoom(Vector2 world) =>
            new Vector2(world.x * GameClock.PixelsPerUnit + Scene.RoomWidth / 2f, Scene.RoomHeight / 2f - world.y * GameClock.PixelsPerUnit);

        // ---- Sprites ----

        /// <summary>The first frame of GML sprite <paramref name="name"/>, pivoted on its origin; null (with a warning) if not imported.</summary>
        public Sprite SpriteFor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (sprites.TryGetValue(name, out var sprite)) return sprite;
            sprite = manifest != null ? manifest.Frame(name) : null;
            if (sprite == null) Debug.LogWarning("[Unity.Hare] " + name + " is not imported.");
            sprites[name] = sprite;
            return sprite;
        }

        /// <summary>A sprite renderer for an instance at depth 0, drawn in <paramref name="order"/> and at <paramref name="scale"/>.</summary>
        public SpriteRenderer MakeSprite(string objectName, string sprite, int order, float scale = 1f)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = HareSortingLayers.Default;
            renderer.sortingOrder = HareArt.SortingOrderForDepth(0) + order;
            renderer.sprite = SpriteFor(sprite);
            return renderer;
        }

        /// <summary>Puts <paramref name="renderer"/>'s origin on room pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public void Place(SpriteRenderer renderer, double x, double y)
        {
            var at = ToWorld(x, y);
            renderer.transform.position = new Vector3(at.x, at.y, 0f);
        }

        /// <summary>The drop's <c>effect_create_below(ef_smokeup, x, y, size, colour)</c> at room pixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public void Smoke(double x, double y, int size, string color)
        {
            var tint = ColorUtility.TryParseHtmlString(color, out var parsed) ? parsed : Color.black;
            GmlEffects.CreateBelow(GmlEffectKind.SmokeUp, ToWorld(x, y), size, tint);
        }

        // ---- Drawing ----

        /// <summary>Draws the scene where it stands: backgrounds, clouds and the rabbit. Runs every LateUpdate.</summary>
        public void Present()
        {
            if (Scene == null) return;
            PlaceLayers();

            var list = Scene.Clouds;
            while (clouds.Count < list.Count) clouds.Add(MakeSprite("obj_drop_cloud", null, CloudOrder));
            for (int i = 0; i < list.Count; i++)
            {
                var cloud = list[i];
                var renderer = clouds[i];
                if (!renderer.enabled) renderer.enabled = true;
                renderer.sprite = SpriteFor(cloud.Sprite);
                renderer.color = new Color(1f, 1f, 1f, (float)cloud.Alpha);
                Place(renderer, cloud.X, cloud.Y);
            }
            for (int i = list.Count; i < clouds.Count; i++)
                if (clouds[i].enabled) clouds[i].enabled = false;

            var d = Scene.Drop;
            drop.enabled = d != null && drop.sprite != null;
            if (d != null) Place(drop, d.X, d.Y);
        }

        /// <summary>One tiled sprite per background on the backdrop layer, one image larger than the room each way.</summary>
        void BuildBackgrounds()
        {
            for (int i = 0; i < Scene.Backgrounds.Count; i++)
            {
                var layer = Scene.Backgrounds[i];
                var go = new GameObject(layer.Name);
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = HareSortingLayers.Backdrop;
                renderer.sortingOrder = i;

                var background = manifest != null ? manifest.FindBackground(layer.Name) : null;
                var size = background != null ? new Vector2Int(background.width, background.height) : new Vector2Int(Scene.RoomWidth, Scene.RoomHeight);
                if (background == null) Debug.LogWarning("[Unity.Hare] background " + layer.Name + " is not imported.");
                else renderer.sprite = background.sprite;

                int columns = Mathf.CeilToInt(Scene.RoomWidth / (float)size.x) + 1;
                int rows = Mathf.CeilToInt(Scene.RoomHeight / (float)size.y) + 1;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(GameClock.Px(columns * size.x), GameClock.Px(rows * size.y));
                layers.Add(renderer);
                layerSizes.Add(size);
            }
        }

        /// <summary>Each background where GameMaker draws its first tile: background_x/y mod the image, one image back.</summary>
        void PlaceLayers()
        {
            var origin = RoomOrigin;
            for (int i = 0; i < layers.Count; i++)
            {
                var layer = Scene.Backgrounds[i];
                var size = layerSizes[i];
                int x = StartupLayer.Wrap(layer.X, size.x) - size.x;
                int y = StartupLayer.Wrap(layer.Y, size.y) - size.y;
                layers[i].transform.position = new Vector3(origin.x + GameClock.Px(x), origin.y - GameClock.Px(y), 0f);
            }
        }

        /// <summary>The room pixel the tile of background <paramref name="index"/> starts on (tests).</summary>
        public Vector2Int TileStart(int index)
        {
            var origin = RoomOrigin;
            var p = layers[index].transform.position;
            return new Vector2Int(Mathf.RoundToInt((p.x - origin.x) * GameClock.PixelsPerUnit), Mathf.RoundToInt((origin.y - p.y) * GameClock.PixelsPerUnit));
        }
    }
}
