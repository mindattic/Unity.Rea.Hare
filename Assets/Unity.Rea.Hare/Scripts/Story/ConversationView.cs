using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Audio;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Story
{
    /// <summary>
    /// obj_conversation drawn and played (U.HARE-T28): Core's <see cref="ConversationBox"/> on a 1920x1080 GUI layer, as
    /// its Draw GUI draws it: spr_conversation_box at (x, y), then spr_comm_frame_back, the speaker's portrait and
    /// spr_comm_frame at <c>(x + frame_margin_left, y + frame_margin_top)</c>, the speaker's name centred under the frame
    /// (fnt_point, <see cref="HareTextStyles.ConversationName"/>, size-2 outline) and the typed line at
    /// <c>(x + text_margin_left, y + text_margin_top)</c> (fnt_score, <see cref="HareTextStyles.Conversation"/>, size-2
    /// outline). It steps itself on the room's clock (<c>room_speed</c> steps a second), plays the sounds the box asks for
    /// (sfx_flick, sfx_woof, and sfx_type restarted for each typed character), and sends its key press (vk_anykey) and
    /// global left press to <see cref="ConversationBox.NextLine"/>. obj_map (U.HARE-T19/T63) makes one with
    /// <see cref="ConversationDefinition.MapScript"/>'s line and slides it in and out.
    /// </summary>
    public sealed class ConversationView : VisualElement
    {
        public const string ElementName = "obj_conversation";

        readonly ConversationBox box;
        readonly float stepsPerSecond;
        readonly VisualElement text;
        readonly VisualElement speaker;
        readonly TextDoneView textDone;
        InputAction anyKey, leftPress;
        double pending;
        string drawn;

        ConversationView(ConversationBox box, float stepsPerSecond)
        {
            this.box = box;
            this.stepsPerSecond = Mathf.Max(1f, stepsPerSecond);
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = box.Definition.Width;
            style.height = box.Definition.Height;

            var d = box.Definition;
            var manifest = HareArtManifest.Load();
            Add(Image(manifest, d.BoxSprite, 0, 0, d.Width, d.Height));
            float fx = (float)(box.FrameX - box.X), fy = (float)(box.FrameY - box.Y);
            Add(Image(manifest, d.FrameBackSprite, fx, fy, d.FrameWidth, d.FrameHeight));
            Add(Image(manifest, d.PortraitSprite, fx, fy, d.FrameWidth, d.FrameHeight));
            Add(Image(manifest, d.FrameSprite, fx, fy, d.FrameWidth, d.FrameHeight));

            // draw_set_halign(fa_center): the name centred on the frame's middle, its top at the frame's bottom.
            speaker = HareUi.OutlineText(box.Speaker, d.NameOutline, HareTextStyles.ConversationName.UssClass, this);
            speaker.style.position = Position.Absolute;
            speaker.style.left = fx;
            speaker.style.width = d.FrameWidth;
            speaker.style.top = (float)(box.NameY - box.Y);
            speaker.style.unityTextAlign = TextAnchor.UpperCenter;

            text = HareUi.OutlineText("", d.Typewriter.Outline, HareTextStyles.Conversation.UssClass, this);
            text.style.position = Position.Absolute;
            text.style.left = (float)(box.TextX - box.X);
            text.style.top = (float)(box.TextY - box.Y);
            text.style.unityTextAlign = TextAnchor.UpperLeft;

            // obj_textdone (depth -1001, above the box): the view's corner, so placed against the box's moving top.
            textDone = new TextDoneView(box.TextDone, box.ViewWidth, box.ViewHeight);
            Add(textDone);

            RegisterCallback<AttachToPanelEvent>(_ => EnableKeys());
            RegisterCallback<DetachFromPanelEvent>(_ => DisableKeys());
            Present();
            schedule.Execute(state => Tick(state.deltaTime / 1000f)).Every(0);
        }

        /// <summary>The box's rules.</summary>
        public ConversationBox Box => box;

        /// <summary>The typed line as drawn.</summary>
        public VisualElement TextElement => text;

        /// <summary>The speaker's name as drawn.</summary>
        public VisualElement SpeakerElement => speaker;

        /// <summary>obj_textdone as drawn.</summary>
        public TextDoneView TextDone => textDone;

        /// <summary>Every sound the box played, in order, for whoever watches.</summary>
        public event Action<string> SoundPlayed;

        /// <summary><c>instance_create(0, 0, obj_conversation)</c> on <paramref name="gui"/>, stepping <paramref name="stepsPerSecond"/> times a second.</summary>
        public static ConversationView Show(VisualElement gui, ConversationBox box, float stepsPerSecond)
        {
            var view = new ConversationView(box ?? throw new ArgumentNullException(nameof(box)), stepsPerSecond);
            gui.Add(view);
            return view;
        }

        /// <summary>One step of the box.</summary>
        public void Step()
        {
            Play(box.Step(), restartTyping: true);
            Present();
        }

        /// <summary>Its key press and global left pressed events: next_line, once the line is out and the box idle.</summary>
        public void NextLine()
        {
            Play(box.NextLine(), restartTyping: false);
            Present();
        }

        void Play(IReadOnlyList<string> sounds, bool restartTyping)
        {
            foreach (var sound in sounds)
            {
                // The typing's audio_stop_sound then audio_play_sound; the slides and next_line only play.
                if (restartTyping && sound == box.Definition.Typewriter.Sound) HareAudio.Restart(sound);
                else HareAudio.Play(sound);
                SoundPlayed?.Invoke(sound);
            }
        }

        void Tick(float seconds)
        {
            if (panel == null) return;
            if ((anyKey != null && anyKey.WasPressedThisFrame()) || (leftPress != null && leftPress.WasPressedThisFrame())) NextLine();
            pending += Mathf.Max(0f, seconds) * stepsPerSecond;
            int steps = Math.Min((int)Math.Floor(pending), 8);
            pending -= Math.Floor(pending);
            for (int i = 0; i < steps; i++) Step();
        }

        void Present()
        {
            style.top = (float)box.Y;
            textDone.Present(box.Y);
            style.display = box.Mode == ConversationMode.Off && box.Y >= box.StartY ? DisplayStyle.None : DisplayStyle.Flex;
            string now = box.Typewriter.DrawText;
            if (now != drawn)
            {
                drawn = now;
                HareUi.SetOutlineText(text, now);
            }
        }

        static VisualElement Image(HareArtManifest manifest, string sprite, float left, float top, float width, float height)
        {
            var image = new VisualElement { name = sprite, pickingMode = PickingMode.Ignore };
            image.style.position = Position.Absolute;
            image.style.left = left;
            image.style.top = top;
            image.style.width = width;
            image.style.height = height;
            var frame = manifest != null ? manifest.Frame(sprite) : null;
            if (frame != null) image.style.backgroundImage = new StyleBackground(frame);
            else Debug.LogWarning("[Unity.Rea.Hare] " + sprite + " is not imported.");
            return image;
        }

        void EnableKeys()
        {
            if (anyKey != null) return;
            anyKey = new InputAction("AnyKey", InputActionType.Button, "<Keyboard>/anyKey");
            leftPress = new InputAction("LeftPress", InputActionType.Button, "<Mouse>/leftButton");
            anyKey.Enable();
            leftPress.Enable();
        }

        void DisableKeys()
        {
            if (anyKey == null) return;
            anyKey.Disable();
            anyKey.Dispose();
            anyKey = null;
            leftPress.Disable();
            leftPress.Dispose();
            leftPress = null;
        }
    }
}
