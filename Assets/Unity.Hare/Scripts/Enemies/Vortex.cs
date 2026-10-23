using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>A vortex's GML mode string.</summary>
    public enum VortexMode
    {
        Idle,
        Open,
        Spawn,
        Close
    }

    /// <summary>
    /// What par_enemy_emitter and obj_wormhole share (gmlHARE): a half-transparent spr_vortex drawn
    /// twice (image_angle turning -5 a step, back_angle -6) that Alarm 0 walks through
    /// idle → open → spawn → close → idle, each change setting first_pass so the Step event runs
    /// that mode's one-off work once. Open grows image_xscale 0.01 a step up to scale, close
    /// shrinks it to 0. Alarms run before Step, as in GML.
    /// </summary>
    public abstract class Vortex : MonoBehaviour
    {
        /// <summary>spr_vortex is 550x550 with its origin centred.</summary>
        public const float SpriteWidthPx = 550f;
        /// <summary>image_angle += rot_speed (-5); back_angle += rot_speed - 1.</summary>
        public const float RotSpeed = -5f;
        public const float GrowPerStep = 0.01f;
        public const float Alpha = 0.5f;

        protected const float Off = -1f;

        [SerializeField] Sprite vortexSprite;

        SpriteRenderer front, back;
        float alarm = Off;
        float imageAngle, backAngle;

        public EnemyKind Instance { get; set; } = EnemyKind.Hydra;
        /// <summary>GML scale: the open size the vortex grows to.</summary>
        public float Scale { get; set; } = 0.25f;
        /// <summary>GML image_xscale (= image_yscale).</summary>
        public float ImageScale { get; protected set; }
        public VortexMode Mode { get; private set; } = VortexMode.Idle;
        public float ImageAngle => imageAngle;
        public float BackAngle => backAngle;
        /// <summary>Steps until Alarm 0, or -1 when unset.</summary>
        public float AlarmSteps => alarm;
        public Rect Room { get; protected set; }
        public Unity.Hare.Player.PlayerShipController Player { get; set; }

        protected bool FirstPass { get; set; } = true;

        /// <summary>The shared GML Create: invisible, half alpha, idle, random back_angle.</summary>
        protected void SetupVortex(Rect? room)
        {
            Room = room ?? Gml.Room;
            ImageScale = 0f;
            Mode = VortexMode.Idle;
            FirstPass = true;
            backAngle = Random.Range(0, 360);
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

        protected void SetAlarm(float steps) => alarm = steps;

        protected void SetAngles(float image, float back)
        {
            imageAngle = Mathf.Repeat(image, 360f);
            backAngle = Mathf.Repeat(back, 360f);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (Alarm 0, then Step), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (this == null) return;
            float steps = dt * Gml.StepsPerSecond;
            if (alarm >= 0f)
            {
                alarm -= steps;
                if (alarm <= 1e-3f)
                {
                    alarm = Off;
                    Advance();
                }
            }

            if (!Gml.Paused)
            {
                imageAngle = Mathf.Repeat(imageAngle + RotSpeed * steps, 360f);
                backAngle = Mathf.Repeat(backAngle + (RotSpeed - 1f) * steps, 360f);
            }

            StepEvent(steps);
            if (this != null) Draw();
        }

        /// <summary>GML Alarm 0: the next mode, with first_pass set.</summary>
        void Advance()
        {
            switch (Mode)
            {
                case VortexMode.Idle: Mode = VortexMode.Open; break;
                case VortexMode.Open: Mode = VortexMode.Spawn; break;
                case VortexMode.Spawn: Mode = VortexMode.Close; break;
                default: Mode = VortexMode.Idle; break;
            }
            FirstPass = true;
        }

        protected abstract void StepEvent(float steps);

        protected void Grow(float steps)
        {
            ImageScale = ImageScale < Scale ? Mathf.Min(Scale, ImageScale + GrowPerStep * steps) : Scale;
        }

        /// <summary>Shrinks toward 0; true once closed.</summary>
        protected bool Shrink(float steps)
        {
            ImageScale -= GrowPerStep * steps;
            if (ImageScale > 1e-4f) return false; // float slack, so 100 steps close a scale of 1
            ImageScale = 0f;
            return true;
        }

        /// <summary>GML instance_create(x, y, instance) plus ef_ring and ef_smoke at the vortex.</summary>
        protected Component Release()
        {
            var enemy = EnemySpawner.Spawn(Instance, transform.position, Room, Player);
            Released?.Invoke(this, enemy);
            return enemy;
        }

        /// <summary>Raised with each enemy the vortex creates (effects and sounds hang off this).</summary>
        public event System.Action<Vortex, Component> Released;

        /// <summary>The Draw event: both spr_vortex layers at image_xscale, alpha 0.5.</summary>
        void Draw()
        {
            if (front == null) return;
            var s = new Vector3(ImageScale, ImageScale, 1f);
            front.transform.localScale = s;
            back.transform.localScale = s;
            front.transform.rotation = Quaternion.Euler(0f, 0f, imageAngle);
            back.transform.rotation = Quaternion.Euler(0f, 0f, backAngle);
        }

        protected void Remove()
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
