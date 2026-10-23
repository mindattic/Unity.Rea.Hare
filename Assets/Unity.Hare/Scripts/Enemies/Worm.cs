using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_worm (gmlHARE, par_enemy): the head of a chain of 6-8 <see cref="WormSegment"/>s
    /// (one, then segment_count = 5-7 more), each following the one ahead of it. The head wanders
    /// at 1 px/step: Alarm 0 (step 1, then every 200-800) jolts its direction by up to 90 degrees
    /// and flips its curve, and Alarm 3 bends it 2-4 degrees that way every 20 steps. More than its
    /// width (128 px) outside the room it reverses. Its jaw (spr_worm_head, drawn 55 px out) turns
    /// 1 degree a step toward the heading, and every 2-5 seconds (first after 240-600 steps) it
    /// spits 12-24 slivers (obj_bullet with spr_sliver) from 128 px out along the jaw, within 20
    /// degrees of it, at 2-3 px/step.
    ///
    /// It must be cut down from the tail: a part with a live child (the next segment back) shrugs
    /// hits off, though bullets of any owner are still spent on it and rockets still explode. An
    /// exposed part darkens (RGB 255, -16 a bullet, -42 a rocket) and the hit after it reaches 128
    /// or less kills it: an obj_grenade_explosion, worm_gib's 6-8 gibs and a pickup; the head is
    /// worth 100 points and also drops obj_worm_head_gib, a segment 10 and speeds the head up by
    /// (10 - segment_count) * 0.1 px/step. Its rocket-explosion and shield-wall events don't exist
    /// and its push-apart is commented out.
    /// </summary>
    public class Worm : SetPieceEnemy
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
        public const int Points = 100;
        /// <summary>The size of the ef_smokeup and ef_firework its kill branches create.</summary>
        public const int DeathEffectSize = DeathEffects.Large;

        readonly List<WormSegment> segments = new List<WormSegment>();
        float headingAlarm;
        float spinAlarm;
        float sliverAlarm;
        float curveAlarm;
        bool curveLeft;

        /// <summary>GML segment_count: the extra segments after the first (5-7).</summary>
        public int SegmentCount { get; private set; }
        /// <summary>All segments, head-most first (dead ones stay in the list until queried as dead).</summary>
        public IReadOnlyList<WormSegment> Segments => segments;
        /// <summary>GML child: the first segment.</summary>
        public WormSegment Child { get; private set; }
        /// <summary>No live child: hits now count.</summary>
        public bool IsExposed => Child == null || Child.IsDead;
        public int Rgb { get; private set; }
        /// <summary>GML heading_angle: where the jaw points.</summary>
        public float HeadingAngle { get; private set; }
        /// <summary>GML headX/headY: the jaw's position.</summary>
        public Vector2 JawPosition { get; private set; }
        public bool CurvesLeft => curveLeft;
        /// <summary>GML rot_speed: the body sprite's spin.</summary>
        public float Spin { get; private set; }
        public float BackAngle { get; private set; }

        /// <summary>Alarm 2's volley, with how many slivers it spat.</summary>
        public event Action<Worm, int> Spat;

        protected override Vector2 BBoxHalfPx => new Vector2(56.5f, 56.5f);

        /// <summary>GML Create, which also builds the chain. Optional values pin the random ones.</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null,
            int? segmentCount = null, float? direction = null)
        {
            Setup(position, room, player);
            UseCircle(56.5f);
            ImageAlpha = 1f;
            SpeedPxPerStep = StartSpeedPxPerStep;
            Direction = direction ?? 0f; // obj_worm never sets direction: GML's default 0
            Rgb = FullRgb;
            BackAngle = IRandom(0, 359);
            HeadingAngle = 0f;
            JawPosition = Position;
            Spin = IRandom(-3, 3);
            curveLeft = true;
            headingAlarm = 1f;
            spinAlarm = IRandom(60, 240);
            sliverAlarm = IRandom(240, 600);
            curveAlarm = CurveEverySteps;
            SegmentCount = Mathf.Max(0, segmentCount ?? IRandom(MinSegmentCount, MaxSegmentCount));

            segments.Clear();
            SetPieceEnemy ahead = this;
            WormSegment previous = null;
            for (int i = 0; i <= SegmentCount; i++)
            {
                var segment = new GameObject("WormSegment").AddComponent<WormSegment>();
                segment.Init(this, ahead, SegmentRadiusPx);
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
                Direction += IRandom(-90, 90);
                curveLeft = !curveLeft;
                headingAlarm = IRandom(200, 800);
            }
            if (Ring(ref spinAlarm, steps))
            {
                Spin = IRandom(-2, 2);
                spinAlarm = IRandom(60, 240);
            }
            if (Ring(ref sliverAlarm, steps))
            {
                Vector2 origin = Position + Gml.Dir(HeadingAngle) * Gml.Px(SliverOffsetPx);
                int count = IRandom(12, 24);
                for (int i = 0; i < count; i++) Fire(origin, HeadingAngle + IRandom(-20, 20), IRandom(2, 3));
                Spat?.Invoke(this, count);
                sliverAlarm = IRandom(2 * 120, 5 * 120);
            }
            if (Ring(ref curveAlarm, steps))
            {
                Direction += curveLeft ? IRandom(2, 4) : -IRandom(2, 4);
                curveAlarm = CurveEverySteps;
            }
        }

        protected override void StepEvent(float steps)
        {
            ImageAngle += Spin * steps;
            BackAngle = Mathf.Repeat(BackAngle - Spin * steps, 360f);
            if (OutsideRoom(SizePx)) Direction -= 180f;

            if (!Dagger.ANearB(HeadingAngle, Direction, AlignToleranceDegrees))
            {
                bool clockwise = Dagger.CheckRotateClockwise(Direction, HeadingAngle);
                HeadingAngle = Mathf.Repeat(HeadingAngle + (clockwise ? -JawTurnDegreesPerStep : JawTurnDegreesPerStep) * steps, 360f);
            }
            JawPosition = Position + Gml.Dir(HeadingAngle) * Gml.Px(JawOffsetPx);
        }

        /// <summary>A tail segment died: head.speed += (10 - head.segment_count) * 0.1.</summary>
        internal void OnSegmentKilled() => SpeedPxPerStep += (10 - SegmentCount) * 0.1f;

        protected override bool RocketHit(Rocket rocket)
        {
            if (IsExposed)
            {
                if (Rgb > DeathRgb) Rgb -= RocketDamage;
                else
                {
                    TinyExplosionAt(rocket);
                    Gib(EnemyDeathCause.Rocket, true);
                }
            }
            return true;
        }

        protected override bool BulletHit(Bullet bullet)
        {
            // Every bullet puffs where it is spent; the kill branch adds a second puff.
            TinyExplosionAt(bullet);
            if (IsExposed)
            {
                if (Rgb > DeathRgb) Rgb -= BulletDamage;
                else
                {
                    TinyExplosionAt(bullet);
                    Gib(EnemyDeathCause.Bullet, IsPlayerRound(bullet));
                }
            }
            return true;
        }

        void Gib(EnemyDeathCause cause, bool awardsScore)
        {
            Vector2 at = Position;
            Vector2 jaw = JawPosition;
            GrenadeExplosion.Spawn(at);
            DeathBurst(DeathEffectSize);
            Die(cause, Points, awardsScore, true);
            FadingDebris.WormGibs(at);
            FadingDebris.Gib(DebrisKind.WormHeadGib, jaw);
        }
    }
}
