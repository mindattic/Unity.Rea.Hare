namespace Unity.Rea.Hare.Pickups
{
    /// <summary>The two par_pickup objects.</summary>
    public enum PickupKind
    {
        /// <summary>obj_carrot (spr_pickup_health): one HP.</summary>
        Carrot,
        /// <summary>obj_coin: COINS, and every hundredth an extra life.</summary>
        Coin
    }

    /// <summary>obj_coin's Create roll: which sprite, and so which value.</summary>
    public enum CoinGrade
    {
        /// <summary>spr_coin_blue, value 5 (rolls 1-75).</summary>
        Blue,
        /// <summary>spr_coin_silver, value 10 (rolls 76-97).</summary>
        Silver,
        /// <summary>spr_coin_gold, value 20 (rolls 98-100).</summary>
        Gold
    }

    /// <summary>The purely visual bits that fly off and fall away: obj_chip and obj_cartridge (two sprites).</summary>
    public enum ScrapKind
    {
        /// <summary>obj_chip: spawn_chips' dirt, kicked up where the ship leaps or lands on a rock.</summary>
        Chip,
        /// <summary>obj_cartridge as shoot_vulcan leaves it (spr_ammo_cartridge).</summary>
        Cartridge,
        /// <summary>obj_cartridge as shoot_shotgun leaves it (<c>shell.sprite_index = spr_ammo_shell</c>).</summary>
        Shell
    }

    /// <summary>obj_coin's Alarm 0 spark colours (globals.gml's c_amber, c_pink, c_cornflowerblue).</summary>
    public enum SparkleColor
    {
        Amber,
        Pink,
        CornflowerBlue
    }

    /// <summary>
    /// One obj_carrot or obj_coin in the <see cref="PickupField"/>: where it is (world units, y up), the built-in motion
    /// move_towards_point gave it (direction and speed, kept once the ship is out of range, as GameMaker keeps them), the
    /// carrot's hover and the alarms. Read-only outside the field.
    /// </summary>
    public sealed class Pickup
    {
        internal Pickup(int id, PickupKind kind, CoinGrade grade, int value, Vec2 position)
        {
            Id = id;
            Kind = kind;
            Grade = grade;
            Value = value;
            Position = position;
        }

        public int Id { get; }
        public PickupKind Kind { get; }
        /// <summary>A coin's grade (a carrot's is <see cref="CoinGrade.Blue"/> and means nothing).</summary>
        public CoinGrade Grade { get; }
        /// <summary>A coin's value (5, 10 or 20); a carrot's HP.</summary>
        public int Value { get; }
        public Vec2 Position { get; internal set; }
        /// <summary>GML direction, degrees.</summary>
        public float Direction { get; internal set; }
        /// <summary>GML speed, px a step.</summary>
        public float SpeedPxPerStep { get; internal set; }
        /// <summary>The carrot's hoverUp.</summary>
        public bool HoverUp { get; internal set; } = true;
        /// <summary>The carrot's hoverY, px (GML y down: a positive hover moves it down the screen).</summary>
        public float HoverYPx { get; internal set; }
        public bool IsGone { get; internal set; }

        /// <summary>The GML sprite it shows.</summary>
        public string SpriteName => Kind == PickupKind.Carrot ? "spr_pickup_health"
            : Grade == CoinGrade.Gold ? "spr_coin_gold"
            : Grade == CoinGrade.Silver ? "spr_coin_silver"
            : "spr_coin_blue";

        internal float HoverAlarm;
        internal float PingAlarm;
        internal float SparkleAlarm;
    }

    /// <summary>
    /// One obj_chip or obj_cartridge: no collisions, it only flies up, spins and falls out of the room. Its Alarm 0 moves it
    /// every few steps (<c>x += velocityX; y -= velocityY; velocityY -= 1; y += 1</c>); its Step turns it by spin.
    /// </summary>
    public sealed class Scrap
    {
        internal Scrap(int id, ScrapKind kind, int variant, Vec2 position, float scale)
        {
            Id = id;
            Kind = kind;
            Variant = variant;
            Position = position;
            Scale = scale;
        }

        public int Id { get; }
        public ScrapKind Kind { get; }
        /// <summary>A chip's sprite, 1-5 (spr_chip1..5); 1 for a casing.</summary>
        public int Variant { get; }
        public Vec2 Position { get; internal set; }
        /// <summary>image_xscale and image_yscale.</summary>
        public float Scale { get; }
        /// <summary>image_angle, degrees.</summary>
        public float ImageAngle { get; internal set; }
        /// <summary>Degrees a step.</summary>
        public float Spin { get; internal set; }
        /// <summary>velocityX, px a move (GML x).</summary>
        public float VelocityXPx { get; internal set; }
        /// <summary>velocityY, px a move upward (GML <c>y -= velocityY</c>).</summary>
        public float VelocityYPx { get; internal set; }
        /// <summary>A chip's <c>direction</c>, set in Create and never used (it has no speed).</summary>
        public float Direction { get; internal set; }
        /// <summary>sprite_height: the room's bottom plus this is where it is gone.</summary>
        public float HeightPx { get; internal set; }
        public bool IsGone { get; internal set; }

        /// <summary>The GML sprite it shows.</summary>
        public string SpriteName => Kind == ScrapKind.Chip ? "spr_chip" + Variant
            : Kind == ScrapKind.Shell ? "spr_ammo_shell"
            : "spr_ammo_cartridge";

        internal float MoveEverySteps;
        internal float MoveAlarm;
    }
}
