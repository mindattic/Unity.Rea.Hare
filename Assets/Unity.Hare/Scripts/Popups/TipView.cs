using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Hare.Art;
using Unity.Hare.Audio;
using Unity.Hare.UI;

namespace Unity.Hare.Popups
{
    /// <summary>
    /// obj_tip drawn and played (U.HARE-T27): Core's <see cref="TipBox"/> on a 1920x1080 GUI layer, as its Draw GUI draws
    /// it: spr_tip_box at 0.7 alpha with its top left at (x, y), then the text at alpha 1, fa_left, fnt_point
    /// (<see cref="HareTextStyles.MenuText"/>), size-1 outline, at <c>(x + margin, y + margin)</c>. It steps itself on
    /// the room's clock (<c>room_speed</c> steps a second; it has no paused check), plays sfx_flick when a
    /// slide starts, and goes at once on its key press (vk_anykey, the Input System's any key) or a left press on the
    /// box. obj_map (U.HARE-T19/T63) and obj_drop_intro (U.HARE-T62) make one with <see cref="TipDefinition.MapText"/> and
    /// <see cref="TipDefinition.IntroText"/>.
    /// </summary>
    public sealed class TipView : VisualElement
    {
        public const string ElementName = "obj_tip";

        readonly TipBox tip;
        readonly float stepsPerSecond;
        readonly VisualElement box;
        readonly VisualElement text;
        InputAction anyKey;
        double pending;

        TipView(TipBox tip, float stepsPerSecond)
        {
            this.tip = tip;
            this.stepsPerSecond = Mathf.Max(1f, stepsPerSecond);
            name = ElementName;
            pickingMode = PickingMode.Position;
            style.position = Position.Absolute;
            style.width = tip.Definition.Width;
            style.height = tip.Definition.Height;

            box = new VisualElement { name = "spr_tip_box", pickingMode = PickingMode.Ignore };
            box.style.position = Position.Absolute;
            box.style.left = 0;
            box.style.top = 0;
            box.style.width = tip.Definition.Width;
            box.style.height = tip.Definition.Height;
            box.style.opacity = tip.Definition.Alpha;
            var manifest = HareArtManifest.Load();
            var sprite = manifest != null ? manifest.Frame(tip.Definition.Sprite) : null;
            if (sprite != null) box.style.backgroundImage = new StyleBackground(sprite);
            else Debug.LogWarning("[Unity.Hare] " + tip.Definition.Sprite + " is not imported.");
            Add(box);

            text = HareUi.OutlineText(tip.DrawText, tip.Definition.Outline, HareTextStyles.MenuText.UssClass, this);
            text.style.position = Position.Absolute;
            text.style.left = tip.Definition.Margin;
            text.style.top = tip.Definition.Margin;
            text.style.unityTextAlign = TextAnchor.UpperLeft;

            // Its left pressed event (the box is spr_tip_box's rectangle).
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button == 0) Dismiss();
            });
            RegisterCallback<AttachToPanelEvent>(_ => EnableKeys());
            RegisterCallback<DetachFromPanelEvent>(_ => DisableKeys());
            Place();
            schedule.Execute(state => Tick(state.deltaTime / 1000f)).Every(0);
        }

        /// <summary>The tip's rules.</summary>
        public TipBox Tip => tip;

        /// <summary>The text as drawn.</summary>
        public VisualElement TextElement => text;

        /// <summary>A step of its own was played (with whether sfx_flick played), for whoever watches.</summary>
        public event Action<bool> Stepped;

        /// <summary><c>instance_create(0, 0, obj_tip)</c> on <paramref name="gui"/>, stepping <paramref name="stepsPerSecond"/> times a second.</summary>
        public static TipView Show(VisualElement gui, TipBox tip, float stepsPerSecond)
        {
            var view = new TipView(tip, stepsPerSecond);
            gui.Add(view);
            return view;
        }

        /// <summary>Its key press and left pressed events: <c>instance_destroy()</c>.</summary>
        public void Dismiss()
        {
            tip.Dismiss();
            RemoveFromHierarchy();
        }

        /// <summary>One Step; removes it once it has gone.</summary>
        public void Step()
        {
            if (!tip.Step(out bool sound))
            {
                RemoveFromHierarchy();
                return;
            }
            if (sound) HareAudio.Play(tip.Definition.Sound);
            Place();
            Stepped?.Invoke(sound);
        }

        void Tick(float seconds)
        {
            if (tip.Gone || panel == null) return;
            if (anyKey != null && anyKey.WasPressedThisFrame())
            {
                Dismiss();
                return;
            }
            pending += Mathf.Max(0f, seconds) * stepsPerSecond;
            int steps = Math.Min((int)Math.Floor(pending), 8);
            pending -= Math.Floor(pending);
            for (int i = 0; i < steps && !tip.Gone; i++) Step();
        }

        void Place()
        {
            style.left = (float)tip.X;
            style.top = (float)tip.Y;
        }

        void EnableKeys()
        {
            if (anyKey != null) return;
            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            anyKey.Enable();
        }

        void DisableKeys()
        {
            if (anyKey == null) return;
            anyKey.Disable();
            anyKey.Dispose();
            anyKey = null;
        }
    }
}
