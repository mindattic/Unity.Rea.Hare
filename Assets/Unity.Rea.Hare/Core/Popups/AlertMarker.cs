using System;

namespace Unity.Rea.Hare.Popups
{
    /// <summary>
    /// obj_alert (U.HARE-T27), engine-free, in room pixels (y down): a mission marker obj_map's Create pins to
    /// obj_earthball's rim (<c>degree</c>, <c>scenario_id</c>). Create: <c>degree = 0; image_speed = 0.2; Len = 550;
    /// ping_rate = 30; alarm[0] = ping_rate; scenario_id = -1</c>. Alarm 0 (before the Step, as GameMaker runs alarms):
    /// while <c>obj_map.mode == "idle"</c>, an <c>ef_ring</c> of size 0 in c_red at <c>obj_earthball + lengthdir(Len,
    /// image_angle)</c>; re-armed every time. Step: <c>image_angle = degree + obj_earthball.image_angle</c>, and it sits
    /// at <c>obj_earthball + lengthdir(Len, image_angle)</c>. Draw: draw_self (spr_alertmarker, animated at image_speed,
    /// turned by image_angle) at depth -10. obj_destination finds it by collision to pick the scenario (U.HARE-T63).
    /// </summary>
    public sealed class AlertMarker
    {
        int alarm;

        public AlertMarker(AlertDefinition definition, float degree, int scenarioId)
        {
            Definition = definition ?? new AlertDefinition();
            Degree = degree;
            ScenarioId = scenarioId;
            alarm = Definition.PingRateSteps;
        }

        public AlertDefinition Definition { get; }
        public float Degree { get; set; }
        public int ScenarioId { get; set; }
        public double X { get; private set; }
        public double Y { get; private set; }
        public double ImageAngle { get; private set; }
        /// <summary>GML image_index, running on by image_speed every step and wrapping at the sprite's frames.</summary>
        public double ImageIndex { get; private set; }
        /// <summary>Steps left on alarm[0].</summary>
        public int Alarm => alarm;

        /// <summary>The frame draw_self shows.</summary>
        public int Frame => Definition.Frames <= 0 ? 0 : (int)Math.Floor(ImageIndex) % Definition.Frames;

        /// <summary>
        /// One step against obj_earthball at (<paramref name="earthX"/>, <paramref name="earthY"/>) turned
        /// <paramref name="earthAngle"/> degrees, with obj_map idle or not. True when Alarm 0 pinged, at
        /// (<paramref name="pingX"/>, <paramref name="pingY"/>).
        /// </summary>
        public bool Step(double earthX, double earthY, double earthAngle, bool mapIdle, out double pingX, out double pingY)
        {
            bool pinged = false;
            pingX = pingY = 0;
            if (alarm > 0 && --alarm == 0)
            {
                if (mapIdle)
                {
                    pingX = earthX + LengthDirX(Definition.Length, ImageAngle);
                    pingY = earthY + LengthDirY(Definition.Length, ImageAngle);
                    pinged = true;
                }
                alarm = Definition.PingRateSteps;
            }
            ImageAngle = Degree + earthAngle;
            X = earthX + LengthDirX(Definition.Length, ImageAngle);
            Y = earthY + LengthDirY(Definition.Length, ImageAngle);
            if (Definition.Frames > 0)
            {
                ImageIndex += Definition.ImageSpeed;
                if (ImageIndex >= Definition.Frames) ImageIndex -= Definition.Frames;
            }
            return pinged;
        }

        /// <summary>GML lengthdir_x(len, dir).</summary>
        public static double LengthDirX(double length, double degrees) => length * Math.Cos(degrees * Math.PI / 180.0);

        /// <summary>GML lengthdir_y(len, dir): y grows downward, so a positive angle goes up.</summary>
        public static double LengthDirY(double length, double degrees) => -length * Math.Sin(degrees * Math.PI / 180.0);
    }
}
