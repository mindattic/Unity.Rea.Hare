using System;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="DaggerEnemy"/> (obj_dagger): its rotate, fly and stop flight, its collisions and its
    /// scoring live in Core; this shows the blade and plays its launch and smoke. The shield wall reaches it as an
    /// <see cref="IShieldable"/>.
    /// </summary>
    public class Dagger : SetPieceEnemy, IShieldable
    {
        public const float SizePx = DaggerEnemy.SizePx;
        public const float MaxSpeedPxPerStep = DaggerEnemy.MaxSpeedPxPerStep;
        public const int Points = 10;
        /// <summary>spr_dagger's bounding box (18..94 x 26..69 of a 96x96 sprite centred at 48,48).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(76f, 43f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(8f, 0f);

        DaggerEnemy Rules => (DaggerEnemy)Core;

        protected override string BodySprite => Rules.Variant == 2 ? "spr_dagger2" : "spr_dagger";

        public DaggerMode Mode => Rules.Mode;
        /// <summary>1 = spr_dagger, 2 = spr_dagger2.</summary>
        public int Variant => Rules.Variant;
        /// <summary>Steps left in Fly before Alarm 1 switches to Stop, or negative when unset.</summary>
        public float FlyStepsLeft => Rules.FlyStepsLeft;

        /// <summary>Lined up and launched (GML effect_create_above(ef_ring)).</summary>
        public event Action<Dagger> Launched;
        /// <summary>Alarm 2 while flying: effect_create_below(ef_smokeup) just behind it.</summary>
        public event Action<Dagger, Vector2> SmokePuff;

        /// <summary>
        /// GML Create at <paramref name="position"/>. <paramref name="direction"/> and <paramref name="imageAngle"/>
        /// override the random irandom_range(0, 359) values (tests pin them); <paramref name="room"/> defaults to room_gamescreen.
        /// </summary>
        public void Init(Vector2 position, Rect? room = null, float? direction = null, float? imageAngle = null) =>
            Begin(EnemyKind.Dagger, position, room, null, new EnemyOptions { Direction = direction, ImageAngle = imageAngle });

        protected override void Configure()
        {
            var hitbox = UseBox(HitboxSizePx, HitboxOffsetPx);
            hitbox.isTrigger = true;
            AddDamageSource();
        }

        public override void OnEnemyEvent(EnemyEvent e)
        {
            if (e is DaggerLaunched) Launched?.Invoke(this);
            else if (e is SmokePuffRaised puff) SmokePuff?.Invoke(this, puff.At.ToVector2());
            base.OnEnemyEvent(e);
        }

        /// <summary>GML collision with obj_shieldwall: dies with a tiny explosion.</summary>
        public void OnShieldHit(ShieldWall wall, Vector2 normal)
        {
            if (!IsDead) Send(new StrikeShield(Core.Id));
        }
    }
}
