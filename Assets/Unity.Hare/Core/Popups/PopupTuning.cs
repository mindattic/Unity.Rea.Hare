using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Popups
{
    /// <summary>
    /// obj_float_text's numbers (U.HARE-T27): its Create's defaults, the colours its Step cycles through while it flashes,
    /// where its Draw puts the text, and what every kill and broken rock passes to show_float_text_ext.
    /// </summary>
    [Serializable]
    public class FloatTextDefinition
    {
        /// <summary>Create: <c>alpha = 1</c>.</summary>
        public double StartAlpha = 1;
        /// <summary>Create: <c>color = c_white</c>.</summary>
        public string Color = "#FFFFFF";
        /// <summary>Create: <c>outline = c_black</c>.</summary>
        public string OutlineColor = "#000000";
        /// <summary>Create: <c>flash = true</c>.</summary>
        public bool Flash = true;
        /// <summary>Create: <c>fadespeed = 0.008</c>, taken off alpha every step.</summary>
        public double FadeSpeed = 0.008;
        /// <summary>Create: <c>floatspeed = 1</c>, px a step it rises.</summary>
        public double FloatSpeed = 1;
        /// <summary>
        /// Step, while flashing: <c>switch(colorCycle)</c>'s colours for colorCycle 0..4 (<c>case 0: case 1: c_white</c>,
        /// <c>2: c_red</c>, <c>3: c_yellow</c>, <c>4: c_blue</c>); a colorCycle past the list keeps the colour it had.
        /// </summary>
        public string[] FlashColors = { "#FFFFFF", "#FFFFFF", "#FF0000", "#FFFF00", "#0000FF" };
        /// <summary>Step: <c>if(colorCycle &lt;= 4) colorCycle += 1; else colorCycle = 0</c>.</summary>
        public int FlashCycleTop = 4;
        /// <summary>Draw: <c>draw_text_outline_color(x, y-10, 1, ...)</c>'s y offset.</summary>
        public int DrawOffsetY = -10;
        /// <summary>Draw: the outline size.</summary>
        public int Outline = 1;
        /// <summary>Every <c>current_score += n</c> in the original: <c>show_float_text_ext(x, y, string(n), true, 0.01, 1)</c>.</summary>
        public bool ScoreFlash = true;
        public double ScoreFadeSpeed = 0.01;
        public double ScoreFloatSpeed = 1;

        internal void Validate(List<string> errors, string path)
        {
            if (StartAlpha <= 0 || StartAlpha > 1) errors.Add($"{path}.StartAlpha: must be above 0 and at most 1 (was {StartAlpha}).");
            CheckColor(errors, path + ".Color", Color);
            CheckColor(errors, path + ".OutlineColor", OutlineColor);
            ValidationHelpers.Positive(errors, path + ".FadeSpeed", (float)FadeSpeed);
            ValidationHelpers.Positive(errors, path + ".ScoreFadeSpeed", (float)ScoreFadeSpeed);
            if (FlashColors == null || FlashColors.Length == 0) errors.Add(path + ".FlashColors: must list at least one colour.");
            else for (int i = 0; i < FlashColors.Length; i++) CheckColor(errors, $"{path}.FlashColors[{i}]", FlashColors[i]);
            ValidationHelpers.NonNegative(errors, path + ".FlashCycleTop", FlashCycleTop);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
        }

        internal static void CheckColor(List<string> errors, string path, string value)
        {
            if (!global::Unity.Hare.Rooms.StartupTuning.IsHexColor(value)) errors.Add($"{path}: must be #RRGGBB (was {value ?? "null"}).");
        }
    }

    /// <summary>obj_tip's numbers (U.HARE-T27) and the two tips the original makes (obj_map's and obj_drop_intro's).</summary>
    [Serializable]
    public class TipDefinition
    {
        /// <summary>The object's sprite, drawn by its Draw GUI's draw_self.</summary>
        public string Sprite = "spr_tip_box";
        /// <summary>Create: <c>width = sprite_get_width(spr_tip_box)</c>, <c>height = sprite_get_height(spr_tip_box)</c> (548x250, origin 0,0).</summary>
        public int Width = 548;
        public int Height = 250;
        /// <summary>Create: <c>image_alpha = 0.7</c>, the box's draw_set_alpha (the text is drawn at 1).</summary>
        public float Alpha = 0.7f;
        /// <summary>Create: <c>y = view_hview - height - 30</c>.</summary>
        public int BottomMargin = 30;
        /// <summary>Create: <c>endX = view_wview - width - 30</c>, where it slides in to.</summary>
        public int RightMargin = 30;
        /// <summary>Create: <c>margin = 20</c>, the text's inset from the box's top left.</summary>
        public int Margin = 20;
        /// <summary>Slide in: <c>x -= 1 + (x - endX) * 0.1</c>.</summary>
        public double SlideInMin = 1;
        /// <summary>Slide out: <c>x += 2 + (startX - x) * 0.1</c>.</summary>
        public double SlideOutMin = 2;
        public double SlideFactor = 0.1;
        /// <summary>A slide's first pass: <c>audio_play_sound(sfx_flick, 1, 0)</c>.</summary>
        public string Sound = "sfx_flick";
        /// <summary>Draw GUI: <c>draw_text_outline(x + margin, y + margin, 1, text)</c>.</summary>
        public int Outline = 1;
        /// <summary>obj_map's Create: "Mission Command" and how to pick a mission (GML text: # is a new line, indent() ten spaces).</summary>
        public string MapText = "Mission Command#          Press [A] or [D] to cycle through available missions.#          Press [Any Key] to launch selected mission.#";
        /// <summary>obj_drop_intro's controls list.</summary>
        public string IntroText = "Controls#"
            + "          Mouse 1 - Fire Primary\\Secondary Weapon#"
            + "          CTRL - Primary\\Secondary Toggle#"
            + "          Mouse 2 - Leap (on surface) \\ Brake (in flight)#"
            + "          W, S - Shield yourself (on surface)#"
            + "          A, D - Change trajectory (in flight)#"
            + "          SPACE, W - Boost (in flight)#"
            + "          SHIFT, S - Brake (in flight)#"
            + "          Q - Cycle Primary\\Secondary Ground Weapon#"
            + "          E - Cycle Primary\\Secondary Air Weapon#";

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            if (Alpha < 0f || Alpha > 1f) errors.Add($"{path}.Alpha: must be 0-1 (was {Alpha}).");
            ValidationHelpers.NonNegative(errors, path + ".Margin", Margin);
            ValidationHelpers.Positive(errors, path + ".SlideInMin", (float)SlideInMin);
            ValidationHelpers.Positive(errors, path + ".SlideOutMin", (float)SlideOutMin);
            if (SlideFactor < 0 || SlideFactor >= 1) errors.Add($"{path}.SlideFactor: must be 0 or more and below 1 (was {SlideFactor}).");
            ValidationHelpers.Required(errors, path + ".Sound", Sound);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            if (MapText == null) errors.Add(path + ".MapText: must not be null.");
            if (IntroText == null) errors.Add(path + ".IntroText: must not be null.");
        }
    }

    /// <summary>One of obj_map's four missions: <c>mission[i] = instance_create(0,0, obj_alert); .degree; .scenario_id</c>.</summary>
    [Serializable]
    public class AlertMission
    {
        public float Degree;
        public int ScenarioId = -1;

        public AlertMission() { }

        public AlertMission(float degree, int scenarioId)
        {
            Degree = degree;
            ScenarioId = scenarioId;
        }
    }

    /// <summary>obj_alert's numbers (U.HARE-T27), the mission marker obj_map pins to obj_earthball's rim.</summary>
    [Serializable]
    public class AlertDefinition
    {
        /// <summary>The object's sprite (32x64, origin 16,32, six frames).</summary>
        public string Sprite = "spr_alertmarker";
        public int Frames = 6;
        /// <summary>The object's depth, -10.</summary>
        public int Depth = -10;
        /// <summary>Create: <c>image_speed = 0.2</c>.</summary>
        public double ImageSpeed = 0.2;
        /// <summary>Create: <c>Len = 550</c>, how far from obj_earthball's centre it rides.</summary>
        public double Length = 550;
        /// <summary>Create: <c>ping_rate = 30; alarm[0] = ping_rate</c>.</summary>
        public int PingRateSteps = 30;
        /// <summary>Alarm 0: <c>effect_create_above(ef_ring, pingX, pingY, 0, c_red)</c>.</summary>
        public int PingSize = 0;
        public string PingColor = "#FF0000";
        /// <summary>obj_map's Create: the four missions.</summary>
        public AlertMission[] Missions =
        {
            new AlertMission(16, 1),
            new AlertMission(150, 2),
            new AlertMission(270, 3),
            new AlertMission(310, 4),
        };

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".Frames", Frames);
            ValidationHelpers.NonNegative(errors, path + ".ImageSpeed", (float)ImageSpeed);
            ValidationHelpers.Positive(errors, path + ".Length", (float)Length);
            ValidationHelpers.Positive(errors, path + ".PingRateSteps", PingRateSteps);
            ValidationHelpers.NonNegative(errors, path + ".PingSize", PingSize);
            FloatTextDefinition.CheckColor(errors, path + ".PingColor", PingColor);
            if (Missions == null) errors.Add(path + ".Missions: must not be null (an empty list for none).");
            else for (int i = 0; i < Missions.Length; i++)
                if (Missions[i] == null) errors.Add($"{path}.Missions[{i}]: must not be null.");
        }
    }

    /// <summary>
    /// The popups as data (the content registry's <c>popups</c> section, U.HARE-T27): obj_float_text, obj_tip and
    /// obj_alert. The defaults are the original's.
    /// </summary>
    [Serializable]
    public class PopupTuning
    {
        public FloatTextDefinition FloatText = new FloatTextDefinition();
        public TipDefinition Tip = new TipDefinition();
        public AlertDefinition Alert = new AlertDefinition();

        internal void Validate(List<string> errors, string path)
        {
            if (FloatText == null) errors.Add(path + ".FloatText: must not be null.");
            else FloatText.Validate(errors, path + ".FloatText");
            if (Tip == null) errors.Add(path + ".Tip: must not be null.");
            else Tip.Validate(errors, path + ".Tip");
            if (Alert == null) errors.Add(path + ".Alert: must not be null.");
            else Alert.Validate(errors, path + ".Alert");
        }
    }
}
