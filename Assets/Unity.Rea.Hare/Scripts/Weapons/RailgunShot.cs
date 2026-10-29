using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Arena;
using Unity.Hare.Effects;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_railgun + generate_swirl/obj_rail_swirl (gmlHARE): a fixed 2000 px rail from the
    /// muzzle along the aim, drawn in railColor (200,255,200). Its alpha starts at railDefaultAlpha
    /// (0.8) and fades by railFade (0.01) per step; while it is still visible it kills one
    /// par_enemy on the line every step, then it removes itself and its swirls. generate_swirl puts an
    /// obj_rail_swirl (spr_railgun_swirl, depth -1, turned to the aim, blended railColor) every
    /// swirlIncrement (56 px) along the rail that lies inside the room; each takes the rail's alpha
    /// every step. While the rail is visible, its Step puffs a small railColor ef_smoke under everything
    /// at the muzzle and at the rail's end every step (its image_alpha > 0.25 gate is always open: the
    /// sprite-less obj_railgun never changes image_alpha from 1).
    /// </summary>
    public class RailgunShot : MonoBehaviour
    {
        public const float DefaultAlpha = 0.8f;
        public const float FadePerStep = 0.01f;
        public const float SwirlIncrementPx = 56f;
        public const string SwirlSprite = "spr_railgun_swirl";
        public const int SwirlDepth = -1;
        public static readonly Color RailColor = new Color(200f / 255f, 1f, 200f / 255f);

        readonly List<Vector2> swirlPoints = new List<Vector2>();
        readonly List<SpriteRenderer> swirls = new List<SpriteRenderer>();
        LineRenderer halo;
        LineRenderer core;
        float smokeSteps;

        public Vector2 Origin { get; private set; }
        public Vector2 End { get; private set; }
        public float Alpha { get; private set; }
        /// <summary>Every point generate_swirl's loop visits (every 56 px from the muzzle along the rail).</summary>
        public IReadOnlyList<Vector2> SwirlPoints => swirlPoints;
        /// <summary>The obj_rail_swirl drawn: one at each of <see cref="SwirlPoints"/> inside the room.</summary>
        public IReadOnlyList<SpriteRenderer> Swirls => swirls;

        /// <summary>The room the swirls are kept inside (generate_swirl's 0..room_width, 0..room_height), in world units.</summary>
        public static Rect RoomBounds()
        {
            var arena = Object.FindAnyObjectByType<ArenaRoom>();
            if (arena == null || arena.State == null) return Gml.Room;
            Vector2 topLeft = arena.ToWorld(0f, 0f);
            Vector2 bottomRight = arena.ToWorld(arena.State.Width, arena.State.Height);
            return Rect.MinMaxRect(topLeft.x, bottomRight.y, bottomRight.x, topLeft.y);
        }

        public void Init(Vector2 origin, float directionDegrees, float lengthUnits) =>
            Init(origin, directionDegrees, lengthUnits, RoomBounds());

        public void Init(Vector2 origin, float directionDegrees, float lengthUnits, Rect room)
        {
            Origin = origin;
            var dir = Gml.Dir(directionDegrees);
            End = origin + dir * lengthUnits;
            Alpha = DefaultAlpha;
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0f, 0f, directionDegrees);

            for (float i = 0f; i < lengthUnits; i += Gml.Px(SwirlIncrementPx))
            {
                var at = origin + dir * i;
                swirlPoints.Add(at);
                // GML: railX >= 0 && railX <= room_width, railY likewise (inclusive edges).
                if (at.x < room.xMin || at.x > room.xMax || at.y < room.yMin || at.y > room.yMax) continue;
                var swirl = new GameObject("RailSwirl");
                swirl.transform.SetParent(transform, false);
                swirl.transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, directionDegrees));
                swirls.Add(HareArt.Show(swirl, SwirlSprite, SwirlDepth));
            }

            halo = Gml.AddLine(gameObject, new Color(RailColor.r, RailColor.g, RailColor.b, 0.1f), Gml.Px(9f));
            var coreGo = new GameObject("Core");
            coreGo.transform.SetParent(transform, false);
            core = Gml.AddLine(coreGo, RailColor, Gml.Px(1f));
            foreach (var line in new[] { halo, core })
            {
                line.SetPosition(0, Origin);
                line.SetPosition(1, End);
            }
            ApplyAlpha();
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>GML Step, scaled from per-step to <paramref name="dt"/> seconds. Returns false once the rail is gone.</summary>
        public bool Tick(float dt)
        {
            if (Alpha > 0f)
            {
                Alpha -= FadePerStep * Gml.StepsPerSecond * dt;
                Smoke(dt);
                var enemy = Gml.FirstAlong<IEnemyTarget>(Origin, End, out var point);
                enemy?.OnWeaponHit(new WeaponHit(WeaponHitKind.Railgun, point, (End - Origin).normalized));
            }

            if (Alpha <= 0f)
            {
                Alpha = 0f;
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
                return false;
            }
            ApplyAlpha();
            return true;
        }

        /// <summary>The Step's two ef_smoke puffs (muzzle, rail end) for each step <paramref name="dt"/> holds.</summary>
        void Smoke(float dt)
        {
            smokeSteps += dt * Gml.StepsPerSecond;
            while (smokeSteps >= 1f - 1e-4f)
            {
                smokeSteps -= 1f;
                GmlEffects.CreateBelow(GmlEffectKind.Smoke, Origin, 0, RailColor);
                GmlEffects.CreateBelow(GmlEffectKind.Smoke, End, 0, RailColor);
            }
        }

        void ApplyAlpha()
        {
            // obj_rail_swirl's Step: image_alpha = railAlpha; its image_blend is railColor.
            var swirlColor = RailColor;
            swirlColor.a = Alpha;
            foreach (var swirl in swirls)
                if (swirl != null) swirl.color = swirlColor;

            if (core == null) return;
            var c = RailColor;
            c.a = Alpha;
            core.startColor = core.endColor = c;
        }
    }
}
