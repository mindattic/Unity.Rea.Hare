using System;

namespace Unity.Rea.Hare.Map
{
    /// <summary>
    /// obj_scanner (spr_scanner_beam, depth 0; U.HARE-T63): the beam under the USS Warren, placed at x 1045 with its
    /// origin (210, 0) at its top-right corner. Its Create: <c>y = room_height - sprite_get_height(spr_earthball)/2 -
    /// sprite_get_height(spr_scanner_beam) + 50</c> (388), <c>image_alpha = 0.2</c>, <c>sweepLeft = true</c>,
    /// <c>sweepAmount = 0.1</c>, <c>sweepSpeed = 0.004</c>, <c>sweepY = 0</c>. Its Step, every step whatever the map does:
    /// <code>
    /// if(sweepLeft) { if(sweepY &lt; sweepAmount) { sweepY += sweepSpeed } else {sweepLeft = false; } }
    /// else { if(sweepY &gt; -sweepAmount) { sweepY -= sweepSpeed } else {sweepLeft = true; } }
    /// image_angle += sweepY;
    /// image_alpha = 0.01 * irandom_range(16, 24);
    /// </code>
    /// so the beam sways about its corner between 0 and about 2.6 degrees (102 steps a sway) and flickers between 0.16
    /// and 0.24. It has no Draw event: draw_self. The comparisons are GML's, equal within its 0.00001 epsilon.
    /// </summary>
    public sealed class MapScanner
    {
        /// <summary>GML compares reals as equal within this (math_set_epsilon's default).</summary>
        public const double GmlEpsilon = 0.00001d;

        readonly MapTuning tuning;
        readonly IRandomSource random;

        internal MapScanner(MapTuning tuning, int roomHeight, IRandomSource random)
        {
            this.tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            X = tuning.ScannerX;
            Y = tuning.ScannerY(roomHeight);
            ImageAngle = 0d;
            Alpha = tuning.ScannerAlpha;
            SweepLeft = true;
            SweepY = 0d;
        }

        /// <summary>Its place (the beam's top-right corner, the sprite's origin).</summary>
        public double X { get; }
        public double Y { get; }

        /// <summary>image_angle, degrees anticlockwise about the origin (never wrapped).</summary>
        public double ImageAngle { get; private set; }

        /// <summary>image_alpha.</summary>
        public double Alpha { get; private set; }

        /// <summary>sweepLeft: the sway's speed is rising.</summary>
        public bool SweepLeft { get; private set; }

        /// <summary>sweepY: degrees added to image_angle this step.</summary>
        public double SweepY { get; private set; }

        /// <summary>image_alpha for drawing, held to 0..1 as draw_self does.</summary>
        public float DrawAlpha => (float)Math.Max(0d, Math.Min(1d, Alpha));

        /// <summary>obj_scanner's Step.</summary>
        internal void Step()
        {
            double amount = tuning.ScannerSweepAmount;
            double speed = tuning.ScannerSweepSpeed;
            if (SweepLeft)
            {
                if (GmlLess(SweepY, amount)) SweepY += speed;
                else SweepLeft = false;
            }
            else
            {
                if (GmlLess(-amount, SweepY)) SweepY -= speed;
                else SweepLeft = true;
            }
            ImageAngle += SweepY;
            Alpha = tuning.ScannerAlphaUnit * random.IRandom(tuning.ScannerAlphaMin, tuning.ScannerAlphaMax);
        }

        /// <summary>GML's <c>a &lt; b</c> on reals: false when they are equal within <see cref="GmlEpsilon"/>.</summary>
        public static bool GmlLess(double a, double b) => a < b && Math.Abs(a - b) >= GmlEpsilon;
    }
}
