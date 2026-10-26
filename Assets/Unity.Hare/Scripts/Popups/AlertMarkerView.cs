using System;
using UnityEngine;
using Unity.Hare.Effects;

namespace Unity.Hare.Popups
{
    /// <summary>
    /// obj_alert drawn and played (U.HARE-T27): Core's <see cref="AlertMarker"/> as spr_alertmarker (its draw_self,
    /// animated at image_speed 0.2 and turned by image_angle) at depth -10, and its Alarm 0's red size-0 ef_ring through
    /// <see cref="GmlEffects"/>. The map room (U.HARE-T19/T63) steps it on its own clock against obj_earthball, with
    /// <c>obj_map.mode == "idle"</c>, and gives the room-pixel to world mapping.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AlertMarkerView : MonoBehaviour
    {
        public const string ObjectName = "obj_alert";

        Func<double, double, Vector2> toWorld;
        Color pingColor = Color.red;

        /// <summary>The marker's rules.</summary>
        public AlertMarker Marker { get; private set; }

        /// <summary>The sprite as drawn.</summary>
        public SpriteRenderer Renderer { get; private set; }

        /// <summary>An ef_ring went off (where, in the world).</summary>
        public event Action<Vector2> Pinged;

        /// <summary><c>instance_create(0, 0, obj_alert)</c> under <paramref name="parent"/>, drawn through <paramref name="toWorld"/> (room pixels to world).</summary>
        public static AlertMarkerView Create(Transform parent, AlertMarker marker, Func<double, double, Vector2> toWorld)
        {
            var go = new GameObject(ObjectName);
            if (parent != null) go.transform.SetParent(parent, false);
            var view = go.AddComponent<AlertMarkerView>();
            view.Marker = marker ?? throw new ArgumentNullException(nameof(marker));
            view.toWorld = toWorld ?? ((x, y) => new Vector2(GameClock.Px((float)x), -GameClock.Px((float)y)));
            view.Renderer = HareArt.Show(go, marker.Definition.Sprite, marker.Definition.Depth);
            if (!ColorUtility.TryParseHtmlString(marker.Definition.PingColor, out view.pingColor)) view.pingColor = Color.red;
            view.Draw();
            return view;
        }

        /// <summary>
        /// One step against obj_earthball at room pixel (<paramref name="earthX"/>, <paramref name="earthY"/>) turned
        /// <paramref name="earthAngle"/> degrees, the map idle or not; true when its Alarm 0 pinged.
        /// </summary>
        public bool Step(double earthX, double earthY, double earthAngle, bool mapIdle)
        {
            bool pinged = Marker.Step(earthX, earthY, earthAngle, mapIdle, out double px, out double py);
            if (pinged)
            {
                var at = toWorld(px, py);
                GmlEffects.CreateAbove(GmlEffectKind.Ring, at, Marker.Definition.PingSize, pingColor);
                Pinged?.Invoke(at);
            }
            Draw();
            return pinged;
        }

        /// <summary>draw_self: the frame, the place and the turn.</summary>
        void Draw()
        {
            var at = toWorld(Marker.X, Marker.Y);
            transform.position = new Vector3(at.x, at.y, transform.position.z);
            transform.rotation = Quaternion.Euler(0f, 0f, (float)Marker.ImageAngle);
            if (Renderer != null) Renderer.sprite = HareArt.Frame(Marker.Definition.Sprite, Marker.Frame);
        }
    }
}
