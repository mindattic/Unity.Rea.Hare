using System;
using UnityEngine;

namespace Unity.Hare.Input
{
    /// <summary>
    /// HARE.gmx's <c>option_showcursor</c> (False, U.HARE-T61): the system cursor is hidden while the game's rooms run, so
    /// only obj_mouse's pointer or reticle shows. The room flow hides it as a room comes up and gives it back as it
    /// leaves the rooms (a scene that is not a room, or the flow going away), restoring what it was before, so a test or
    /// the Editor never keeps a hidden cursor after the game.
    /// </summary>
    public static class SystemCursor
    {
        static bool hidden;
        static bool before = true;

        /// <summary>Reads the system cursor's visibility; a seam for tests.</summary>
        public static Func<bool> GetVisible = () => Cursor.visible;

        /// <summary>Sets the system cursor's visibility; a seam for tests.</summary>
        public static Action<bool> SetVisible = value => Cursor.visible = value;

        /// <summary>Whether this has hidden the cursor and not yet given it back.</summary>
        public static bool Hidden => hidden;

        /// <summary>Hides the system cursor, remembering whether it showed.</summary>
        public static void Hide()
        {
            if (hidden) return;
            before = GetVisible();
            SetVisible(false);
            hidden = true;
        }

        /// <summary>Gives back the cursor <see cref="Hide"/> took, as it was.</summary>
        public static void Restore()
        {
            if (!hidden) return;
            hidden = false;
            SetVisible(before);
        }

        /// <summary>A room came up: <c>option_showcursor</c> says whether the system cursor shows in it.</summary>
        public static void ApplyForRoom(bool showSystemCursor)
        {
            if (showSystemCursor) Restore();
            else Hide();
        }
    }
}
