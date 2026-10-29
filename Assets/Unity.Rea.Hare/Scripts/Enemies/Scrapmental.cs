using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="ScrapmentalEnemy"/> (obj_scrapmental, level_08's boss): spr_schrapnel_eye in its grey
    /// image_blend, turning with image_angle, under spr_shrapnel_frame drawn upright, both only while it is alive; behind
    /// them obj_shrapnel_back (<see cref="ShrapnelBack"/>). Its slivers show spr_sliver. It is the level's o_defeat target
    /// (<see cref="Arena.LevelTargets"/>): the object goes 10 s after the defeat, and that completes the level.
    /// </summary>
    public class Scrapmental : SetPieceEnemy
    {
        public const string GmlObject = "obj_scrapmental";
        public const string EyeSprite = "spr_schrapnel_eye";
        public const string FrameSprite = "spr_shrapnel_frame";
        public const string SliverSprite = "spr_sliver";
        public const int Depth = -3;
        /// <summary>obj_bullet's depth.</summary>
        public const int SliverDepth = -3;
        /// <summary>spr_schrapnel_eye's mask (bbox 43..84 x 44..85 of 128x128, origin 64,64).</summary>
        public const float HitRadiusPx = 21f;

        ScrapmentalEnemy Rules => (ScrapmentalEnemy)Core;
        SpriteRenderer eye;
        SpriteRenderer frame;

        /// <summary>GML alive: false once defeated (no longer drawn, inert).</summary>
        public bool Alive => Rules.Alive;
        public int Rgb => Rules.Rgb;
        public float Spin => Rules.Spin;
        public float UndulateY => Rules.UndulateY;
        public SpriteRenderer Eye => eye;
        public SpriteRenderer Frame => frame;
        /// <summary>obj_shrapnel_back while it is still there.</summary>
        public ShrapnelBack Back { get; private set; }

        /// <summary>The hull pieces still in the world.</summary>
        public IReadOnlyList<ShrapnelPiece> Pieces
        {
            get
            {
                var list = new List<ShrapnelPiece>();
                foreach (var p in Rules.Pieces)
                {
                    var view = EnemyDirector.ViewOf<ShrapnelPiece>(p.Id);
                    if (view != null) list.Add(view);
                }
                return list;
            }
        }

        /// <summary>The harpoons still in the world.</summary>
        public IReadOnlyList<ShrapnelHarpoon> Harpoons
        {
            get
            {
                var list = new List<ShrapnelHarpoon>();
                foreach (var h in Rules.Harpoons)
                {
                    var view = EnemyDirector.ViewOf<ShrapnelHarpoon>(h.Id);
                    if (view != null) list.Add(view);
                }
                return list;
            }
        }

        /// <summary>Alarm 1's volley, with how many slivers it fired.</summary>
        public event Action<Scrapmental, int> Volley;
        /// <summary>The defeat (alive = false), 10 s before the object goes.</summary>
        public event Action<Scrapmental> Defeated;

        /// <summary>GML instance_create(x, y, obj_scrapmental) with its Create (pieces, harpoons, back plate).</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null, float? spin = null) =>
            Begin(EnemyKind.Scrapmental, position, room, player, new EnemyOptions { Spin = spin });

        /// <summary>level_08's <c>boss_scrapmental = instance_create(room_width/2, room_height/2, obj_scrapmental)</c>.</summary>
        public static GameObject Spawn(Vector2 world)
        {
            var go = new GameObject("Scrapmental");
            go.AddComponent<Scrapmental>().Init(world);
            return go;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterTarget() => Arena.LevelTargets.Register(GmlObject, Spawn);

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void RegisterInEditor() => RegisterTarget();
#endif

        protected override void Configure()
        {
            UseCircle(HitRadiusPx);
            AddDamageSource();
            eye = HareArt.Show(gameObject, EyeSprite, Depth);
            var frameGo = new GameObject("frame");
            frameGo.transform.SetParent(transform, false);
            frameGo.transform.localPosition = new Vector3(0f, 0f, -0.01f); // drawn after the eye at the same depth
            frame = HareArt.Show(frameGo, FrameSprite, Depth);
            Back = ShrapnelBack.Create(this);
            Refresh();
        }

        void LateUpdate() => Refresh();

        /// <summary>Draw: <c>if(alive) { draw_self(); draw_sprite_ext(spr_shrapnel_frame, 0, x, y, 1, 1, 0, c_white, 1); }</c>.</summary>
        void Refresh()
        {
            if (Core == null) return;
            bool alive = Rules.Alive;
            float grey = Rules.Rgb / 255f;
            if (eye != null)
            {
                eye.enabled = alive;
                eye.color = new Color(grey, grey, grey, Rules.ImageAlpha);
            }
            if (frame != null)
            {
                frame.enabled = alive;
                frame.transform.rotation = Quaternion.identity;
            }
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            switch (e)
            {
                case ScrapmentalVolley volley:
                    Volley?.Invoke(this, volley.Slivers);
                    break;
                case ScrapmentalDefeated _:
                    // Inert from now on (docs/BIBLE.md D30): nothing touches it, hits it or is hurt by it.
                    foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
                    if (Back != null) Back.FadeOut();
                    Refresh();
                    Defeated?.Invoke(this);
                    break;
            }
            base.OnEnemyEvent(e);
        }

        /// <summary>bullet.sprite_index = spr_sliver.</summary>
        public override void OnRound(RoundFired fired, Bullet bullet)
        {
            if (bullet != null) HareArt.Show(bullet.gameObject, SliverSprite, SliverDepth);
        }

        /// <summary>
        /// Its obj_rocket event destroys the rocket itself (<c>with(other) instance_destroy()</c>) and obj_rocket has no
        /// Destroy event, so a rocket that hits it vanishes in a tiny explosion without a blast.
        /// </summary>
        public override bool OnRocketHit(Rocket rocket)
        {
            if (IsDead || !Rules.Alive) return false;
            bool hit = base.OnRocketHit(rocket);
            if (hit && rocket != null) rocket.OnProjectileHit(null); // the rocket's own silent removal
            return false;
        }

        protected override void OnKilled(EnemyKill kill)
        {
            // Gone before its plate finished fading (a beam's kill): the plate fades on by itself (D29).
            if (Back != null)
            {
                Back.transform.SetParent(null, true);
                Back.FadeOut();
            }
            base.OnKilled(kill);
        }
    }

    /// <summary>
    /// obj_shrapnel_back (presentation only): spr_brain at depth -1 behind the boss, on the boss's position every step;
    /// at the defeat (mode "fadeout") it sinks 1 px a step while its alpha drops 0.005 a step, then is gone. Paused, it
    /// holds still.
    /// </summary>
    public sealed class ShrapnelBack : MonoBehaviour
    {
        public const string Sprite = "spr_brain";
        public const int Depth = -1;
        public const float FadePerStep = 0.005f;
        public const float SinkPxPerStep = 1f;

        SpriteRenderer spriteRenderer;
        float alpha = 1f;

        /// <summary>GML mode == "fadeout".</summary>
        public bool FadingOut { get; private set; }
        public float Alpha => alpha;
        public SpriteRenderer Renderer => spriteRenderer;

        internal static ShrapnelBack Create(Scrapmental boss)
        {
            var go = new GameObject("ShrapnelBack");
            go.transform.SetParent(boss.transform, false);
            go.transform.localPosition = Vector3.zero;
            var back = go.AddComponent<ShrapnelBack>();
            back.spriteRenderer = HareArt.Show(go, Sprite, Depth);
            return back;
        }

        public void FadeOut() => FadingOut = true;

        void FixedUpdate() => Step(Time.fixedDeltaTime * Gml.StepsPerSecond);

        void LateUpdate() => transform.rotation = Quaternion.identity;

        /// <summary>One GML step, <paramref name="steps"/> steps long.</summary>
        public void Step(float steps)
        {
            if (Gml.Paused || !FadingOut) return; // "normal": it rides on the boss (a child of its object)
            transform.position += new Vector3(0f, -Gml.Px(SinkPxPerStep * steps), 0f); // y += 1 (GML y grows downward)
            if (alpha > 0f) alpha = Mathf.Max(0f, alpha - FadePerStep * steps);
            else
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
                return;
            }
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    /// <summary>
    /// The view on <see cref="ShrapnelPieceBody"/> (obj_shrapnel_piece): a spr_boss_hull plate in its grey image_blend,
    /// masked by the hull sprite's bounding box (it turns with image_angle).
    /// </summary>
    public class ShrapnelPiece : SetPieceEnemy
    {
        public const int Depth = -4;

        ShrapnelPieceBody Rules => (ShrapnelPieceBody)Core;
        SpriteRenderer spriteRenderer;

        public Scrapmental Parent => EnemyDirector.ViewOf<Scrapmental>(Rules.Parent.Id);
        public ShrapnelPieceMode Mode => Rules.Mode;
        public int Rgb => Rules.Rgb;
        public int Asset => Rules.Asset;
        public float Spin => Rules.Spin;
        public SpriteRenderer Renderer => spriteRenderer;

        protected override void Configure()
        {
            ShrapnelPieceBody.HullBoxPx(Rules.Asset, out float left, out float top, out float right, out float bottom);
            var size = new Vector2(right + 1f - left, bottom + 1f - top);
            var offset = new Vector2((left + right + 1f) / 2f - ShrapnelPieceBody.OriginX, -((top + bottom + 1f) / 2f - ShrapnelPieceBody.OriginY));
            UseBox(size, offset);
            AddDamageSource();
            spriteRenderer = HareArt.Show(gameObject, Rules.SpriteName, Depth);
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (Core == null || spriteRenderer == null) return;
            float grey = Rules.Rgb / 255f;
            spriteRenderer.color = new Color(grey, grey, grey, Rules.ImageAlpha);
        }
    }

    /// <summary>
    /// The view on <see cref="ShrapnelHarpoonBody"/> (obj_shrapnel_harpoon): spr_shrapnel_harpoon with its line to the boss
    /// (<c>draw_line_width_color(parent.x, parent.y, x, y, 3, c_gray, c_gray)</c>). Its par_rock and obj_shrapnel_piece
    /// collisions go to Core as <see cref="StrikeRock"/> and <see cref="GrabPiece"/>.
    /// </summary>
    public class ShrapnelHarpoon : SetPieceEnemy
    {
        public const string Sprite = "spr_shrapnel_harpoon";
        public const int Depth = -2;
        public const float LineWidthPx = 3f;
        /// <summary>GML c_gray.</summary>
        public static readonly Color LineColor = new Color32(128, 128, 128, 255);
        /// <summary>spr_shrapnel_harpoon's bbox (63..127 x 52..75 of 128x128, origin 64,64): the head end.</summary>
        static readonly Vector2 HitboxSizePx = new Vector2(65f, 24f);
        static readonly Vector2 HitboxOffsetPx = new Vector2(31.5f, 0f);

        ShrapnelHarpoonBody Rules => (ShrapnelHarpoonBody)Core;
        LineRenderer line;

        public Scrapmental Parent => EnemyDirector.ViewOf<Scrapmental>(Rules.Parent.Id);
        public ShrapnelHarpoonMode Mode => Rules.Mode;
        public float LengthPx => Rules.LengthPx;
        public bool Grabbing => Rules.Grabbing;
        public LineRenderer Line => line;

        protected override void Configure()
        {
            UseBox(HitboxSizePx, HitboxOffsetPx);
            AddDamageSource();
            HareArt.Show(gameObject, Sprite, Depth);
            var lineGo = new GameObject("line");
            lineGo.transform.SetParent(transform, false);
            line = Gml.AddLine(lineGo, LineColor, Gml.Px(LineWidthPx));
            line.sortingOrder = HareArt.SortingOrderForDepth(Depth) - 1; // drawn before draw_self
            Refresh();
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            if (Core == null || line == null) return;
            var boss = Rules.Parent;
            bool show = boss != null && !boss.IsDead;
            line.enabled = show;
            if (!show) return;
            line.SetPosition(0, new Vector3(boss.Position.X, boss.Position.Y, 0f));
            line.SetPosition(1, transform.position);
        }

        protected override void OnTouch(Collider2D other)
        {
            if (Rules.Mode == ShrapnelHarpoonMode.Fire && other.GetComponentInParent<Rocks.Rock>() != null)
            {
                Send(new StrikeRock(Core.Id));
                return;
            }
            var piece = other.GetComponentInParent<ShrapnelPiece>();
            if (piece != null && !piece.IsDead && piece.Mode == ShrapnelPieceMode.Float && !Rules.Grabbing)
                Send(new GrabPiece(Core.Id, piece.Core.Id));
        }
    }
}
