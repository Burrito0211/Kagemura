using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kagemura.UI
{
    /// <summary>
    /// The two scene names every menu needs, and one safe way to load them.
    ///
    /// Split out because SceneManager.LoadScene fails on a scene that is not in Build Settings,
    /// and it fails with an error that names the scene but never says why — which is a bad first
    /// experience for whoever wires the menus up. This checks first and explains.
    ///
    /// Loading also restores the time scale before it goes: a menu pauses by setting timeScale to
    /// 0, and timeScale survives a scene load, so a screen that loads while paused hands the next
    /// scene a frozen clock. That reads as a hang rather than a menu bug, which is why it is
    /// handled here rather than left to each screen to remember.
    /// </summary>
    public static class SceneRoutes
    {
        // Must match the scene file's name exactly, spaces included — SceneManager matches on the
        // name, so renaming the asset silently kills every "Quit to Menu" button in the game.
        public const string DefaultMainMenu = "Main Menu";
        public const string DefaultFirstLevel = "Game";

        public static void Load(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[SceneRoutes] No scene name given.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneRoutes] Scene '{sceneName}' is not in Build Settings, so " +
                               "it cannot be loaded. Add it under File > Build Profiles > Scene " +
                               "List. (Play mode does not need this; a build does.)");
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>Reload the scene currently running — the Restart button.</summary>
        public static void Reload()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Leave the game. Does nothing in the editor beyond a log, since Application.Quit is a
        /// no-op there and a Quit button that appears dead is worth explaining.
        /// </summary>
        public static void Quit()
        {
#if UNITY_EDITOR
            Debug.Log("[SceneRoutes] Quit pressed. Ignored in the editor; this exits a build.");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
