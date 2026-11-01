using Unity.Rea.Hare.Content;
using Unity.Multiplayer;    // NetBootstrap
using Unity.Multiplayer.UI; // NetFrontEnd, MenuPlacement
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Rea.Hare.Online
{
    /// <summary>
    /// The online scene's one game component (U.HARE-T35). The menus are the kit's drop-in front end (Host, Join by IP,
    /// Join by code, the lobby with its join code and roster, reconnect, the Esc menu); this adds Hare's own items to
    /// it: <c>Play solo</c>, which leaves for the local game exactly as it plays without the kit, and how to play. It
    /// sizes the session from the content registry's online section. Runs on every peer: a dedicated server and a
    /// bot run this scene too, where the front end builds no menus.
    /// </summary>
    [DefaultExecutionOrder(-800)] // before NetBootstrap.Start, which may start a headless server at once
    public sealed class HareOnlineGame : MonoBehaviour
    {
        /// <summary>The local game's scene (Build Settings' first), which <see cref="PlaySolo"/> opens.</summary>
        public const string SoloScene = "Main";

        void Start()
        {
            // A host or a client must keep its connection running while its window is in the background (the kit's
            // New Game turns Run In Background on project-wide; here it is on only in the online scene, so the local
            // game pauses when unfocused exactly as before).
            Application.runInBackground = true;

            var bootstrap = NetBootstrap.Instance;
            if (bootstrap == null || bootstrap.Session == null)
            {
                Debug.LogError("[Unity.Rea.Hare] The online scene has no NetBootstrap; run Unity.Rea.Hare > Rebuild Online Scene.", this);
                return;
            }
            if (!bootstrap.Session.IsActive)
                bootstrap.Configure(c => c.MaxPlayers = GameContent.Current.Online.MaxPlayers);

            var frontEnd = NetFrontEnd.Instance;
            if (frontEnd == null) return;

            frontEnd.AddMainMenuItems(s => s.AddButton("Play solo", PlaySolo));
            frontEnd.AddScreen("How to play", s =>
            {
                s.AddText("Fly with A and D, boost with W or Space, brake with S or Shift; aim with the mouse.");
                s.AddText("Perched on a rock, the boost and brake keys raise your shield.");
                s.AddText("Esc opens the menu: invite players, change your name and colour, or leave.");
            }, MenuPlacement.Both);
            frontEnd.GameStarted += () => Debug.Log("[Unity.Rea.Hare] Online as " + frontEnd.Session.Identity.DisplayName + ".");
        }

        /// <summary>
        /// Leaves the online scene for the local game. The kit's NetworkManager survives scene loads, so it goes
        /// first: local play runs without any of the kit, as it always has.
        /// </summary>
        public static void PlaySolo()
        {
            var bootstrap = NetBootstrap.Instance;
            if (bootstrap != null)
            {
                if (bootstrap.Session != null && bootstrap.Session.IsActive) bootstrap.Leave();
                Destroy(bootstrap.gameObject);
            }
            Application.runInBackground = false; // the project's own setting, as the local game has always run
            SceneManager.LoadScene(SoloScene, LoadSceneMode.Single);
        }
    }
}
