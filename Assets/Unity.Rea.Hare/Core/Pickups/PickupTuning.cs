using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Pickups
{
    /// <summary>
    /// The pickups' and the kicked-up scraps' numbers, as gmlHARE has them (the content file's <c>pickups</c> section):
    /// random_pickup's rolls, obj_carrot's hover and ping, obj_coin's grades, fall and sparkle, the shared magnet
    /// (<c>distance_to_point(player) &lt; 48</c> then <c>move_towards_point(player, 4)</c>), spawn_chips and obj_chip,
    /// obj_cartridge (the vulcan's spr_ammo_cartridge, the shotgun's spr_ammo_shell). Distances are GML px, times steps.
    /// </summary>
    public sealed class PickupTuning
    {
        // ---- random_pickup ----

        /// <summary><c>rndpickup = irandom_range(1, 2)</c>: 1 rolls for a carrot, 2 drops a coin.</summary>
        public int PickupRollMax = 2;
        /// <summary><c>if (irandom_range(1, 4) == 1)</c>: the carrot roll (its coin roll is commented out, so a coin always drops).</summary>
        public int CarrotOneIn = 4;

        // ---- The magnet both pickups share ----

        /// <summary><c>distance_to_point(player.x, player.y) &lt; 48</c>, from the pickup's bounding box.</summary>
        public float MagnetRangePx = 48f;
        /// <summary><c>move_towards_point(player.x, player.y, 4)</c>: speed 4 toward the ship (it keeps it once out of range).</summary>
        public float MagnetSpeedPx = 4f;

        // ---- obj_carrot ----

        /// <summary>The carrot's HP: <c>player.HP += 1</c> while below MAXHP.</summary>
        public int CarrotHeal = 1;
        /// <summary>Alarm 0's <c>rate</c>: the hover moves every 8 steps.</summary>
        public float CarrotHoverEverySteps = 8f;
        /// <summary><c>hoverAmount</c>: hoverY swings between -4 and 4.</summary>
        public float CarrotHoverAmountPx = 4f;
        /// <summary><c>hoverSpeed</c>: hoverY changes 1 a hover.</summary>
        public float CarrotHoverSpeedPx = 1f;
        /// <summary>Alarm 1's <c>ping_rate</c>: a red ef_ring every 120 steps.</summary>
        public float CarrotPingEverySteps = 120f;
        /// <summary>spr_pickup_health's bounding box about its origin (7, 12): left, top, right, bottom, GML px (y down).</summary>
        public float[] CarrotBox = { -7f, -12f, 6f, 11f };

        // ---- obj_coin ----

        /// <summary><c>rndvalue = irandom_range(1, 100)</c>.</summary>
        public int CoinRollMax = 100;
        /// <summary><c>rndvalue &lt;= 75</c>: spr_coin_blue, value 5.</summary>
        public int BlueCoinMaxRoll = 75;
        /// <summary><c>rndvalue &gt;= 98</c>: spr_coin_gold, value 20 (between the two: spr_coin_silver, value 10).</summary>
        public int GoldCoinMinRoll = 98;
        public int BlueCoinValue = 5;
        public int SilverCoinValue = 10;
        public int GoldCoinValue = 20;
        /// <summary>The Step's <c>y += 1</c> while the ship is out of range.</summary>
        public float CoinFallPxPerStep = 1f;
        /// <summary>Alarm 0's <c>sparkle_rate</c>: an ef_spark every 5 steps.</summary>
        public float CoinSparkleEverySteps = 5f;
        /// <summary><c>rnd_x = irandom_range(-2, 2)</c>, likewise y.</summary>
        public int CoinSparkleJitterPx = 2;
        /// <summary>The coins' bounding box about their origin (16, 16), GML px.</summary>
        public float[] CoinBox = { -16f, -16f, 15f, 15f };
        /// <summary>sprite_height: a coin is gone once <c>y &gt; room_height + sprite_height</c>.</summary>
        public float CoinHeightPx = 32f;

        // ---- Kicking up dirt: player_leap and the par_rock landing ----

        /// <summary><c>Len = 32; feetX = x + lengthdir_x(Len, image_angle)</c>.</summary>
        public float FeetDistancePx = 32f;

        // ---- spawn_chips and obj_chip ----

        /// <summary><c>repeat(irandom_range(6, 12))</c>.</summary>
        public int ChipCountMin = 6;
        public int ChipCountMax = 12;
        /// <summary>Each at <c>feetY + irandom_range(-3, 3)</c>.</summary>
        public int ChipJitterPx = 3;
        /// <summary><c>image_xscale = image_yscale = 0.5</c>.</summary>
        public float ChipScale = 0.5f;
        /// <summary>spr_chip1..5's heights, px (sprite_height is this times the scale).</summary>
        public float[] ChipHeightsPx = { 8f, 10f, 10f, 12f, 12f };
        /// <summary><c>direction = player.image_angle + irandom_range(-80, 80)</c> (the chip never gets a speed, so it is never used).</summary>
        public int ChipDirectionSpread = 80;
        /// <summary><c>spin = irandom_range(-6, 6)</c>, 3 when it rolls 0.</summary>
        public int ChipSpinMax = 6;
        /// <summary><c>velocityX = irandom_range(-4, 4)</c>.</summary>
        public int ChipVelocityXMax = 4;
        /// <summary><c>velocityY = irandom_range(2, 8)</c>.</summary>
        public int ChipVelocityYMin = 2;
        public int ChipVelocityYMax = 8;
        /// <summary>The "huge spurt": <c>if (irandom_range(1, 32) == 1) velocityY = irandom_range(8, 16)</c>.</summary>
        public int ChipSpurtOneIn = 32;
        public int ChipSpurtVelocityYMin = 8;
        public int ChipSpurtVelocityYMax = 16;
        /// <summary>Alarm 0's <c>rate</c>: a chip moves every 3 steps.</summary>
        public float ChipMoveEverySteps = 3f;

        // ---- obj_cartridge ----

        /// <summary><c>velocityX = irandom_range(-5, 5)</c>.</summary>
        public int CasingVelocityXMax = 5;
        /// <summary><c>velocityY = irandom_range(2, 12)</c>.</summary>
        public int CasingVelocityYMin = 2;
        public int CasingVelocityYMax = 12;
        /// <summary><c>spin = irandom_range(-3, 3)</c>, 3 when it rolls 0.</summary>
        public int CasingSpinMax = 3;
        /// <summary>Alarm 0's <c>rate</c>: a casing moves every 2 steps.</summary>
        public float CasingMoveEverySteps = 2f;
        /// <summary>spr_ammo_cartridge's and spr_ammo_shell's heights, px.</summary>
        public float CartridgeHeightPx = 2f;
        public float ShellHeightPx = 4f;

        // ---- Both scraps' Alarm 0 ----

        /// <summary><c>y -= velocityY; velocityY -= 1</c>.</summary>
        public float ScrapVelocityLossPx = 1f;
        /// <summary><c>//Gravity y += 1</c>.</summary>
        public float ScrapGravityPx = 1f;
        /// <summary>The spin a 0 roll becomes (<c>if (spin == 0) spin = 3</c>).</summary>
        public int ScrapZeroSpin = 3;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".PickupRollMax", PickupRollMax);
            ValidationHelpers.Positive(errors, path + ".CarrotOneIn", CarrotOneIn);
            ValidationHelpers.NonNegative(errors, path + ".MagnetRangePx", MagnetRangePx);
            ValidationHelpers.NonNegative(errors, path + ".MagnetSpeedPx", MagnetSpeedPx);
            ValidationHelpers.Positive(errors, path + ".CarrotHeal", CarrotHeal);
            ValidationHelpers.Positive(errors, path + ".CarrotHoverEverySteps", CarrotHoverEverySteps);
            ValidationHelpers.NonNegative(errors, path + ".CarrotHoverAmountPx", CarrotHoverAmountPx);
            ValidationHelpers.Positive(errors, path + ".CarrotHoverSpeedPx", CarrotHoverSpeedPx);
            ValidationHelpers.Positive(errors, path + ".CarrotPingEverySteps", CarrotPingEverySteps);
            Box(errors, path + ".CarrotBox", CarrotBox);
            ValidationHelpers.Positive(errors, path + ".CoinRollMax", CoinRollMax);
            if (BlueCoinMaxRoll < 1 || BlueCoinMaxRoll >= GoldCoinMinRoll || GoldCoinMinRoll > CoinRollMax)
                errors.Add($"{path}: need 1 <= BlueCoinMaxRoll < GoldCoinMinRoll <= CoinRollMax (was {BlueCoinMaxRoll}, {GoldCoinMinRoll}, {CoinRollMax}).");
            ValidationHelpers.Positive(errors, path + ".BlueCoinValue", BlueCoinValue);
            ValidationHelpers.Positive(errors, path + ".SilverCoinValue", SilverCoinValue);
            ValidationHelpers.Positive(errors, path + ".GoldCoinValue", GoldCoinValue);
            ValidationHelpers.NonNegative(errors, path + ".CoinFallPxPerStep", CoinFallPxPerStep);
            ValidationHelpers.Positive(errors, path + ".CoinSparkleEverySteps", CoinSparkleEverySteps);
            ValidationHelpers.NonNegative(errors, path + ".CoinSparkleJitterPx", CoinSparkleJitterPx);
            Box(errors, path + ".CoinBox", CoinBox);
            ValidationHelpers.NonNegative(errors, path + ".CoinHeightPx", CoinHeightPx);
            ValidationHelpers.NonNegative(errors, path + ".FeetDistancePx", FeetDistancePx);
            ValidationHelpers.NonNegative(errors, path + ".ChipCountMin", ChipCountMin);
            if (ChipCountMax < ChipCountMin) errors.Add($"{path}.ChipCountMax: must be at least ChipCountMin (was {ChipCountMax}).");
            ValidationHelpers.NonNegative(errors, path + ".ChipJitterPx", ChipJitterPx);
            ValidationHelpers.Positive(errors, path + ".ChipScale", ChipScale);
            if (ChipHeightsPx == null || ChipHeightsPx.Length == 0) errors.Add($"{path}.ChipHeightsPx: needs one height per chip sprite.");
            ValidationHelpers.NonNegative(errors, path + ".ChipDirectionSpread", ChipDirectionSpread);
            ValidationHelpers.NonNegative(errors, path + ".ChipSpinMax", ChipSpinMax);
            ValidationHelpers.NonNegative(errors, path + ".ChipVelocityXMax", ChipVelocityXMax);
            if (ChipVelocityYMax < ChipVelocityYMin) errors.Add($"{path}.ChipVelocityYMax: must be at least ChipVelocityYMin (was {ChipVelocityYMax}).");
            ValidationHelpers.Positive(errors, path + ".ChipSpurtOneIn", ChipSpurtOneIn);
            if (ChipSpurtVelocityYMax < ChipSpurtVelocityYMin) errors.Add($"{path}.ChipSpurtVelocityYMax: must be at least ChipSpurtVelocityYMin (was {ChipSpurtVelocityYMax}).");
            ValidationHelpers.Positive(errors, path + ".ChipMoveEverySteps", ChipMoveEverySteps);
            ValidationHelpers.NonNegative(errors, path + ".CasingVelocityXMax", CasingVelocityXMax);
            if (CasingVelocityYMax < CasingVelocityYMin) errors.Add($"{path}.CasingVelocityYMax: must be at least CasingVelocityYMin (was {CasingVelocityYMax}).");
            ValidationHelpers.NonNegative(errors, path + ".CasingSpinMax", CasingSpinMax);
            ValidationHelpers.Positive(errors, path + ".CasingMoveEverySteps", CasingMoveEverySteps);
            ValidationHelpers.NonNegative(errors, path + ".CartridgeHeightPx", CartridgeHeightPx);
            ValidationHelpers.NonNegative(errors, path + ".ShellHeightPx", ShellHeightPx);
        }

        static void Box(List<string> errors, string path, float[] box)
        {
            if (box == null || box.Length != 4 || box[2] < box[0] || box[3] < box[1])
                errors.Add($"{path}: must be [left, top, right, bottom] with right >= left and bottom >= top.");
        }
    }
}
