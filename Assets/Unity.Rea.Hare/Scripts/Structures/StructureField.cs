using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Rocks;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Structures
{
    /// <summary>
    /// The level's structures, porting setup_level's structure pass (U.HARE-T56):
    /// <list type="bullet">
    /// <item>with(par_structure) and with(par_structure_cover) instance_destroy().</item>
    /// <item>rnd_structure = irandom_range(structure_min, structure_max) par_structure, each at
    /// irandom_range(player_size, room - player_size) on each axis, with a par_structure_cover made on it at its
    /// image_angle, spr_structure1 under spr_structure1_cover.</item>
    /// <item>move_rocks's structure half: every structure in the top 800 px (y &lt; 800) within 400 px of the room's centre
    /// line is destroyed with its cover, clearing the ship's spawn point; nothing replaces it.</item>
    /// </list>
    /// structure_min / structure_max are each level's <see cref="Arena.LevelSetting.StructureMin"/> /
    /// <see cref="Arena.LevelSetting.StructureMax"/> (1-3 in level_02 and level_04, the shaft; 2-4 in level_07).
    /// </summary>
    public class StructureField : MonoBehaviour
    {
        readonly List<Structure> structures = new List<Structure>();
        Rect room;
        bool hasRoom;

        public Rect Room => hasRoom ? room : Gml.Room;
        public IReadOnlyList<Structure> Structures => structures;

        /// <summary>A new field object in the scene.</summary>
        public static StructureField CreateField(Rect? room = null)
        {
            var field = new GameObject("StructureField").AddComponent<StructureField>();
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

        /// <summary>The setup_level structure pass: clear, place irandom_range(min, max), then move_rocks's structure half.</summary>
        public void Setup(int structureMin, int structureMax)
        {
            ClearAll();
            int count = Structure.IRandom(structureMin, structureMax);
            float widthPx = Room.width * Gml.PixelsPerUnit, heightPx = Room.height * Gml.PixelsPerUnit;
            int size = (int)RockField.PlayerSizePx;
            for (int i = 0; i < count; i++)
            {
                float x = Structure.IRandom(size, (int)(widthPx - size));
                float y = Structure.IRandom(size, (int)(heightPx - size));
                Place(FromGml(x, y));
            }
            MoveStructures();
        }

        /// <summary>move_rocks's with(par_structure): clears the structures over the ship's spawn point.</summary>
        public void MoveStructures()
        {
            float centreX = Room.center.x;
            float top = Room.yMax - Gml.Px(RockField.SpawnClearDepthPx);
            float half = Gml.Px(RockField.SpawnClearHalfWidthPx);
            foreach (var structure in structures.ToArray())
            {
                if (structure == null) continue;
                Vector2 p = structure.Position;
                if (p.y > top && Mathf.Abs(p.x - centreX) <= half + 1e-5f) structure.Clear();
            }
        }

        /// <summary>with(par_structure) / with(par_structure_cover) instance_destroy() for this field's structures.</summary>
        public void ClearAll()
        {
            foreach (var structure in structures.ToArray())
                if (structure != null) structure.Clear();
            structures.Clear();
        }

        /// <summary>
        /// One structure of setup_level's pass at world point <paramref name="at"/>: instance_create(x, y, par_structure)
        /// and its par_structure_cover.
        /// </summary>
        public Structure Place(Vector2 at)
        {
            var structure = Structure.Create(at);
            structure.transform.SetParent(transform, true);
            if (structure.Cover != null) structure.Cover.transform.SetParent(transform, true);
            structure.Field = this;
            structures.Add(structure);
            return structure;
        }

        internal void OnStructureGone(Structure structure) => structures.Remove(structure);
    }
}
