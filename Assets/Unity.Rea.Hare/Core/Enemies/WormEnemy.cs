using System;
using System.Collections.Generic;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_worm (gmlHARE, par_enemy): the head of a chain of 6-8 <see cref="WormSegmentBody"/>s (one, then
    /// segment_count = 5-7 more), each following the one ahead of it. The head wanders at 1 px/step: Alarm 0 (step 1,
    /// then every 200-800) jolts its direction by up to 90 degrees and flips its curve, and Alarm 3 bends it 2-4
    /// degrees that way every 20 steps. More than its width (128 px) outside the room it reverses. Its jaw (spr_worm_head,
    /// drawn 55 px out) turns 1 degree a step toward the heading, and every 2-5 seconds (first after 240-600 steps) it
    /// spits 12-24 slivers (obj_bullet with spr_sliver) from 128 px out along the jaw, within 20 degrees of it, at 2-3 px/step.
    ///
    /// It must be cut down from the tail: a part with a live child (the next segment back) shrugs hits off, though
    /// bullets of any owner are still spent on it and rockets still explode. An exposed part darkens (RGB 255, -16 a
    /// bullet, -42 a rocket) and the hit after it reaches 128 or less kills it: an obj_grenade_explosion, worm_gib's
    /// 6-8 gibs and a pickup; the head is worth 100 points and also drops obj_worm_head_gib, a segment 10 and speeds
    /// the head up by (10 - segment_count) * 0.1 px/step. Its rocket-explosion and shield-wall events don't exist and
    /// its push-apart is commented out.
    /// </summary>
    public sealed class WormEnemy : EnemyBody
    {
        public const float StartSpeedPxPerStep = 1f;
        public const float SizePx = 128f;
        public const float JawOffsetPx = 55f;
        public const float SliverOffsetPx = 128f;
        public const float JawTurnDegreesPerStep = 1f;
        public const float AlignToleranceDegrees = 2f;
        public const float CurveEverySteps = 20f;
        public const float SegmentRadiusPx = 4f;
        public const int MinSegmentCount = 5;
        public const int MaxSegmentCount = 7;
        public const int FullRgb = 255;
        public const int DeathRgb = 128;
        public const int BulletDamage = 16;
        public const int RocketDamage = 42;

        readonly List<WormSegmentBody> segments = new List<WormSegmentBody>();
        float headingAlarm;
        float spinAlarm;
        float sliverAlarm;
        float curveAlarm;
        bool curveLeft;

        public WormEnemy() => Kind = EnemyKind.Worm;

        /// <summary>GML segment_count: the extra segments after the first (5-7).</summary>
        public int SegmentCount { get; private set; }
        /// <summary>All segments, head-most first (dead ones stay in the list).</summary>
        public IReadOnlyList<WormSegmentBody> Segments => segments;
        /// <summary>GML child: the first segment.</summary>
        public WormSegmentBody Child { get; private set; }
        /// <summary>No live child: hits now count.</summary>
        public bool IsExposed => Child == null || Child.IsDead;
        public int Rgb { get; private set; }
        /// <summary>GML heading_angle: where the jaw points.</summary>
        public float HeadingAngle { get; private set; }
        /// <summary>GML headX/headY: the jaw's position.</summary>
        public Vec2 JawPosition { get; private set; }
        public bool CurvesLeft => curveLeft;
        /// <summary>GML rot_speed: the body sprite's spin.</summary>
        public float Spin { get; private set; }
        public float BackAngle { get; private set; }

        /// <summary>GML Create, which also builds the chain. Optional values pin the random ones.</summary>
        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = StartSpeedPxPerStep;
            Direction = o.Direction ?? 0f; // obj_worm never sets direction: GML's default 0
            Rgb = FullRgb;
            BackAngle = Rng.IRandom(0, 359);
            HeadingAngle = 0f;
            JawPosition = Position;
            Spin = Rng.IRandom(-3, 3);
            curveLeft = true;
            headingAlarm = 1f;
            spinAlarm = Rng.IRandom(60, 240);
            sliverAlarm = Rng.IRandom(240, 600);
            curveAlarm = CurveEverySteps;
            SegmentCount = Math.Max(0, o.SegmentCount ?? Rng.IRandom(MinSegmentCount, MaxSegmentCount));

            segments.Clear();
            EnemyBody ahead = this;
            WormSegmentBody previous = null;
            for (int i = 0; i <= SegmentCount; i++)
            {
                var segment = new WormSegmentBody(this, ahead, SegmentRadiusPx);
                Spawn(segment);
                if (previous != null) previous.Child = segment;
                else Child = segment;
                segments.Add(segment);
                ahead = previous = segment;
            }
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref headingAlarm, steps))
            {
                Direction += Rng.IRandom(-90, 90);
                curveLeft = !curveLeft;
                headingAlarm = Rng.IRandom(200, 800);
            }
            if (Ring(ref spinAlarm, steps))
            {
                Spin = Rng.IRandom(-2, 2);
                spinAlarm = Rng.IRandom(60, 240);
            }
            if (Ring(ref sliverAlarm, steps))
            {
                Vec2 origin = Position + GmlMath.Dir(HeadingAngle) * GameClock.Px(SliverOffsetPx);
                int count = Rng.IRandom(12, 24);
                for (int i = 0; i < count; i++) Fire(origin, HeadingAngle + Rng.IRandom(-20, 20), Rng.IRandom(2, 3));
                Emit(new WormSpat { Slivers = count });
                sliverAlarm = Rng.IRandom(2 * 120, 5 * 120);
            }
            if (Ring(ref curveAlarm, steps))
            {
                Direction += curveLeft ? Rng.IRandom(2, 4) : -Rng.IRandom(2, 4);
                curveAlarm = CurveEverySteps;
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            BackAngle = GmlMath.Wrap(BackAngle - Spin * steps);
            if (OutsideRoom(SizePx)) Direction -= 180f;

            if (!GmlMath.ANearB(HeadingAngle, Direction, AlignToleranceDegrees))
            {
                bool clockwise = GmlMath.CheckRotateClockwise(Direction, HeadingAngle);
                HeadingAngle = GmlMath.Wrap(HeadingAngle + (clockwise ? -JawTurnDegreesPerStep : JawTurnDegreesPerStep) * steps);
            }
            JawPosition = Position + GmlMath.Dir(HeadingAngle) * GameClock.Px(JawOffsetPx);
        }

        /// <summary>A tail segment died: head.speed += (10 - head.segment_count) * 0.1.</summary>
        internal void OnSegmentKilled() => SpeedPxPerStep += (10 - SegmentCount) * 0.1f;

        protected override bool RocketHit(Vec2 rocketAt)
        {
            if (IsExposed)
            {
                if (Rgb > DeathRgb) Rgb -= RocketDamage;
                else
                {
                    TinyExplosionAt(rocketAt);
                    Gib(EnemyDeathCause.Rocket, true);
                }
            }
            return true;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            // Every bullet puffs where it is spent; the kill branch adds a second puff.
            TinyExplosionAt(shot.Position);
            if (IsExposed)
            {
                if (Rgb > DeathRgb) Rgb -= BulletDamage;
                else
                {
                    TinyExplosionAt(shot.Position);
                    Gib(EnemyDeathCause.Bullet, shot.FromPlayer);
                }
            }
            return true;
        }

        void Gib(EnemyDeathCause cause, bool awardsScore)
        {
            Vec2 at = Position;
            Vec2 jaw = JawPosition;
            Emit(new GrenadeExplosionRaised { At = at });
            DeathBurst();
            Die(cause, EnemyTable.Get(Kind).Points, awardsScore, true);
            Gibs.Scatter(Rng, DebrisKind.WormGib, at, 6, 8, Debris);
            Debris(Gibs.Gib(Rng, DebrisKind.WormHeadGib, jaw));
        }
    }

    /// <summary>
    /// The rules of obj_worm_segment (gmlHARE, par_enemy): one link of a <see cref="WormEnemy"/>. While both its parent
    /// (the part ahead) and the head live, it moves toward the parent at the head's speed whenever the parent's centre
    /// is more than radius (4) px outside its bounding box, so links trail about a body width apart; otherwise it
    /// keeps its last velocity. Its sprite spins like the head's. Hits work as on the head, for 10 points.
    /// </summary>
    public sealed class WormSegmentBody : EnemyBody
    {
        float spinAlarm;

        internal WormSegmentBody(WormEnemy head, EnemyBody parent, float radiusPx)
        {
            Kind = EnemyKind.Worm;
            Head = head;
            Parent = parent;
            RadiusPx = radiusPx;
        }

        public override BodyPart Part => BodyPart.Segment;
        public override bool PushesAway => false;

        public WormEnemy Head { get; }
        /// <summary>GML parent: the part this one follows.</summary>
        public EnemyBody Parent { get; }
        /// <summary>GML child: the next segment back, or null for the tail.</summary>
        public WormSegmentBody Child { get; internal set; }
        public bool IsExposed => Child == null || Child.IsDead;
        public float RadiusPx { get; }
        public int Rgb { get; private set; }
        public float Spin { get; private set; }
        public float BackAngle { get; private set; }

        internal override void Create(EnemyOptions o)
        {
            SpeedPxPerStep = 0f;
            Rgb = WormEnemy.FullRgb;
            BackAngle = Rng.IRandom(0, 359);
            Spin = Rng.IRandom(-3, 3);
            spinAlarm = Rng.IRandom(60, 240);
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref spinAlarm, steps))
            {
                Spin = Rng.IRandom(-2, 2);
                spinAlarm = Rng.IRandom(60, 240);
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            BackAngle = GmlMath.Wrap(BackAngle - Spin * steps);
            if (Parent != null && !Parent.IsDead && Head != null && !Head.IsDead && DistanceToPointPx(Parent.Position) > RadiusPx)
                MoveTowardsPoint(Parent.Position, Head.SpeedPxPerStep);
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            if (IsExposed)
            {
                if (Rgb > WormEnemy.DeathRgb) Rgb -= WormEnemy.RocketDamage;
                else
                {
                    TinyExplosionAt(rocketAt);
                    Gib(EnemyDeathCause.Rocket, true);
                }
            }
            return true;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            // Every bullet puffs where it is spent; the kill branch adds a second puff.
            TinyExplosionAt(shot.Position);
            if (IsExposed)
            {
                if (Rgb > WormEnemy.DeathRgb) Rgb -= WormEnemy.BulletDamage;
                else
                {
                    TinyExplosionAt(shot.Position);
                    Gib(EnemyDeathCause.Bullet, shot.FromPlayer);
                }
            }
            return true;
        }

        void Gib(EnemyDeathCause cause, bool awardsScore)
        {
            Vec2 at = Position;
            Emit(new GrenadeExplosionRaised { At = at });
            DeathBurst();
            Die(cause, EnemyTable.Get(Kind).PartPoints, awardsScore, true);
            Gibs.Scatter(Rng, DebrisKind.WormGib, at, 6, 8, Debris);
            if (Head != null && !Head.IsDead) Head.OnSegmentKilled();
        }
    }
}
