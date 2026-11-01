using UnityEngine;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Player
{
    /// <summary>
    /// Anything the shield wall can stop: enemy bodies (GML: obj_dagger/obj_sawblade/... die on
    /// contact with obj_shieldwall) and projectiles (GML: obj_bullet/obj_rocket bounce off along
    /// the wall's normal). The enemy and weapon tickets implement this on their own components.
    /// </summary>
    public interface IShieldable
    {
        /// <param name="wall">The wall that was hit.</param>
        /// <param name="normal">Unit vector from the shield's centre out through the contact point.</param>
        void OnShieldHit(ShieldWall wall, Vector2 normal);
    }

    /// <summary>
    /// Ports obj_shieldwall (gmlHARE): a trigger hitbox spawned on the player by player_shield
    /// that follows the player every step, faces the aim direction (GML: player.mouse_angle) and
    /// flickers its alpha between 0.3 and 0.6 (GML: image_alpha = 0.1 * irandom_range(3,6)).
    /// It lives exactly as long as <see cref="PlayerShipController.IsShielding"/> is true; the
    /// controller creates and destroys it.
    /// </summary>
    [RequireComponent(typeof(PolygonCollider2D))]
    public class ShieldWall : MonoBehaviour
    {
        /// <summary>spr_shield_hitbox's mask: a half disc of about 47 px radius on the front (+x) side only (U.HARE-T90).</summary>
        public const float MaskRadiusPx = 47f;
        public const string SpriteName = "spr_shieldwall_anime";
        public const int SpriteFrames = 6;
        /// <summary>Alarm 1 advances the frame again every 3 unpaused steps.</summary>
        public const int AlarmEverySteps = 3;
        public const int Depth = 0;

        PlayerShipController owner;
        SpriteRenderer spriteRenderer;
        float stepRemainder;
        int alarmSteps;

        /// <summary>spr_shieldwall_anime frame (GML current_frame).</summary>
        public int CurrentFrame { get; private set; }

        public PlayerShipController Owner => owner;
        public float Alpha { get; private set; }

        /// <summary>Called by the controller right after AddComponent (Awake does not run in EditMode).</summary>
        public void Init(PlayerShipController player)
        {
            owner = player;
            var hitbox = GetComponent<PolygonCollider2D>();
            hitbox.isTrigger = true;
            const int arc = 12;
            var points = new Vector2[arc + 1];
            for (int i = 0; i <= arc; i++)
            {
                float a = (-90f + 180f * i / arc) * Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Gml.Px(MaskRadiusPx);
            }
            hitbox.points = points;
            spriteRenderer = Effects.HareArt.Show(gameObject, SpriteName, Depth);
            CurrentFrame = 0;
            alarmSteps = 0;
            stepRemainder = 0f;
            ApplyFrame();
            Follow();
        }

        /// <summary>
        /// Advances <paramref name="steps"/> GML steps: every Step moves current_frame on one (wrapping at 6) and
        /// Alarm 1, ringing every 3 steps, moves it on again.
        /// </summary>
        public void Tick(float steps)
        {
            stepRemainder += steps;
            while (stepRemainder >= 1f)
            {
                stepRemainder -= 1f;
                NextFrame();
                if (++alarmSteps >= AlarmEverySteps) { alarmSteps = 0; NextFrame(); }
            }
            ApplyFrame();
        }

        void NextFrame() => CurrentFrame = CurrentFrame < SpriteFrames - 1 ? CurrentFrame + 1 : 0;

        void ApplyFrame()
        {
            if (spriteRenderer != null) spriteRenderer.sprite = Effects.HareArt.Frame(SpriteName, CurrentFrame);
        }

        void LateUpdate()
        {
            if (owner == null) { Destroy(gameObject); return; }
            Follow();
            Tick(Effects.SpriteAnimation.StepsIn(Time.deltaTime));
            Flicker(Random.Range(3, 7));
        }

        /// <summary>GML End Step: x/y = player.x/y, image_angle = player.mouse_angle.</summary>
        public void Follow()
        {
            transform.position = owner.transform.position;
            transform.rotation = Quaternion.Euler(0f, 0f, owner.AimDirection);
        }

        /// <summary>GML: image_alpha = 0.1 * irandom_range(3,6).</summary>
        public void Flicker(int step)
        {
            Alpha = 0.1f * Mathf.Clamp(step, 3, 6);
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = Alpha;
                spriteRenderer.color = c;
            }
        }

        /// <summary>Unit vector from the shield's centre to <paramref name="point"/> (GML normal_detect).</summary>
        public Vector2 NormalAt(Vector2 point)
        {
            Vector2 n = point - (Vector2)transform.position;
            return n.sqrMagnitude > Mathf.Epsilon
                ? n.normalized
                : new Vector2(Mathf.Cos(owner.AimDirection * Mathf.Deg2Rad), Mathf.Sin(owner.AimDirection * Mathf.Deg2Rad));
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var target = other.GetComponentInParent<IShieldable>();
            if (target == null) return;
            target.OnShieldHit(this, NormalAt(other.transform.position));
        }
    }
}
