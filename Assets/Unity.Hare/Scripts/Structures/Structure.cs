using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Structures
{
    /// <summary>
    /// Ports par_structure (gmlHARE, depth 0, U.HARE-T56): a still landmark the ship perches on, drawn as spr_structure1's
    /// plain grey silhouette with its precise mask (colkind 0) and hidden under its <see cref="StructureCover"/>
    /// (par_structure_cover, depth -1, spr_structure1_cover), which draws the detailed art over it.
    /// <list type="bullet">
    /// <item>Create: <c>image_angle = 0 + irandom_range(-30, 30)</c>, <c>cover = -1</c> (setup_level then makes the cover
    /// and sets the sprites). It never moves, spins or breaks.</item>
    /// <item>Collision obj_bullet (any owner): an obj_tinyexplosion at the bullet and the bullet is destroyed (obj_bullet's
    /// own par_structure event does the same).</item>
    /// <item>Collision obj_rocket: an obj_rocket_explosion at the rocket and the rocket is destroyed.</item>
    /// <item>draw_beam stops the laser at it (<see cref="IBeamBlocker"/>).</item>
    /// <item>obj_player's par_structure collision perches the flying ship on it (<see cref="IPerchSurface"/>, through the
    /// same <see cref="PlayerShipController.LandOn"/> as a rock; unlike par_rock's there is no in_view check, no chips and
    /// no ride).</item>
    /// <item>setup_level's with(par_structure) / with(par_structure_cover) instance_destroy() and move_rocks's spawn-point
    /// clear destroy it with its cover (<see cref="Clear"/>).</item>
    /// </list>
    /// Nothing else (enemies, flame, the railgun, explosions, the shield wall) touches it.
    /// </summary>
    public class Structure : MonoBehaviour, IProjectileTarget, IRocketTarget, IBeamBlocker, IPerchSurface
    {
        public const int Depth = 0;
        /// <summary>setup_level: <c>rnd_structure = irandom_range(1, 1)</c>, so always spr_structure1.</summary>
        public const string SpriteName = "spr_structure1";
        /// <summary>Create: image_angle = 0 + irandom_range(-30, 30).</summary>
        public const int AngleRange = 30;
        /// <summary>spr_structure1's bbox (33..355, 33..338 about its 198, 188 origin): the fallback mask without art.</summary>
        public static readonly Vector2 BBoxSizePx = new Vector2(323f, 306f);
        /// <summary>The bbox's centre from the origin, px, Unity axes (y up).</summary>
        public static readonly Vector2 BBoxCentrePx = new Vector2(-4f, 2.5f);

        SpriteRenderer spriteRenderer;
        Collider2D mask;
        float imageAngle;
        bool gone;

        /// <summary>Raised once as any structure leaves play, before it is destroyed.</summary>
        public static event Action<Structure> Removed;

        /// <summary>GML cover: the par_structure_cover drawn over it.</summary>
        public StructureCover Cover { get; internal set; }

        /// <summary>The field that placed it (setup_level's structure pass); null for a lone structure.</summary>
        public StructureField Field { get; set; }

        public bool IsGone => gone;
        public Vector2 Position => transform.position;
        public SpriteRenderer Renderer => spriteRenderer;

        /// <summary>GML image_angle, degrees in [0, 360).</summary>
        public float ImageAngle
        {
            get => imageAngle;
            private set
            {
                imageAngle = Mathf.Repeat(value, 360f);
                transform.rotation = Quaternion.Euler(0f, 0f, imageAngle);
            }
        }

        // ---- IPerchSurface: obj_player's par_structure collision ----

        SurfaceKind IPerchSurface.Kind => SurfaceKind.Structure;
        int IPerchSurface.SurfaceId => GetInstanceID();
        float IPerchSurface.SpinPerStep => 0f;
        float IPerchSurface.SpeedPxPerStep => 0f;
        Collider2D IPerchSurface.Mask => mask;

        /// <summary>
        /// setup_level's <c>structure = instance_create(x, y, par_structure)</c> (its Create rolls image_angle) and
        /// <c>cover = instance_create(structure.x, structure.y, par_structure_cover)</c> with the structure's image_angle and
        /// parent; <paramref name="angle"/> pins image_angle for tests.
        /// </summary>
        public static Structure Create(Vector2 position, float? angle = null)
        {
            var go = new GameObject("Structure");
            var structure = go.AddComponent<Structure>();
            structure.Init(position, angle ?? IRandom(-AngleRange, AngleRange));
            structure.Cover = StructureCover.Create(structure);
            return structure;
        }

        void Init(Vector2 position, float angle)
        {
            gone = false;
            transform.position = new Vector3(position.x, position.y, 0f);
            ImageAngle = angle;
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            spriteRenderer = HareArt.Show(gameObject, SpriteName, Depth);
            UseMask();
        }

        /// <summary>A precise mask: the sprite's physics shape, or its bounding box without art.</summary>
        void UseMask()
        {
            if (spriteRenderer != null && spriteRenderer.sprite != null && spriteRenderer.sprite.GetPhysicsShapeCount() > 0)
            {
                mask = gameObject.AddComponent<PolygonCollider2D>();
            }
            else
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(Gml.Px(BBoxSizePx.x), Gml.Px(BBoxSizePx.y));
                box.offset = new Vector2(Gml.Px(BBoxCentrePx.x), Gml.Px(BBoxCentrePx.y));
                mask = box;
            }
            mask.isTrigger = true;
        }

        /// <summary>random_positions: <c>x = irandom_range(0, room_width); y = irandom_range(0, room_height)</c>.</summary>
        public void MoveTo(Vector2 world)
        {
            transform.position = new Vector3(world.x, world.y, transform.position.z);
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.position = world;
            if (Cover != null) Cover.Follow();
        }

        // ---- Collisions ----

        /// <summary>par_structure Collision obj_bullet: an obj_tinyexplosion at the bullet, which is spent.</summary>
        public bool OnProjectileHit(Bullet bullet)
        {
            if (gone || bullet == null) return false;
            TinyExplosion.Spawn(bullet.transform.position);
            return true;
        }

        /// <summary>par_structure Collision obj_rocket: the rocket goes off where it is (obj_rocket_explosion) and is gone.</summary>
        public bool OnRocketHit(Rocket rocket) => !gone && rocket != null;

        /// <summary>with(cover) instance_destroy(); instance_destroy(): setup_level's clear and move_rocks's spawn clear.</summary>
        public void Clear()
        {
            if (gone) return;
            gone = true;
            Removed?.Invoke(this);
            if (Field != null) Field.OnStructureGone(this);
            if (Cover != null) Cover.Clear();
            Cover = null;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        /// <summary>GML irandom_range(a, b), inclusive.</summary>
        internal static int IRandom(int min, int max) => UnityEngine.Random.Range(min, max + 1);
    }
}
