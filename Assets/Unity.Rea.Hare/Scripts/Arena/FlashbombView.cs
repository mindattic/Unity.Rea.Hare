using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;
using Unity.Hare.Rendering;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// One obj_flashbomb as drawn (U.HARE-T67), at depth -1000 over everything in the room: its Draw's <c>draw_self()</c>,
    /// spr_white (32x32, origin 0,0) scaled by view_wview and view_hview at view 0's corner at image_alpha, then the four
    /// <c>draw_circle_color(player.x, player.y, radius*, c_white, c_white, true)</c> outlines at burst_alpha (GameMaker's
    /// default circle precision, 24 segments). It reads Core's <see cref="Flashbomb"/> and never changes it.
    /// </summary>
    public sealed class FlashbombView : MonoBehaviour
    {
        /// <summary>draw_circle's default precision (draw_set_circle_precision is never called).</summary>
        public const int CircleSegments = 24;

        static Sprite fallbackWhite;
        static Material unlitMaterial;

        SpriteRenderer flash;
        readonly LineRenderer[] circles = new LineRenderer[4];

        public Flashbomb Bomb { get; private set; }
        /// <summary>The white flash over view 0.</summary>
        public SpriteRenderer Flash => flash;
        /// <summary>radius, radius2, radius3 and radius4's outlines.</summary>
        public IReadOnlyList<LineRenderer> Circles => circles;

        public static FlashbombView Create(Transform parent, Flashbomb bomb, FlashbombDefinition definition)
        {
            var go = new GameObject("obj_flashbomb");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<FlashbombView>();
            view.Bomb = bomb;
            view.Build(definition);
            return view;
        }

        void Build(FlashbombDefinition d)
        {
            int order = HareArt.SortingOrderForDepth(d.Depth);
            var flashGo = new GameObject("flash");
            flashGo.transform.SetParent(transform, false);
            flash = flashGo.AddComponent<SpriteRenderer>();
            flash.sprite = HareArt.Frame(d.Sprite) ?? FallbackWhite();
            flash.sortingLayerName = HareSortingLayers.Default;
            flash.sortingOrder = order;
            var material = UnlitMaterial();
            if (material != null) flash.sharedMaterial = material;
            flash.color = new Color(1f, 1f, 1f, 0f);

            for (int i = 0; i < circles.Length; i++)
            {
                var circleGo = new GameObject("circle " + (i + 1));
                circleGo.transform.SetParent(transform, false);
                var line = circleGo.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.positionCount = CircleSegments;
                line.widthMultiplier = Gml.Px(1f);
                line.numCapVertices = 0;
                line.sortingLayerName = HareSortingLayers.Default;
                line.sortingOrder = order + 1; // drawn after draw_self
                if (material != null) line.sharedMaterial = material;
                line.enabled = false;
                circles[i] = line;
            }
        }

        /// <summary>
        /// Draws the bomb this frame: the flash with its top-left on view 0's (<paramref name="viewTopLeft"/>, world) at
        /// <paramref name="viewWidth"/> x <paramref name="viewHeight"/> times the sprite, the circles round
        /// <paramref name="centre"/> (the ship; none without one).
        /// </summary>
        public void Draw(Vector2 viewTopLeft, float viewWidth, float viewHeight, Vector2? centre)
        {
            if (Bomb == null) return;
            flash.transform.position = new Vector3(viewTopLeft.x, viewTopLeft.y, 0f);
            flash.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01((float)Bomb.ImageAlpha));

            var radii = Bomb.Radii;
            var colour = new Color(1f, 1f, 1f, Mathf.Clamp01((float)Bomb.BurstAlpha));
            for (int i = 0; i < circles.Length; i++)
            {
                var line = circles[i];
                line.enabled = centre.HasValue;
                if (!centre.HasValue) continue;
                float r = Gml.Px((float)radii[i]);
                for (int k = 0; k < CircleSegments; k++)
                {
                    float a = k * Mathf.PI * 2f / CircleSegments;
                    line.SetPosition(k, new Vector3(centre.Value.x + Mathf.Cos(a) * r, centre.Value.y + Mathf.Sin(a) * r, 0f));
                }
                line.startColor = colour;
                line.endColor = colour;
            }
        }

        /// <summary>spr_white with no imported art: 32x32 white, pivot top-left, 100 px to a unit.</summary>
        static Sprite FallbackWhite()
        {
            if (fallbackWhite != null) return fallbackWhite;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "HareFlashbombWhite", hideFlags = HideFlags.DontSave };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            fallbackWhite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 1f), 1f / Gml.Px(32f));
            fallbackWhite.name = "HareFlashbombWhite";
            fallbackWhite.hideFlags = HideFlags.DontSave;
            return fallbackWhite;
        }

        static Material UnlitMaterial()
        {
            if (unlitMaterial != null) return unlitMaterial;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            unlitMaterial = new Material(shader) { name = "HareFlashbombUnlit", hideFlags = HideFlags.DontSave };
            return unlitMaterial;
        }
    }

    /// <summary>
    /// obj_flashbomb's Step sweep (U.HARE-T67): <c>with(par_enemy)</c>, each enemy in view (<c>in_view(x, y)</c>) whose mask
    /// the circle of <c>radius</c> round the ship touches becomes an obj_rocket_explosion at its x, y and is destroyed (no
    /// par_enemy has a Destroy event: no score, KILLS or drop); then <c>with(par_projectile)</c>, each such shot (obj_bullet,
    /// obj_rocket, obj_mine, obj_enemy_mine) becomes an obj_tinyexplosion. The original's <c>collision_circle(...,
    /// par_enemy, ...)</c> asked whether any enemy touched the circle, so the first one it reached set off every enemy in
    /// view at once; here each goes when the circle reaches its own mask (BIBLE D26).
    /// </summary>
    public static class FlashbombSweep
    {
        static readonly List<Collider2D> Hits = new List<Collider2D>();

        /// <summary>Sweeps the circle of <paramref name="radius"/> (world units) round <paramref name="centre"/>; returns enemies and shots taken.</summary>
        public static (int enemies, int projectiles) Sweep(Vector2 centre, float radius, Func<Vector2, bool> inView)
        {
            if (radius <= 0f) return (0, 0);
            Hits.Clear();
            var filter = new ContactFilter2D { useTriggers = true };
            filter.NoFilter();
            filter.useTriggers = true;
            Physics2D.OverlapCircle(centre, radius, filter, Hits);

            var enemies = new List<EnemyBodyView>();
            var projectiles = new List<Component>();
            var seen = new HashSet<Object>();
            foreach (var hit in Hits)
            {
                if (hit == null) continue;
                var enemy = hit.GetComponentInParent<EnemyBodyView>();
                if (enemy != null)
                {
                    if (enemy.IsDead || !seen.Add(enemy)) continue;
                    if (enemy is EnemyMine) projectiles.Add(enemy); // obj_enemy_mine is a par_projectile
                    else enemies.Add(enemy);
                    continue;
                }
                Component shot = hit.GetComponentInParent<Bullet>();
                if (shot == null) shot = hit.GetComponentInParent<Rocket>();
                if (shot == null) shot = hit.GetComponentInParent<Mine>();
                if (shot != null && seen.Add(shot)) projectiles.Add(shot);
            }

            int enemiesTaken = 0, shotsTaken = 0;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                Vector2 at = enemy.transform.position;
                if (inView != null && !inView(at)) continue;
                new GameObject("RocketExplosion").AddComponent<RocketExplosion>().Init(at);
                EnemyDirector.Remove(enemy);
                enemiesTaken++;
            }
            foreach (var shot in projectiles)
            {
                if (shot == null) continue;
                Vector2 at = shot.transform.position;
                if (inView != null && !inView(at)) continue;
                TinyExplosion.Spawn(at);
                if (shot is EnemyBodyView mine) EnemyDirector.Remove(mine);
                else if (Application.isPlaying) Object.Destroy(shot.gameObject);
                else Object.DestroyImmediate(shot.gameObject);
                shotsTaken++;
            }
            return (enemiesTaken, shotsTaken);
        }
    }
}
