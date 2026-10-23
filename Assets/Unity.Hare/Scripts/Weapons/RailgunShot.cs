using System.Collections.Generic;
using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_railgun + generate_swirl/obj_rail_swirl (gmlHARE): a fixed 2000 px rail from the
    /// muzzle along the aim, drawn in railColor (200,255,200). Its alpha starts at railDefaultAlpha
    /// (0.8) and fades by railFade (0.01) per step; while it is still visible it kills one
    /// par_enemy on the line every step, then it removes itself and its swirls. Swirls sit every
    /// swirlIncrement (56 px) along the rail and share the rail's alpha.
    /// </summary>
    public class RailgunShot : MonoBehaviour
    {
        public const float DefaultAlpha = 0.8f;
        public const float FadePerStep = 0.01f;
        public const float SwirlIncrementPx = 56f;
        public static readonly Color RailColor = new Color(200f / 255f, 1f, 200f / 255f);

        readonly List<Vector2> swirlPoints = new List<Vector2>();
        LineRenderer halo;
        LineRenderer core;

        public Vector2 Origin { get; private set; }
        public Vector2 End { get; private set; }
        public float Alpha { get; private set; }
        public IReadOnlyList<Vector2> SwirlPoints => swirlPoints;

        public void Init(Vector2 origin, float directionDegrees, float lengthUnits)
        {
            Origin = origin;
            var dir = Gml.Dir(directionDegrees);
            End = origin + dir * lengthUnits;
            Alpha = DefaultAlpha;
            transform.position = origin;
            transform.rotation = Quaternion.Euler(0f, 0f, directionDegrees);

            for (float i = 0f; i < lengthUnits; i += Gml.Px(SwirlIncrementPx))
                swirlPoints.Add(origin + dir * i);

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

        void ApplyAlpha()
        {
            if (core == null) return;
            var c = RailColor;
            c.a = Alpha;
            core.startColor = core.endColor = c;
        }
    }
}
