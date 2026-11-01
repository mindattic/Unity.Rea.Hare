using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Rocks
{
    /// <summary>
    /// The level's asteroid field, porting gmlHARE's rock scripts:
    /// <list type="bullet">
    /// <item>setup_level: with(par_rock) instance_destroy(), then irandom_range(rock_min, rock_max)
    /// par_rock instances at irandom_range(player_size, room - player_size) on each axis
    /// (player_size = spr_rabbit_idle's 96 px), each moving or still by stationary_percent and with
    /// its spin rolled again, and finally move_rocks.</item>
    /// <item>move_rocks: every rock in the top 800 px (y &lt; 800) within 400 px of the room's centre
    /// line is destroyed, clearing the ship's spawn point; nothing replaces those.</item>
    /// <item>create_new_rock: a new par_rock just outside the left or right edge (x = -size or
    /// room_width + size) at a random height, moving 0-1.5 px/step, spin rolled again.</item>
    /// <item>rock_destroyed: 8-12 obj_flotsum within 40 px, fling_player if the ship stood on it
    /// (UH-T53, via <see cref="Rock.Removed"/>), then create_new_rock. A rock that drifts out of the
    /// room is replaced the same way (no flotsum), so the field keeps its size.</item>
    /// </list>
    /// Per level (level_01-10): rocks 15-30 (level_default, 1, 2, 6, 7, 9, generator), 15-20
    /// (3), 20 (4, the shaft), 0 (5, the side-scroll), 20-24 (8); stationary_percent 50 (1,
    /// generator), 25 (2), 15 (3), 75 (6), 30 (7), 60 (8), and whatever the level before left for the
    /// rest. The numbers are each level's data (<see cref="Arena.LevelSetting.RockMin"/>,
    /// <see cref="Arena.LevelSetting.StationaryPercentAfter"/>, U.HARE-T20); U.HARE-T57 passes them to <see cref="Setup"/>.
    /// create_new_rock gave its rock a random heading over the sign it meant for the side it came
    /// from, so half the new rocks left at once and the still ones sat parked outside the room for
    /// good, thinning the field; fixed: a new rock always heads into the room, at 0.1-1.5 px/step.
    /// </summary>
    public class RockField : MonoBehaviour
    {
        /// <summary>level_default.</summary>
        public const int DefaultRockMin = 15;
        public const int DefaultRockMax = 30;
        public const int DefaultStationaryPercent = 50;
        /// <summary>
        /// globals player_size = sprite_get_width(spr_rabbit_idle): the arena's one PlayerSize number (96 px), which the
        /// art pipeline's tests hold to the imported sprite's width.
        /// </summary>
        public static float PlayerSizePx => Content.GameContent.Current.Arena.PlayerSize;
        /// <summary>move_rocks: y &lt; 800 and a_near_b(x, room_width / 2, 400).</summary>
        public const float SpawnClearDepthPx = 800f;
        public const float SpawnClearHalfWidthPx = 400f;
        public const int FlotsumMin = 8;
        public const int FlotsumMax = 12;
        public const int FlotsumSpreadPx = 40;
        /// <summary>create_new_rock: irandom_range(0, 15) * 0.1 px/step (fixed: at least 0.1, see the class notes).</summary>
        public const int NewRockSpeedTenthsMax = 15;

        readonly List<Rock> rocks = new List<Rock>();
        Rect room;
        bool hasRoom;

        /// <summary>stationary_percent for the rocks this field creates through par_rock's Create.</summary>
        public int StationaryPercent { get; set; } = DefaultStationaryPercent;

        public Rect Room => hasRoom ? room : Gml.Room;
        public IReadOnlyList<Rock> Rocks => rocks;

        /// <summary>Each rock create_new_rock adds.</summary>
        public event Action<Rock> RockCreated;

        /// <summary>A new field object in the scene.</summary>
        public static RockField CreateField(Rect? room = null)
        {
            var field = new GameObject("RockField").AddComponent<RockField>();
            if (room.HasValue) field.SetRoom(room.Value);
            return field;
        }

        public void SetRoom(Rect value)
        {
            room = value;
            hasRoom = true;
        }

        /// <summary>GML x, y (px from the room's top left, y down) as a world point in this field's room.</summary>
        public Vector2 FromGml(float xPx, float yPx) => new Vector2(Room.xMin + Gml.Px(xPx), Room.yMax - Gml.Px(yPx));

        /// <summary>The setup_level rock pass: clear, place irandom_range(min, max), then move_rocks.</summary>
        public void Setup(int rockMin = DefaultRockMin, int rockMax = DefaultRockMax, int stationaryPercent = DefaultStationaryPercent)
        {
            StationaryPercent = stationaryPercent;
            ClearAll();
            int count = Rock.IRandom(rockMin, rockMax);
            float widthPx = Room.width * Gml.PixelsPerUnit, heightPx = Room.height * Gml.PixelsPerUnit;
            for (int i = 0; i < count; i++)
            {
                float x = Rock.IRandom((int)PlayerSizePx, (int)(widthPx - PlayerSizePx));
                float y = Rock.IRandom((int)PlayerSizePx, (int)(heightPx - PlayerSizePx));
                var rock = Add(FromGml(x, y));
                rock.RollSpin();
            }
            MoveRocks();
        }

        /// <summary>GML move_rocks: clears the rocks over the ship's spawn point.</summary>
        public void MoveRocks()
        {
            float centreX = Room.center.x;
            float top = Room.yMax - Gml.Px(SpawnClearDepthPx);
            float half = Gml.Px(SpawnClearHalfWidthPx);
            foreach (var rock in rocks.ToArray())
            {
                if (rock == null) continue;
                Vector2 p = rock.Position;
                if (p.y > top && Mathf.Abs(p.x - centreX) <= half + 1e-5f) rock.Clear();
            }
        }

        /// <summary>with(par_rock) instance_destroy().</summary>
        public void ClearAll()
        {
            foreach (var rock in rocks.ToArray())
                if (rock != null) rock.Clear();
            rocks.Clear();
        }

        /// <summary>GML create_new_rock: a rock entering from just past the left or right edge.</summary>
        public Rock CreateNewRock()
        {
            var rock = Add(Vector2.zero);
            float size = Gml.Px(rock.SizePx);
            bool fromLeft = Rock.IRandom(1, 2) == 1;
            float heightPx = Room.height * Gml.PixelsPerUnit;
            float x = fromLeft ? Room.xMin - size : Room.xMax + size;
            float y = Room.yMax - Gml.Px(Rock.IRandom(0, (int)heightPx));
            rock.transform.position = new Vector3(x, y, 0f);

            float speed = Rock.IRandom(1, NewRockSpeedTenthsMax) * 0.1f;
            float heading = Rock.IRandom(0, 359);
            bool outward = fromLeft ? Mathf.Cos(heading * Mathf.Deg2Rad) < 0f : Mathf.Cos(heading * Mathf.Deg2Rad) > 0f;
            if (outward) heading = 180f - heading;
            rock.SetMotion(heading, speed);
            rock.RollSpin();
            RockCreated?.Invoke(rock);
            return rock;
        }

        Rock Add(Vector2 at)
        {
            var rock = Rock.Create(at, StationaryPercent, Room);
            rock.transform.SetParent(transform, true);
            rock.Field = this;
            rocks.Add(rock);
            return rock;
        }

        /// <summary>A rock of this field left play: rock_destroyed for weapon kills, a replacement for strays.</summary>
        internal void OnRockGone(Rock rock, RockGone info)
        {
            rocks.Remove(rock);
            if (info.Destroyed)
            {
                int n = Rock.IRandom(FlotsumMin, FlotsumMax);
                for (int i = 0; i < n; i++)
                {
                    Vector2 offset = new Vector2(Gml.Px(Rock.IRandom(-FlotsumSpreadPx, FlotsumSpreadPx)), Gml.Px(Rock.IRandom(-FlotsumSpreadPx, FlotsumSpreadPx)));
                    Flotsum.Spawn(info.Position + offset);
                }
                CreateNewRock();
            }
            else if (info.Cause == RockRemoval.LeftRoom)
            {
                CreateNewRock();
            }
        }
    }
}
