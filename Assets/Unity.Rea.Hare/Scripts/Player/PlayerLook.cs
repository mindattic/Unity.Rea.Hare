using UnityEngine;
using Unity.Hare.Art;
using Unity.Hare.Content;
using Unity.Hare.Effects;
using Unity.Hare.Rendering;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// obj_player as it is seen (U.HARE-T55), built in code from the imported art: its Draw event and the trails its
    /// Step and Alarms 2/3 leave. It reads the ship's Core state (<see cref="PlayerShip.Pose"/>, <see cref="ShipLook"/>,
    /// <see cref="ShipHealth.Color"/>, <see cref="ShipFlight"/>) and plays its events, and never changes the game:
    /// <list type="bullet">
    /// <item>the body: frame 0 of the spr_rabbit_* pose (draw_sprite_ext(sprite_index, 0, ...)) at the ship, turned to
    /// image_angle (the ship's own rotation), scaled by <c>scale</c> and blended with Alarm 5's colour;</item>
    /// <item>the held ground weapon, drawn beneath the body while perched: frame 0 of ground_weapon_asset at gunX/gunY
    /// (<c>lengthdir(-13, image_angle)</c> from the ship), turned to mouse_angle, in the same colour;</item>
    /// <item>nothing while the ship is dead;</item>
    /// <item>Alarm 2's white ef_smokeup and Alarm 3's amber, pink or cornflower ef_spark one step behind the ship
    /// (<c>x - hspeed, y - vspeed</c>), and the justspawned jet's orange ef_smoke 36 px behind it, all below everything
    /// (effect_create_below).</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerShipController))]
    public sealed class PlayerLook : SeatView
    {
        /// <summary>obj_player's depth 0. Draw draws the gun first, then the ship over it.</summary>
        const int Depth = 0;

        /// <summary>GameMaker's c_orange (the jet's colour).</summary>
        public static readonly Color Orange = new Color32(255, 160, 64, 255);

        SpriteRenderer body, gun;
        PlayerShipController ship;

        /// <summary>The ship's pose sprite.</summary>
        public SpriteRenderer Body => body;
        /// <summary>The held ground weapon's sprite.</summary>
        public SpriteRenderer Gun => gun;

        static LookTuning Tuning => GameContent.Current.Look;

        void Awake() => ResetState();

        /// <summary>Everything Awake does, callable from EditMode tests (Awake never runs there): builds the two sprites.</summary>
        public void ResetState()
        {
            ship = GetComponent<PlayerShipController>();
            if (body == null) body = MakeSprite("spr_rabbit", 0);
            if (gun == null) gun = MakeSprite("ground weapon", -1);
            _ = Seat; // subscribe to the ship's events now, not on the first draw
            Draw();
        }

        SpriteRenderer MakeSprite(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = HareSortingLayers.Default;
            renderer.sortingOrder = HareArt.SortingOrderForDepth(Depth) + order;
            return renderer;
        }

        void LateUpdate() => Draw();

        /// <summary>Draws what obj_player's Draw would this frame. Runs every LateUpdate.</summary>
        public void Draw()
        {
            if (body == null || gun == null) return;
            var core = Core;
            var look = core.Look;
            var manifest = HareArtManifest.Load();
            Color color = PlayerHealth.ColorOf(core.Health.Color);

            body.enabled = look.IsDrawn;
            body.sprite = manifest != null ? manifest.Frame(core.Pose) : null;
            body.color = color;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity; // the ship's transform already turns to image_angle
            body.transform.localScale = new Vector3(look.Scale, look.Scale, 1f);

            gun.enabled = look.IsDrawn && look.ShowsGun;
            gun.sprite = manifest != null ? manifest.Frame(look.HeldWeaponSprite) : null;
            gun.color = color;
            gun.transform.SetPositionAndRotation(GunPosition, Quaternion.Euler(0f, 0f, core.Flight.AimDirection));
            gun.transform.localScale = Vector3.one; // draw_sprite_ext(ground_weapon_asset, 0, gunX, gunY, 1, 1, mouse_angle, ...)
        }

        /// <summary>GML gunX/gunY: <c>x + lengthdir_x(-13, image_angle)</c>, likewise y.</summary>
        public Vector2 GunPosition =>
            (Vector2)transform.position + Gml.Dir(transform.eulerAngles.z) * Gml.Px(GameContent.Current.WeaponPhysicals.PerchedMuzzle.GunOffsetPx);

        /// <summary>GML <c>x - hspeed, y - vspeed</c>: where the ship was a step ago.</summary>
        Vector2 OneStepBehind()
        {
            var body2d = ship != null ? ship.GetComponent<Rigidbody2D>() : null;
            Vector2 velocity = body2d != null ? body2d.linearVelocity : Vector2.zero;
            return (Vector2)transform.position - velocity / GameClock.StepsPerSecond;
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case SmokeTrailPuffed _:
                    GmlEffects.CreateBelow(GmlEffectKind.SmokeUp, OneStepBehind(), 0, Color.white);
                    break;
                case SparkTrailSparked spark:
                    GmlEffects.CreateBelow(GmlEffectKind.Spark, OneStepBehind() + HareArt.GmlOffset(spark.JitterXPx, spark.JitterYPx), 0,
                        PlayerHealth.ColorOf(spark.Color));
                    break;
                case SpawnJetPuffed _:
                    // jetX/jetY = x + lengthdir(36, image_angle - 180).
                    Vector2 jet = (Vector2)transform.position + Gml.Dir(transform.eulerAngles.z - 180f) * Gml.Px(Tuning.JetOffsetPx);
                    GmlEffects.CreateBelow(GmlEffectKind.Smoke, jet, 0, Orange);
                    break;
                case LeaveSmokePuffed _:
                    // state_leave (U.HARE-T22): ef_smokeup (0, c_black) and ef_smoke (1, c_orange) at the same jetX/jetY.
                    Vector2 exhaust = (Vector2)transform.position + Gml.Dir(transform.eulerAngles.z - 180f) * Gml.Px(Tuning.JetOffsetPx);
                    GmlEffects.CreateBelow(GmlEffectKind.SmokeUp, exhaust, 0, Color.black);
                    GmlEffects.CreateBelow(GmlEffectKind.Smoke, exhaust, 1, Orange);
                    break;
            }
        }
    }
}
