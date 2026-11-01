using System;
using System.Collections.Generic;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// The rules of obj_scrapmental (gmlHARE, par_enemy): Scrap Mental, level_08's boss and its o_defeat target. A spinning
    /// eye (spr_schrapnel_eye in spr_shrapnel_frame) that creeps toward the ship at 1 px/step while farther than 100 px,
    /// floats up and down (its undulate radius swings between -20 and 160 px, 4 px every 8 steps), fires 12-16 slivers
    /// (obj_bullet with spr_sliver, 2-5 px/step in random directions) every 4-8 s (first after 3-6 s), and makes a
    /// sawblade or a dagger every 3-6 s. Its Create builds 36 <see cref="ShrapnelPieceBody"/> hull pieces orbiting on the
    /// undulate radius and 16 <see cref="ShrapnelHarpoonBody"/> grappling arms (obj_shrapnel_back, the brain plate behind
    /// it, is presentation only).
    ///
    /// Hits darken it (image_blend RGB 255, -1 a bullet of the ship's, -6 a rocket, each shoving it half the shot's step
    /// and re-rolling its spin); the hit after RGB reaches 128 defeats it: 500 points (always for a rocket, only for the
    /// ship's bullet), every hull piece flies off, 24 pickups within 128 px, an obj_grenade_explosion, the large death
    /// burst and spawn_death_particles. It then stays, unseen, for the original's 10 s (alarm[4] = death_delay) before it
    /// is gone, and that is what completes level_08. Its own slivers pass through it.
    ///
    /// Fixed (docs/BIBLE.md D28-D32): the defeated boss is inert (in the original it kept chasing, hurting the ship and
    /// could be "killed" again by a rocket for another 500 and 24 pickups); its pieces fly out along their own angles.
    /// </summary>
    public sealed class ScrapmentalEnemy : EnemyBody
    {
        public const float EngageRangePx = 100f;
        public const float ChaseSpeedPxPerStep = 1f;
        public const int FullRgb = 255;
        public const int DefeatRgb = 128;
        public const int BulletDamage = 1;
        public const int RocketDamage = 6;
        public const int PieceCount = 36;
        public const int HullAssets = 8;
        public const int HarpoonCount = 16;
        public const float HarpoonAngleStep = 20f;
        public const float UndulateAmountPx = 160f;
        public const float UndulateFloorPx = -20f;
        public const float UndulateSpeedPx = 4f;
        public const float UndulateEverySteps = 8f;
        /// <summary>death_delay = room_speed * 10.</summary>
        public const float DeathDelaySteps = 10f * GameClock.StepsPerSecond;
        public const int PickupCount = 24;
        public const int PickupScatterPx = 128;
        public const int MinSlivers = 12;
        public const int MaxSlivers = 16;
        public const int MinSliverSpeed = 2;
        public const int MaxSliverSpeed = 5;

        const int RoomSpeed = (int)GameClock.StepsPerSecond;

        readonly List<ShrapnelPieceBody> pieces = new List<ShrapnelPieceBody>();
        readonly List<ShrapnelHarpoonBody> harpoons = new List<ShrapnelHarpoonBody>();
        float spawnAlarm;
        float fireAlarm;
        float spinAlarm;
        float undulateAlarm;
        float deathAlarm = Off;
        bool undulateUp;

        public ScrapmentalEnemy() => Kind = EnemyKind.Scrapmental;

        /// <summary>GML alive: false once defeated (it is then not drawn, and in the rebuild inert).</summary>
        public bool Alive { get; private set; }
        /// <summary>GML RGB: image_blend's grey level.</summary>
        public int Rgb { get; private set; }
        /// <summary>GML spin, degrees/step.</summary>
        public float Spin { get; private set; }
        /// <summary>GML undulateY: the hull's orbit radius, px.</summary>
        public float UndulateY { get; private set; }
        public IReadOnlyList<ShrapnelPieceBody> Pieces => pieces;
        public IReadOnlyList<ShrapnelHarpoonBody> Harpoons => harpoons;
        /// <summary>Steps left before the defeated boss is gone (alarm[4]), or negative while it fights.</summary>
        public float DeathStepsLeft => deathAlarm;

        /// <summary>The defeated boss is no longer a par_enemy to anything: beams, rockets, the ship's touch (docs/BIBLE.md D30).</summary>
        public override bool IsParEnemy => Alive;
        public override bool PushesAway => false;

        EnemyDeathCause defeatCause;
        bool defeatedByPlayer;

        /// <summary>GML irandom_range(a, b) with a 0 roll made 1 (<c>if(spin == 0) spin = 1</c>).</summary>
        internal static float NonZeroSpin(IRandomSource rng, int min, int max)
        {
            int spin = rng.IRandom(min, max);
            return spin == 0 ? 1f : spin;
        }

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Rgb = FullRgb;
            Spin = o.Spin ?? NonZeroSpin(Rng, -4, 4);

            pieces.Clear();
            int asset = 1;
            for (int i = 0; i < PieceCount; i++)
            {
                var piece = new ShrapnelPieceBody(this, asset);
                Spawn(piece);
                piece.SetAngle(Rng.IRandom(0, 359));
                pieces.Add(piece);
                asset = asset < HullAssets ? asset + 1 : 1;
            }

            harpoons.Clear();
            float angle = 0f;
            for (int i = 0; i < HarpoonCount; i++)
            {
                var harpoon = new ShrapnelHarpoonBody(this);
                Spawn(harpoon);
                harpoon.SetAngle(angle);
                harpoons.Add(harpoon);
                angle += HarpoonAngleStep;
            }

            spawnAlarm = Rng.IRandom(RoomSpeed * 3, RoomSpeed * 6);
            fireAlarm = Rng.IRandom(RoomSpeed * 3, RoomSpeed * 6);
            spinAlarm = Rng.IRandom(120, 320);
            undulateUp = true;
            UndulateY = 0f;
            undulateAlarm = UndulateEverySteps;
            Alive = true;
        }

        protected override void Alarms(float steps)
        {
            bool acting = !World.Paused && Alive;
            if (Ring(ref spawnAlarm, steps))
            {
                if (acting)
                {
                    if (Rng.IRandom(1, 2) == 1) Spawn(new SawbladeEnemy());
                    else Spawn(new DaggerEnemy());
                }
                spawnAlarm = Rng.IRandom(RoomSpeed * 3, RoomSpeed * 6);
            }
            if (Ring(ref fireAlarm, steps))
            {
                if (acting)
                {
                    int count = Rng.IRandom(MinSlivers, MaxSlivers);
                    for (int i = 0; i < count; i++)
                    {
                        float direction = Rng.IRandom(0, 359);
                        Fire(Position, direction, Rng.IRandom(MinSliverSpeed, MaxSliverSpeed));
                    }
                    Emit(new ScrapmentalVolley { Slivers = count });
                }
                fireAlarm = Rng.IRandom(RoomSpeed * 4, RoomSpeed * 8);
            }
            if (Ring(ref spinAlarm, steps))
            {
                if (acting) Spin = NonZeroSpin(Rng, -3, 3);
                spinAlarm = Rng.IRandom(120, 600);
            }
            if (Ring(ref undulateAlarm, steps))
            {
                if (acting) Undulate();
                undulateAlarm = UndulateEverySteps;
            }
            if (Ring(ref deathAlarm, steps))
                Die(defeatCause, 0, defeatedByPlayer, false); // alarm[4]: instance_destroy(), which completes o_defeat
        }

        /// <summary>Alarm 3: gently float up and down.</summary>
        void Undulate()
        {
            if (undulateUp)
            {
                if (UndulateY < UndulateAmountPx) UndulateY += UndulateSpeedPx;
                else undulateUp = false;
            }
            else
            {
                if (UndulateY > UndulateFloorPx) UndulateY -= UndulateSpeedPx;
                else undulateUp = true;
            }
        }

        protected override void StepEvent(float steps)
        {
            if (World.Paused || !Alive)
            {
                SpeedPxPerStep = 0f;
                return;
            }
            ImageAngle += Spin * steps;
            FadeIn(steps);
            var target = Target;
            if (target != null && DistanceToPlayerPx() > EngageRangePx) MoveTowardsPoint(target.Position, ChaseSpeedPxPerStep);
            else SpeedPxPerStep = 0f;
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            // other.owner != ID: its own slivers pass through; another enemy's round does not (U.HARE-T91).
            if (!Alive || IgnoresRound(shot)) return false;
            if (Rgb > DefeatRgb)
            {
                Teleport(Position + GmlMath.Dir(shot.Direction) * (shot.SpeedUnitsPerSecond / GameClock.StepsPerSecond / 2f));
                Spin = NonZeroSpin(Rng, -3, 3);
                Rgb -= BulletDamage;
                TinyExplosionAt(shot.Position);
            }
            else Defeat(EnemyDeathCause.Bullet, shot.FromPlayer);
            return true;
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            if (!Alive) return false; // inert once defeated (docs/BIBLE.md D30)
            if (Rgb > DefeatRgb)
            {
                Vec2 push = RocketVelocityPxPerStep * 0.5f;
                Teleport(Position + new Vec2(GameClock.Px(push.X), GameClock.Px(push.Y)));
                Spin = NonZeroSpin(Rng, -3, 3);
                Rgb -= RocketDamage;
                TinyExplosionAt(rocketAt);
            }
            else Defeat(EnemyDeathCause.Rocket, true);
            return true;
        }

        /// <summary>The kill branch: everything but the instance_destroy, which alarm[4] does 10 s later.</summary>
        void Defeat(EnemyDeathCause cause, bool byPlayer)
        {
            Vec2 at = Position;
            if (byPlayer) Emit(new ScoreAwarded { At = at, Points = EnemyTable.Get(Kind).Points });
            // with(obj_shrapnel_piece) mode = "float": send the shrapnel flying.
            foreach (var piece in pieces)
                if (!piece.IsDead) piece.Fly();
            for (int i = 0; i < PickupCount; i++)
            {
                int dx = Rng.IRandom(-PickupScatterPx, PickupScatterPx);
                int dy = Rng.IRandom(-PickupScatterPx, PickupScatterPx);
                Emit(new EnemyPickupDropped { At = at + new Vec2(GameClock.Px(dx), -GameClock.Px(dy)) });
            }
            Alive = false;
            SpeedPxPerStep = 0f;
            defeatCause = cause;
            defeatedByPlayer = byPlayer;
            Emit(new ScrapmentalDefeated());
            Emit(new GrenadeExplosionRaised { At = at });
            DeathBurst();
            deathAlarm = DeathDelaySteps;
            Emit(new DeathParticlesRaised { At = at });
        }
    }

    /// <summary>What an obj_shrapnel_piece is doing.</summary>
    public enum ShrapnelPieceMode
    {
        /// <summary>Orbiting the boss on its undulate radius.</summary>
        Attached,
        /// <summary>Shot loose (or let go at the boss's defeat), flying off.</summary>
        Float,
        /// <summary>Caught by a harpoon and being reeled back.</summary>
        Grabbed
    }

    /// <summary>
    /// The rules of obj_shrapnel_piece (gmlHARE, par_enemy): one of the boss's 36 hull plates (spr_boss_hull1..8, origin
    /// on the left edge, so the plate sticks outward). Attached, it sits on the boss's undulate radius along its own
    /// image_angle and spins (-4..4, re-rolled to -3..3 every 120-340 steps). Every bullet that reaches it is spent with a
    /// tiny explosion and every rocket explodes on it; while attached, the ship's bullets darken it by 16 and rockets by
    /// 48, and the hit after RGB reaches 128 sets alarm[1] to its death_delay (0-3 s): then it scores 20 (the first time
    /// only) and flies off along its angle at 8-12 px/step, gone once a sprite size outside the room. A harpoon can catch a
    /// flying piece and reel it back; it re-attaches (at RGB 196) once its box is inside the boss's undulate radius.
    /// </summary>
    public sealed class ShrapnelPieceBody : EnemyBody
    {
        public const int BulletDamage = 16;
        public const int RocketDamage = 48;
        public const int ReattachRgb = 196;
        public const int MinLooseSpeed = 8;
        public const int MaxLooseSpeed = 12;
        /// <summary>A grabbed piece sits lengthdir(-40, image_angle) from its harpoon.</summary>
        public const float GrabOffsetPx = -40f;
        /// <summary>spr_boss_hull* size: sprite_width and sprite_height for the out-of-room check.</summary>
        public const float SpriteWidthPx = 200f;
        public const float SpriteHeightPx = 128f;
        const float OriginXPx = 0f, OriginYPx = 64f;

        /// <summary>spr_boss_hull1..8's bounding boxes (left, top, right, bottom), GML px.</summary>
        static readonly float[,] HullBoxes =
        {
            { 55, 0, 199, 124 }, { 84, 18, 199, 111 }, { 105, 23, 199, 109 }, { 62, 8, 199, 110 },
            { 83, 21, 199, 112 }, { 71, 21, 199, 112 }, { 96, 12, 199, 103 }, { 111, 18, 199, 97 }
        };

        /// <summary>spr_boss_hull<paramref name="asset"/>'s bounding box (GML px from the sprite's top-left, inclusive), for its mask.</summary>
        public static void HullBoxPx(int asset, out float left, out float top, out float right, out float bottom)
        {
            int i = Math.Max(1, Math.Min(ScrapmentalEnemy.HullAssets, asset)) - 1;
            left = HullBoxes[i, 0];
            top = HullBoxes[i, 1];
            right = HullBoxes[i, 2];
            bottom = HullBoxes[i, 3];
        }

        /// <summary>spr_boss_hull*'s origin (its left edge, half way down), GML px.</summary>
        public const float OriginX = OriginXPx, OriginY = OriginYPx;

        float changeAlarm;
        float looseAlarm = Off;
        int deathDelay;
        float looseSpeed;
        bool scored;

        internal ShrapnelPieceBody(ScrapmentalEnemy parent, int asset)
        {
            Kind = EnemyKind.Scrapmental;
            Parent = parent;
            Asset = asset;
        }

        public override BodyPart Part => BodyPart.Hull;
        public override bool PushesAway => false;

        public ScrapmentalEnemy Parent { get; }
        /// <summary>Which spr_boss_hull (1-8).</summary>
        public int Asset { get; }
        public string SpriteName => "spr_boss_hull" + Asset;
        public ShrapnelPieceMode Mode { get; private set; }
        public int Rgb { get; private set; }
        public float Spin { get; private set; }
        /// <summary>The harpoon reeling it in, while grabbed.</summary>
        public ShrapnelHarpoonBody Harpoon { get; private set; }
        /// <summary>Steps until it comes loose (alarm[1]), or negative when not set.</summary>
        public float LooseStepsLeft => looseAlarm;

        internal void SetAngle(float angle) => ImageAngle = angle;

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Spin = ScrapmentalEnemy.NonZeroSpin(Rng, -4, 4);
            changeAlarm = Rng.IRandom(120, 340);
            deathDelay = Rng.IRandom(0, (int)GameClock.StepsPerSecond * 3);
            Rgb = ScrapmentalEnemy.FullRgb;
            Mode = ShrapnelPieceMode.Attached;
        }

        protected override void Alarms(float steps)
        {
            if (Ring(ref changeAlarm, steps))
            {
                if (!World.Paused) Spin = ScrapmentalEnemy.NonZeroSpin(Rng, -3, 3);
                changeAlarm = Rng.IRandom(120, 340);
            }
            if (Ring(ref looseAlarm, steps))
            {
                if (!scored)
                {
                    scored = true;
                    Emit(new ScoreAwarded { At = Position, Points = EnemyTable.Get(Kind).PartPoints });
                }
                Fly();
            }
        }

        /// <summary>mode = "float"; direction = image_angle; speed = irandom_range(8, 12) (kept, docs/BIBLE.md D28).</summary>
        internal void Fly()
        {
            if (Harpoon != null) Harpoon.LetGo();
            Harpoon = null;
            Mode = ShrapnelPieceMode.Float;
            Direction = ImageAngle;
            looseSpeed = Rng.IRandom(MinLooseSpeed, MaxLooseSpeed);
            SpeedPxPerStep = World.Paused ? 0f : looseSpeed;
        }

        /// <summary>A harpoon caught it (the harpoon's collision with a floating piece).</summary>
        internal void GrabbedBy(ShrapnelHarpoonBody harpoon)
        {
            Mode = ShrapnelPieceMode.Grabbed;
            Harpoon = harpoon;
            SpeedPxPerStep = 0f;
        }

        bool ParentHere => Parent != null && !Parent.IsDead;

        protected override void StepEvent(float steps)
        {
            if (World.Paused)
            {
                SpeedPxPerStep = 0f;
                return;
            }
            switch (Mode)
            {
                case ShrapnelPieceMode.Attached:
                    SpeedPxPerStep = 0f;
                    if (ParentHere)
                    {
                        Teleport(Parent.Position + GmlMath.Dir(ImageAngle) * GameClock.Px(Parent.UndulateY));
                        ImageAngle += Spin * steps;
                    }
                    else Fly(); // the boss is gone (a beam, a flashbomb): fly off like at its defeat (D29)
                    break;

                case ShrapnelPieceMode.Float:
                    SpeedPxPerStep = looseSpeed;
                    // ... || y > room_height + sprite_height (the original tests room_width, D31).
                    float w = GameClock.Px(SpriteWidthPx), h = GameClock.Px(SpriteHeightPx);
                    if (Position.X < Room.XMin - w || Position.X > Room.XMax + w || Position.Y > Room.YMax + h || Position.Y < Room.YMin - h)
                        Die(EnemyDeathCause.Removed, 0, false, false);
                    break;

                case ShrapnelPieceMode.Grabbed:
                    SpeedPxPerStep = 0f;
                    if (ParentHere && Parent.Alive && Harpoon != null && !Harpoon.IsDead)
                    {
                        Teleport(Harpoon.Position + GmlMath.Dir(ImageAngle) * GameClock.Px(GrabOffsetPx));
                        if (DistanceToPointPx(Parent.Position) < Parent.UndulateY)
                        {
                            Mode = ShrapnelPieceMode.Attached;
                            Harpoon.LetGo();
                            Harpoon = null;
                            Rgb = ReattachRgb;
                        }
                    }
                    else Fly(); // its harpoon or boss is gone: in the original it hung there for good (D29)
                    break;
            }
        }

        protected override bool BulletHit(in BulletShot shot)
        {
            TinyExplosionAt(shot.Position);
            // other.owner != ID && other.owner != parent: neither its own nor its boss's slivers damage it (U.HARE-T102).
            bool ownRound = IgnoresRound(shot) || (shot.FromEnemy && shot.ShooterId == Parent.Id);
            if (!ownRound && Mode == ShrapnelPieceMode.Attached)
            {
                if (Rgb > ScrapmentalEnemy.DefeatRgb)
                {
                    Spin = ScrapmentalEnemy.NonZeroSpin(Rng, -3, 3);
                    Rgb -= BulletDamage;
                }
                else looseAlarm = deathDelay;
            }
            return true;
        }

        protected override bool RocketHit(Vec2 rocketAt)
        {
            if (Mode == ShrapnelPieceMode.Attached)
            {
                if (Rgb > ScrapmentalEnemy.DefeatRgb)
                {
                    Rgb -= RocketDamage;
                    Spin = ScrapmentalEnemy.NonZeroSpin(Rng, -3, 3);
                }
                else looseAlarm = deathDelay;
            }
            return true; // obj_rocket_explosion at the rocket, whatever the mode
        }

        // The hull plate's bounding box turns with it (GameMaker's bbox is the rotated sprite's), for distance_to_point.
        protected override Vec2 BBoxHalfPx => RotatedBox(out _);
        protected override Vec2 BBoxOffsetPx
        {
            get
            {
                RotatedBox(out var centre);
                return centre;
            }
        }

        Vec2 RotatedBox(out Vec2 centre)
        {
            int i = Math.Max(1, Math.Min(ScrapmentalEnemy.HullAssets, Asset)) - 1;
            // Local corners, y up, relative to the origin.
            float left = HullBoxes[i, 0] - OriginXPx, right = HullBoxes[i, 2] + 1f - OriginXPx;
            float top = -(HullBoxes[i, 1] - OriginYPx), bottom = -(HullBoxes[i, 3] + 1f - OriginYPx);
            Vec2 ax = GmlMath.Dir(ImageAngle), ay = GmlMath.Dir(ImageAngle + 90f);
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            foreach (float lx in new[] { left, right })
                foreach (float ly in new[] { top, bottom })
                {
                    Vec2 p = ax * lx + ay * ly;
                    minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                    minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                }
            centre = new Vec2((minX + maxX) / 2f, (minY + maxY) / 2f);
            return new Vec2((maxX - minX) / 2f, (maxY - minY) / 2f);
        }
    }

    /// <summary>What an obj_shrapnel_harpoon is doing.</summary>
    public enum ShrapnelHarpoonMode
    {
        /// <summary>Sitting on the boss, turning 1 degree a step.</summary>
        Rotate,
        /// <summary>Shot out along its angle at 8 px/step.</summary>
        Fire,
        /// <summary>Wound back toward the boss at 4 px/step.</summary>
        Reel
    }

    /// <summary>
    /// The rules of obj_shrapnel_harpoon (gmlHARE, par_enemy): one of the boss's 16 grappling arms, made at angles 0, 20 ...
    /// 300. It sits on the boss turning 1 degree a step; after 1-4 s (then 2-6 s after each reel) it fires along its angle
    /// at 8 px/step, its line (3 px, c_gray) drawn from the boss. It reels back at 4 px/step after 2000 px, on touching a
    /// rock, or on catching a floating hull piece, which it drags home. Bullets and rockets pass through it. Once the boss
    /// is defeated (or gone) it bursts in a tiny explosion.
    /// </summary>
    public sealed class ShrapnelHarpoonBody : EnemyBody
    {
        public const float FireSpeedPxPerStep = 8f;
        public const float ReachPx = 2000f;
        public const float ReelPxPerStep = 4f;
        public const float SpinPerStep = 1f;

        float fireAlarm;

        internal ShrapnelHarpoonBody(ScrapmentalEnemy parent)
        {
            Kind = EnemyKind.Scrapmental;
            Parent = parent;
        }

        public override BodyPart Part => BodyPart.Harpoon;
        public override bool PushesAway => false;

        public ScrapmentalEnemy Parent { get; }
        public ShrapnelHarpoonMode Mode { get; private set; }
        /// <summary>GML Len: how far out it is, px.</summary>
        public float LengthPx { get; private set; }
        /// <summary>GML X/Y: where it was fired from (the boss, while reeling).</summary>
        public Vec2 Anchor { get; private set; }
        /// <summary>GML grabbing: it is dragging a piece home.</summary>
        public bool Grabbing { get; private set; }
        /// <summary>Steps until it fires (alarm[0]), or negative when not set.</summary>
        public float FireStepsLeft => fireAlarm;

        internal void SetAngle(float angle) => ImageAngle = angle;

        internal override void Create(EnemyOptions o)
        {
            ImageAlpha = 1f;
            SpeedPxPerStep = 0f;
            Mode = ShrapnelHarpoonMode.Rotate;
            LengthPx = 0f;
            Anchor = Position;
            Grabbing = false;
            fireAlarm = Rng.IRandom((int)GameClock.StepsPerSecond, (int)GameClock.StepsPerSecond * 4);
        }

        protected override void Alarms(float steps)
        {
            if (!Ring(ref fireAlarm, steps)) return;
            if (World.Paused)
            {
                fireAlarm = 1f; // the original's alarm is lost while paused and the arm never fires again (D32)
                return;
            }
            if (Mode == ShrapnelHarpoonMode.Rotate)
            {
                Mode = ShrapnelHarpoonMode.Fire;
                Direction = ImageAngle;
                SpeedPxPerStep = FireSpeedPxPerStep;
            }
        }

        protected override void StepEvent(float steps)
        {
            if (World.Paused)
            {
                SpeedPxPerStep = 0f;
                return;
            }
            if (Parent == null || Parent.IsDead || !Parent.Alive)
            {
                // The original does this only for a defeated boss; one taken by a beam or a flashbomb left them all (D29).
                TinyExplosionAt(Position);
                Die(EnemyDeathCause.Orphaned, 0, false, false);
                return;
            }
            switch (Mode)
            {
                case ShrapnelHarpoonMode.Rotate:
                    SpeedPxPerStep = 0f;
                    Anchor = Parent.Position;
                    Teleport(Anchor);
                    ImageAngle += SpinPerStep * steps;
                    break;

                case ShrapnelHarpoonMode.Fire:
                    SpeedPxPerStep = FireSpeedPxPerStep;
                    LengthPx += FireSpeedPxPerStep * steps;
                    ImageAngle = GmlMath.PointDirection(Anchor, Position);
                    if (LengthPx >= ReachPx) Reel();
                    break;

                case ShrapnelHarpoonMode.Reel:
                    SpeedPxPerStep = 0f;
                    LengthPx -= ReelPxPerStep * steps;
                    Anchor = Parent.Position;
                    ImageAngle = GmlMath.PointDirection(Anchor, Position);
                    Teleport(Anchor + GmlMath.Dir(ImageAngle) * GameClock.Px(LengthPx));
                    if (LengthPx <= 0f)
                    {
                        Mode = ShrapnelHarpoonMode.Rotate;
                        fireAlarm = Rng.IRandom((int)GameClock.StepsPerSecond * 2, (int)GameClock.StepsPerSecond * 6);
                    }
                    break;
            }
        }

        /// <summary>
        /// mode = "reel". The original keeps its 8 px/step built-in speed while it reels and then rotates, so it sits
        /// 8 px off the line it is drawn on; the reel places it itself (D32).
        /// </summary>
        void Reel()
        {
            Mode = ShrapnelHarpoonMode.Reel;
            SpeedPxPerStep = 0f;
        }

        /// <summary>The grabbed piece re-attached (or flew off): harpoon.grabbing = false.</summary>
        internal void LetGo() => Grabbing = false;

        /// <summary>Collision par_rock: a firing harpoon reels in.</summary>
        protected override bool RockHit()
        {
            if (Mode != ShrapnelHarpoonMode.Fire) return false;
            Reel();
            return true;
        }

        /// <summary>Collision obj_shrapnel_piece: a floating piece is caught and dragged home (ef_firework, small, c_orange).</summary>
        internal bool Grab(ShrapnelPieceBody piece)
        {
            if (IsDead || piece == null || piece.IsDead || piece.Mode != ShrapnelPieceMode.Float || Grabbing) return false;
            Reel();
            Grabbing = true;
            piece.GrabbedBy(this);
            Emit(new HarpoonGrabbed { At = Position });
            return true;
        }

        // No bullet or rocket events: both pass through.
        protected override bool BulletHit(in BulletShot shot) => false;
        protected override bool RocketHit(Vec2 rocketAt) => false;
    }

    /// <summary>A harpoon touched a hull piece: it catches the piece when it is floating and the harpoon has none.</summary>
    public sealed class GrabPiece : EnemyCommand
    {
        readonly int harpoonId, pieceId;
        public GrabPiece(int harpoonId, int pieceId) { this.harpoonId = harpoonId; this.pieceId = pieceId; }

        internal override bool Apply(EnemyWorld world, EnemyResult result)
        {
            var harpoon = world.Get(harpoonId) as ShrapnelHarpoonBody;
            var piece = world.Get(pieceId) as ShrapnelPieceBody;
            if (harpoon == null || piece == null) return false;
            result.Body = harpoon;
            return harpoon.Grab(piece);
        }
    }

    /// <summary>Scrap Mental's Alarm 1 volley of <see cref="Slivers"/> slivers.</summary>
    public sealed class ScrapmentalVolley : EnemyEvent { public int Slivers; }

    /// <summary>Scrap Mental was defeated (alive = false; back.mode = "fadeout"): it stops being drawn.</summary>
    public sealed class ScrapmentalDefeated : EnemyEvent { }

    /// <summary>A harpoon caught a hull piece (effect_create_above(ef_firework, x, y, 0, c_orange)).</summary>
    public sealed class HarpoonGrabbed : EnemyEvent { public Vec2 At; }
}
