using UnityEngine;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Enemies;

namespace Unity.Rea.Hare.Pickups
{
    /// <summary>
    /// Draws one obj_carrot (spr_pickup_health) or obj_coin (spr_coin_blue/silver/gold) at depth 0 where its Core body is,
    /// and carries its mask, a trigger the ship's mask finds: the carrot's rectangle, the coin's ellipse (a 32 px circle),
    /// both from the sprites' bounding boxes. It changes nothing itself.
    /// </summary>
    public sealed class PickupView : MonoBehaviour
    {
        /// <summary>par_pickup's depth.</summary>
        public const int Depth = 0;

        public Pickup Pickup { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        public static PickupView Create(Pickup pickup)
        {
            var go = new GameObject(pickup.Kind == PickupKind.Carrot ? "obj_carrot" : "obj_coin");
            var view = go.AddComponent<PickupView>();
            view.Pickup = pickup;
            view.Renderer = HareArt.Show(go, pickup.SpriteName, Depth);

            var tuning = GameContent.Current.Pickups;
            var box = pickup.Kind == PickupKind.Carrot ? tuning.CarrotBox : tuning.CoinBox;
            // The bbox's pixels l..r (and t..b, GML y down) about the origin, as a world-space shape.
            var size = new Vector2(GameClock.Px(box[2] - box[0] + 1f), GameClock.Px(box[3] - box[1] + 1f));
            var centre = new Vector2(GameClock.Px((box[0] + box[2] + 1f) / 2f), -GameClock.Px((box[1] + box[3] + 1f) / 2f));
            if (pickup.Kind == PickupKind.Coin)
            {
                var circle = go.AddComponent<CircleCollider2D>(); // colkind 2, a 32 x 32 ellipse
                circle.isTrigger = true;
                circle.radius = size.x / 2f;
                circle.offset = centre;
            }
            else
            {
                var rect = go.AddComponent<BoxCollider2D>(); // colkind 1
                rect.isTrigger = true;
                rect.size = size;
                rect.offset = centre;
            }
            view.Present();
            return view;
        }

        /// <summary>Puts the view where the field has the pickup.</summary>
        public void Present()
        {
            if (Pickup == null) return;
            var at = Pickup.Position.ToVector2();
            transform.position = new Vector3(at.x, at.y, transform.position.z);
        }

        void OnDestroy()
        {
            if (Pickup != null) PickupDirector.Dispose(Pickup.Id, this);
        }
    }

    /// <summary>
    /// Draws one obj_chip (spr_chip1..5 at half scale, depth 1) or obj_cartridge (spr_ammo_cartridge or spr_ammo_shell,
    /// depth 0) where its Core body is, turned to its image_angle. No mask: nothing collides with them.
    /// </summary>
    public sealed class ScrapView : MonoBehaviour
    {
        /// <summary>obj_chip's depth (obj_cartridge's is 0).</summary>
        public const int ChipDepth = 1;

        public Scrap Scrap { get; private set; }
        public SpriteRenderer Renderer { get; private set; }

        public static ScrapView Create(Scrap scrap)
        {
            var go = new GameObject(scrap.Kind == ScrapKind.Chip ? "obj_chip" : "obj_cartridge");
            var view = go.AddComponent<ScrapView>();
            view.Scrap = scrap;
            view.Renderer = HareArt.Show(go, scrap.SpriteName, scrap.Kind == ScrapKind.Chip ? ChipDepth : 0);
            go.transform.localScale = new Vector3(scrap.Scale, scrap.Scale, 1f);
            view.Present();
            return view;
        }

        public void Present()
        {
            if (Scrap == null) return;
            var at = Scrap.Position.ToVector2();
            transform.SetPositionAndRotation(new Vector3(at.x, at.y, transform.position.z), Quaternion.Euler(0f, 0f, Scrap.ImageAngle));
        }

        void OnDestroy()
        {
            if (Scrap != null) PickupDirector.Dispose(Scrap.Id, this);
        }
    }
}
