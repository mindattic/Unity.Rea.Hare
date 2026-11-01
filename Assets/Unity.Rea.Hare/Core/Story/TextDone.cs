using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Story
{
    /// <summary>
    /// obj_textdone as data (U.HARE-T87): the blinking "text done" marker in the bottom right corner of a typed box. The
    /// defaults are the original's, read from HARE.gmx: spr_textdone (11x13, four frames) drawn at
    /// <c>image_xscale = image_yscale = 8</c> at <c>x = view_wview - 90, y = view_hview - 90</c> (GUI pixels, the view's
    /// corner, not the box's) with <c>image_speed = 0.1</c>, at depth -1001 above obj_conversation's -1000.
    /// </summary>
    [Serializable]
    public class TextDoneDefinition
    {
        public string Sprite = "spr_textdone";
        public int Frames = 4;
        /// <summary>spr_textdone's size: 11x13.</summary>
        public int Width = 11;
        public int Height = 13;
        /// <summary>Create: <c>image_xscale = 8; image_yscale = 8</c>.</summary>
        public int Scale = 8;
        /// <summary>Create: <c>x = view_wview - 90</c>.</summary>
        public int MarginRight = 90;
        /// <summary>Create: <c>y = view_hview - 90</c>.</summary>
        public int MarginBottom = 90;
        /// <summary>Create: <c>image_speed = 0.1</c> frames a step.</summary>
        public double ImageSpeed = 0.1;
        public int Depth = -1001;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".Frames", Frames);
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            ValidationHelpers.Positive(errors, path + ".Scale", Scale);
            if (!(ImageSpeed >= 0d)) errors.Add($"{path}.ImageSpeed: must not be negative (was {ImageSpeed}).");
        }
    }

    /// <summary>
    /// obj_textdone, engine-free: one instance made when a box's line finishes typing (print_complete) and destroyed
    /// when it is sent away (next_line) or the line starts again, so each finished line blinks from frame 0. Its
    /// image_index runs on by image_speed every step it exists and wraps at the sprite's frames.
    /// </summary>
    public sealed class TextDoneMarker
    {
        public TextDoneMarker(TextDoneDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public TextDoneDefinition Definition { get; }

        /// <summary>Whether the instance exists.</summary>
        public bool Visible { get; private set; }

        /// <summary>GML image_index.</summary>
        public double ImageIndex { get; private set; }

        /// <summary>The frame draw_self shows.</summary>
        public int Frame => Definition.Frames <= 0 ? 0 : (int)Math.Floor(ImageIndex) % Definition.Frames;

        /// <summary>The sprite's top left, in GUI pixels, for a view of the given size.</summary>
        public double X(int viewWidth) => viewWidth - Definition.MarginRight;
        public double Y(int viewHeight) => viewHeight - Definition.MarginBottom;

        /// <summary>One step: make it when <paramref name="shouldShow"/> turns true, destroy it when false, else run its animation.</summary>
        public void Step(bool shouldShow)
        {
            if (!shouldShow)
            {
                Visible = false;
                ImageIndex = 0d;
                return;
            }
            if (!Visible)
            {
                Visible = true;
                ImageIndex = 0d;
                return;
            }
            if (Definition.Frames > 0)
            {
                ImageIndex += Definition.ImageSpeed;
                if (ImageIndex >= Definition.Frames) ImageIndex -= Definition.Frames;
            }
        }

        /// <summary>next_line's <c>with(obj_textdone) instance_destroy()</c>.</summary>
        public void Destroy()
        {
            Visible = false;
            ImageIndex = 0d;
        }
    }
}
