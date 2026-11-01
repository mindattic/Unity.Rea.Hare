using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Flow;

namespace Unity.Rea.Hare.Pointer
{
    /// <summary>
    /// One room's obj_mouse (U.HARE-T61): the room's controller creates it in its Create event and sets its sprite and
    /// visibility there. obj_title: <c>mouse.sprite_index = spr_pointer; mouse.visible = false</c> (obj_logo's Create sets
    /// it visible); obj_map and obj_upgrade: <c>mouse.sprite_index = spr_pointer</c>; obj_game: the object's own
    /// spr_reticle_blue, which obj_player's Step then recolours.
    /// </summary>
    [Serializable]
    public class MouseRoomDefinition
    {
        public RoomId Room;
        /// <summary>The sprite the room's controller leaves on it.</summary>
        public string Sprite = "spr_pointer";
        /// <summary>Whether it is visible once the controller's Create has run.</summary>
        public bool Visible = true;
        /// <summary>Whether obj_player's Step recolours it every step (the play rooms, whose obj_game places the ship).</summary>
        public bool FollowsShip;

        public MouseRoomDefinition() { }

        public MouseRoomDefinition(RoomId room, string sprite, bool visible, bool followsShip)
        {
            Room = room;
            Sprite = sprite;
            Visible = visible;
            FollowsShip = followsShip;
        }

        internal void Validate(List<string> errors, string path)
        {
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
        }
    }

    /// <summary>
    /// obj_mouse's numbers (U.HARE-T61, the content registry's <c>mouse</c> section): its sprite and depth, the three
    /// reticles obj_player's Step picks between, the rooms whose controller creates one and HARE.gmx's
    /// <c>option_showcursor</c> (False: the system cursor is hidden for the whole game).
    /// </summary>
    [Serializable]
    public class MouseTuning
    {
        /// <summary>obj_mouse's own sprite.</summary>
        public string Sprite = "spr_reticle_blue";
        /// <summary>obj_mouse's depth: -5000, over every room object.</summary>
        public int Depth = -5000;
        /// <summary>obj_player's Step, perched with can_jump: <c>obj_mouse.sprite_index = spr_reticle_blue</c>.</summary>
        public string CanLeapSprite = "spr_reticle_blue";
        /// <summary>obj_player's Step, perched without can_jump: spr_reticle_red.</summary>
        public string CannotLeapSprite = "spr_reticle_red";
        /// <summary>obj_player's Step, in flight (<c>state == state_moving</c>): spr_reticle_green.</summary>
        public string FlyingSprite = "spr_reticle_green";
        /// <summary>HARE.gmx's Default.config.gmx <c>option_showcursor</c>: False.</summary>
        public bool ShowSystemCursor;

        /// <summary>The rooms that create an obj_mouse, as their controllers' Create events set it up.</summary>
        public MouseRoomDefinition[] Rooms =
        {
            new MouseRoomDefinition(RoomId.Title, "spr_pointer", visible: false, followsShip: false),
            new MouseRoomDefinition(RoomId.GameScreen, "spr_reticle_blue", visible: true, followsShip: true),
            new MouseRoomDefinition(RoomId.SideScroll, "spr_reticle_blue", visible: true, followsShip: true),
            new MouseRoomDefinition(RoomId.Shaft, "spr_reticle_blue", visible: true, followsShip: true),
            new MouseRoomDefinition(RoomId.Map, "spr_pointer", visible: true, followsShip: false),
            new MouseRoomDefinition(RoomId.Upgrade, "spr_pointer", visible: true, followsShip: false),
        };

        /// <summary>The obj_mouse <paramref name="room"/>'s controller creates, or null when it creates none.</summary>
        public MouseRoomDefinition RoomFor(RoomId room)
        {
            if (Rooms == null) return null;
            foreach (var definition in Rooms)
                if (definition != null && definition.Room == room) return definition;
            return null;
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Required(errors, path + ".CanLeapSprite", CanLeapSprite);
            ValidationHelpers.Required(errors, path + ".CannotLeapSprite", CannotLeapSprite);
            ValidationHelpers.Required(errors, path + ".FlyingSprite", FlyingSprite);
            if (Rooms == null) { errors.Add(path + ".Rooms: must be a list (it may be empty)."); return; }
            var seen = new HashSet<RoomId>();
            for (int i = 0; i < Rooms.Length; i++)
            {
                string entryPath = $"{path}.Rooms[{i}]";
                if (Rooms[i] == null) { errors.Add(entryPath + ": must not be null."); continue; }
                Rooms[i].Validate(errors, entryPath);
                if (!seen.Add(Rooms[i].Room)) errors.Add($"{entryPath}.Room: {Rooms[i].Room} is listed more than once.");
            }
        }
    }
}
