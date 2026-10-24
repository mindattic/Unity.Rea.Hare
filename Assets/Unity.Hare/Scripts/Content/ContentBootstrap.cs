using UnityEngine;
using Unity.Hare.Content;

namespace Unity.Hare
{
    /// <summary>
    /// Loads the game's tuning/content data file (Resources/Hare/Content/GameContent.json) into
    /// <see cref="GameContent.Current"/> before any scene's Awake runs, so every MonoBehaviour that reads
    /// <see cref="GameContent.Current"/> in a field initializer (PlayerShipController, PlayerHealth, PlayerAmmo, the
    /// weapons) sees the loaded numbers rather than the compiled-in defaults. A missing file is not an error (a
    /// fresh checkout or a stripped build still runs on the compiled-in defaults); a file that exists but fails to
    /// parse or validate logs every problem (<see cref="ContentValidationException.Errors"/>) and falls back to
    /// <see cref="ContentRegistry.Default"/> rather than taking the game down over a typo in a data file.
    /// </summary>
    public static class ContentBootstrap
    {
        public const string ResourcePath = "Hare/Content/GameContent";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) return; // no data file: keep ContentRegistry.Default

            try
            {
                GameContent.Current = ContentRegistry.FromJson(asset.text);
            }
            catch (ContentValidationException e)
            {
                Debug.LogError($"[Content] {ResourcePath}.json failed validation; using the compiled-in defaults instead.\n{e.Message}");
                GameContent.Current = ContentRegistry.Default;
            }
        }
    }
}
