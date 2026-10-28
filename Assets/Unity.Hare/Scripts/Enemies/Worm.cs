using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>The view on <see cref="WormEnemy"/> (obj_worm): the head of a chain of <see cref="WormSegment"/>s.</summary>
    public class Worm : SetPieceEnemy
    {
        public const float SizePx = WormEnemy.SizePx;
        public const float JawOffsetPx = WormEnemy.JawOffsetPx;
        public const float SliverOffsetPx = WormEnemy.SliverOffsetPx;
        public const int MinSegmentCount = WormEnemy.MinSegmentCount;
        public const int MaxSegmentCount = WormEnemy.MaxSegmentCount;
        public const int FullRgb = WormEnemy.FullRgb;
        public const int DeathRgb = WormEnemy.DeathRgb;
        public const int BulletDamage = WormEnemy.BulletDamage;
        public const int RocketDamage = WormEnemy.RocketDamage;
        public const int Points = 100;

        WormEnemy Rules => (WormEnemy)Core;

        protected override string BodySprite => "spr_worm_segment";

        /// <summary>GML segment_count: the extra segments after the first (5-7).</summary>
        public int SegmentCount => Rules.SegmentCount;

        /// <summary>All segments, head-most first (a dead one has no view left, so its entry is null).</summary>
        public IReadOnlyList<WormSegment> Segments
        {
            get
            {
                var list = new List<WormSegment>();
                foreach (var s in Rules.Segments) list.Add(EnemyDirector.ViewOf<WormSegment>(s.Id));
                return list;
            }
        }

        /// <summary>GML child: the first segment.</summary>
        public WormSegment Child => Rules.Child == null ? null : EnemyDirector.ViewOf<WormSegment>(Rules.Child.Id);
        /// <summary>No live child: hits now count.</summary>
        public bool IsExposed => Rules.IsExposed;
        public int Rgb => Rules.Rgb;
        /// <summary>GML heading_angle: where the jaw points.</summary>
        public float HeadingAngle => Rules.HeadingAngle;
        /// <summary>GML headX/headY: the jaw's position.</summary>
        public Vector2 JawPosition => Rules.JawPosition.ToVector2();
        public bool CurvesLeft => Rules.CurvesLeft;
        /// <summary>GML rot_speed: the body sprite's spin.</summary>
        public float Spin => Rules.Spin;
        public float BackAngle => Rules.BackAngle;

        /// <summary>Alarm 2's volley, with how many slivers it spat.</summary>
        public event Action<Worm, int> Spat;

        /// <summary>GML Create, which also builds the chain. Optional values pin the random ones.</summary>
        public void Init(Vector2 position, Rect? room = null, PlayerShipController player = null,
            int? segmentCount = null, float? direction = null) =>
            Begin(EnemyKind.Worm, position, room, player, new EnemyOptions { SegmentCount = segmentCount, Direction = direction });

        protected override void Configure()
        {
            UseCircle(56.5f);
            AddDamageSource();
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            if (e is WormSpat spat) Spat?.Invoke(this, spat.Slivers);
            base.OnEnemyEvent(e);
        }
    }
}
