using UnityEngine;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.UI
{
    /// <summary>
    /// obj_losthealth drawn (U.HARE-T54): Core's <see cref="LostHealthPip"/> as spr_lost_health on the 1920x1080 GUI
    /// layer (its Draw GUI's draw_self, origin top left), stepped every frame on the GML clock (standing still while
    /// paused) and removed once it has faded out.
    /// </summary>
    public sealed class LostHealthView : VisualElement
    {
        public const string ElementName = "obj_losthealth";

        readonly LostHealthPip pip;

        LostHealthView(LostHealthPip pip)
        {
            this.pip = pip;
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            var manifest = HareArtManifest.Load();
            var sprite = manifest != null ? manifest.Frame(LostHealthPip.Sprite) : null;
            if (sprite != null)
            {
                style.backgroundImage = new StyleBackground(sprite);
                style.width = sprite.rect.width;
                style.height = sprite.rect.height;
            }
            else Debug.LogWarning("[Unity.Hare] " + LostHealthPip.Sprite + " is not imported.");
            Place();
            schedule.Execute(state => Step(state.deltaTime / 1000f)).Every(0);
        }

        /// <summary>The pip's rules.</summary>
        public LostHealthPip Pip => pip;

        /// <summary>instance_create(..., obj_losthealth) on <paramref name="hud"/>.</summary>
        public static LostHealthView Show(VisualElement hud, LostHealthPip pip)
        {
            var view = new LostHealthView(pip);
            hud.Add(view);
            return view;
        }

        /// <summary>Its Step for <paramref name="seconds"/>; removes it once gone.</summary>
        public void Step(float seconds)
        {
            if (!pip.Step(Mathf.Max(0f, seconds), Gml.Paused))
            {
                RemoveFromHierarchy();
                return;
            }
            Place();
        }

        void Place()
        {
            style.left = pip.X;
            style.top = pip.Y;
            style.opacity = pip.Alpha;
        }
    }
}
