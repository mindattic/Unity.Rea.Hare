using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on a <see cref="VortexBody"/>: what par_enemy_emitter and obj_wormhole share (gmlHARE), a half-transparent
    /// spr_vortex drawn twice (image_angle turning -5 a step, back_angle -6). Core's alarms walk it through
    /// idle, open, spawn, close; this sends each step to the world, draws the two layers at Core's scale and angles,
    /// and plays what the vortex released.
    /// </summary>
    public abstract class Vortex : MonoBehaviour, IEnemyEventSink
    {
        /// <summary>spr_vortex is 550x550 with its origin centred.</summary>
        public const float SpriteWidthPx = VortexBody.SpriteWidthPx;
        public const float RotSpeed = VortexBody.RotSpeed;
        public const float GrowPerStep = VortexBody.GrowPerStep;
        public const float Alpha = VortexBody.Alpha;

        [SerializeField] Sprite vortexSprite;

        SpriteRenderer front, back;

        /// <summary>The Core state this view shows (read-only for presentation).</summary>
        public VortexBody Core { get; private set; }

        public EnemyKind Instance => Core.Instance;
        /// <summary>GML scale: the open size the vortex grows to.</summary>
        public float Scale => Core.Scale;
        /// <summary>GML image_xscale (= image_yscale).</summary>
        public float ImageScale => Core.ImageScale;
        public VortexMode Mode => Core.Mode;
        public float ImageAngle => Core.ImageAngle;
        public float BackAngle => Core.BackAngle;
        /// <summary>Steps until Alarm 0, or -1 when unset.</summary>
        public float AlarmSteps => Core.AlarmSteps;
        public Rect Room => Core.Room.ToRect();
        /// <summary>The ship the enemies it releases chase (found in the scene when empty).</summary>
        public PlayerShipController Player { get; set; }

        /// <summary>Raised with each enemy the vortex creates (effects and sounds hang off this).</summary>
        public event System.Action<Vortex, Component> Released;

        /// <summary>Shows <paramref name="vortex"/>: GML Create has run in Core already.</summary>
        public void Attach(VortexBody vortex, PlayerShipController player)
        {
            Core = vortex;
            if (player != null) Player = player;
            EnemyDirector.Register(vortex.Id, this);
            if (vortexSprite != null && front == null)
            {
                back = Layer("Back", 0);
                front = Layer("Front", 1);
            }
            Draw();
        }

        SpriteRenderer Layer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = vortexSprite;
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, Alpha);
            return r;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (Alarm 0, then Step), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (this == null || Core == null || Core.IsGone) return;
            EnemyDirector.Execute(new TickVortex(Core.Id, dt));
            if (this != null) Draw();
        }

        /// <summary>The Draw event: both spr_vortex layers at image_xscale, alpha 0.5.</summary>
        void Draw()
        {
            Vector2 p = Core.Position.ToVector2();
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            if (front == null) return;
            var s = new Vector3(Core.ImageScale, Core.ImageScale, 1f);
            front.transform.localScale = s;
            back.transform.localScale = s;
            front.transform.rotation = Quaternion.Euler(0f, 0f, Core.ImageAngle);
            back.transform.rotation = Quaternion.Euler(0f, 0f, Core.BackAngle);
        }

        public virtual void OnEnemyEvent(EnemyEvent e)
        {
            switch (e)
            {
                case VortexReleased released:
                {
                    var view = EnemyDirector.ViewOf<EnemyBodyView>(released.Enemy.Id);
                    if (view != null && Player != null) view.Player = Player;
                    Released?.Invoke(this, view);
                    break;
                }
                case VortexClosed _:
                    Remove();
                    break;
            }
        }

        protected void Remove()
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
