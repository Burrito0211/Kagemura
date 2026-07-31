using System.Collections.Generic;
using System.IO;
using Kagemura.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Kagemura.DevTools
{
    /// <summary>
    /// One-time setup that finishes Build Order step 13. The four menu screens (spec §5) were
    /// written as code and never put into a scene, which leaves three things broken in ways that
    /// look like bugs in the menus rather than missing wiring:
    ///
    ///   - There is no MainMenu scene at all, so every "Quit to Menu" button in the game routes to
    ///     a scene that does not exist. SceneRoutes catches it and logs, but the button is dead.
    ///   - Game.unity has the HUD and the settings window in it, but no PauseMenu and no EndScreen.
    ///     Escape does nothing and dying shows no screen.
    ///   - Build Settings still contains only the URP template's SampleScene, so a build would ship
    ///     an empty scene and neither of the real ones. Play mode hides this completely.
    ///
    /// Done from the editor API rather than by writing scene YAML by hand, because a malformed
    /// .unity file fails at import with an error that points at the scene rather than at what
    /// produced it.
    ///
    /// Menus need no inspector configuration: GreyboxMenu builds its own canvas and EventSystem,
    /// PauseMenu finds the SettingsWindow, and EndScreen finds the player's Health and the boss.
    /// Adding a bare GameObject with the component on it is genuinely all there is to it.
    ///
    /// Safe to run more than once — every step checks before it acts.
    /// </summary>
    public static class MenuSceneSetup
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string MainMenuScenePath = ScenesFolder + "/Main Menu.unity";
        private const string GameScenePath = ScenesFolder + "/Game.unity";
        private const string TemplateScenePath =
            "Assets/Main menu with parallax FREE/Main Menu Demo.unity";

        [MenuItem("Kagemura/Setup/Finish Menu Wiring (all three steps)")]
        public static void SetUpEverything()
        {
            CreateMainMenuScene();
            AddMenusToOpenScene();
            RegisterScenesInBuildSettings();
        }

        /// <summary>
        /// Build the title screen from the Asset Store template, REPLACING whatever is at
        /// <see cref="MainMenuScenePath"/>.
        ///
        /// The template's own demo scene is not used directly for one reason: its EventSystem
        /// carries a legacy StandaloneInputModule, which does nothing at all under this project's
        /// input backend, so every mouse click on the menu would be silently ignored. Assembling
        /// the scene here means it gets the Input System's module instead.
        ///
        /// The greybox MainMenu component is deliberately not added — the template replaces it.
        /// PauseMenu and EndScreen still use GreyboxMenu; only the title screen changes.
        /// </summary>
        [MenuItem("Kagemura/Setup/1. Build the Main Menu Scene from the Template (replaces it)")]
        public static void CreateMainMenuScene()
        {
            if (!File.Exists(TemplateScenePath))
            {
                Debug.LogError($"[MenuSceneSetup] No template scene at {TemplateScenePath}. " +
                               "Import 'Main menu with parallax FREE' first, or fix the path.");
                return;
            }

            // Copied from the template's demo scene rather than assembled from its prefab, which
            // was the obvious approach and is wrong: the prefab's mainBackground,
            // mainBackgroundParallax, both backgrounds arrays and both AudioClips are all null in
            // the asset. Every one of them exists only as a prefab-instance override stored in the
            // demo scene. A fresh instance therefore reaches Instantiate(null) in
            // MenuController.initiate() and throws before the menu draws anything.
            if (File.Exists(MainMenuScenePath))
            {
                Debug.LogWarning($"[MenuSceneSetup] Replacing the existing {MainMenuScenePath}.");
                AssetDatabase.DeleteAsset(MainMenuScenePath);
            }

            if (!AssetDatabase.CopyAsset(TemplateScenePath, MainMenuScenePath))
            {
                Debug.LogError($"[MenuSceneSetup] Could not copy {TemplateScenePath} to " +
                               $"{MainMenuScenePath}.");
                return;
            }

            AssetDatabase.Refresh();

            // Additive, so whatever the user has open is neither closed nor prompted to save.
            var scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Additive);

            ReplaceLegacyInputModule(scene);
            AddSettingsWindow(scene);

            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, removeScene: true);

            Debug.Log($"[MenuSceneSetup] Built {MainMenuScenePath} from the template's demo scene.");
        }

        /// <summary>
        /// Run <see cref="ReplaceLegacyInputModule"/> against whatever scene is open, without
        /// rebuilding it.
        ///
        /// Separate from step 1 because the failure this fixes outlives the setup run: the symptom
        /// is an InvalidOperationException thrown from EventSystem.Update every frame, and the
        /// natural response to it is to re-run step 1 — which deletes the scene and any inspector
        /// work done since. This does the one thing needed and touches nothing else.
        /// </summary>
        [MenuItem("Kagemura/Setup/Fix the Input Module in the Open Scene")]
        public static void FixInputModuleInOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            if (ReplaceLegacyInputModule(scene) == 0)
            {
                Debug.Log($"[MenuSceneSetup] '{scene.name}' has no legacy input module. Nothing " +
                          "to do. If the exception continues, another loaded scene owns it — " +
                          "check every scene in the Hierarchy, not just this one.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MenuSceneSetup] Patched '{scene.name}'. Save the scene to keep it.");
        }

        /// <summary>
        /// Swap the template's StandaloneInputModule for the Input System's. The legacy module does
        /// nothing under this project's input backend, so the menu would render perfectly and
        /// ignore every click — which reads as a broken template rather than a missing component.
        ///
        /// Returns how many were replaced. Every root is visited rather than stopping at the first
        /// hit: a scene is allowed more than one EventSystem, and leaving a second one in place
        /// leaves the exception firing with the component that caused it apparently already fixed.
        /// </summary>
        private static int ReplaceLegacyInputModule(Scene scene)
        {
            int replaced = 0;

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var legacy in root.GetComponentsInChildren<StandaloneInputModule>(true))
                {
                    var host = legacy.gameObject;
                    Object.DestroyImmediate(legacy);

                    var module = host.AddComponent<InputSystemUIInputModule>();
                    if (InputSystem.actions != null) module.actionsAsset = InputSystem.actions;

                    replaced++;
                    Debug.Log($"[MenuSceneSetup] Replaced the legacy StandaloneInputModule on " +
                              $"'{host.name}'.", host);
                }
            }

            return replaced;
        }

        /// <summary>
        /// Put the rebindable controls window in the scene, so the template's "Options" entry has
        /// something to point at — the template's own options screen does not do rebinding.
        /// </summary>
        private static void AddSettingsWindow(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<SettingsWindow>(true) != null) return;

            var go = new GameObject("Controls Window", typeof(SettingsWindow));
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        /// <summary>
        /// Drop the pause menu and the end screen into whichever scene is open. Deliberately acts
        /// on the open scene rather than on Game.unity by name, so it also works for the level
        /// scenes of Build Order step 9 as they get built.
        /// </summary>
        [MenuItem("Kagemura/Setup/2. Add Pause + End Screens to the Open Scene")]
        public static void AddMenusToOpenScene()
        {
            var scene = SceneManager.GetActiveScene();

            bool added = AddIfMissing<PauseMenu>("Pause Menu");
            added |= AddIfMissing<EndScreen>("End Screen");

            if (!added)
            {
                Debug.Log($"[MenuSceneSetup] '{scene.name}' already has both screens.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[MenuSceneSetup] Added the missing screens to '{scene.name}'. " +
                      "Save the scene to keep them.");
        }

        /// <summary>
        /// Replace the Build Settings scene list with the two real scenes, title screen first.
        ///
        /// Order is the whole point: a build boots into index 0, so MainMenu has to lead. This
        /// also drops the URP template's SampleScene from the list, which is currently the only
        /// entry — it is not deleted, just no longer shipped.
        /// </summary>
        [MenuItem("Kagemura/Setup/3. Register the Scenes in Build Settings")]
        public static void RegisterScenesInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();

            foreach (string path in new[] { MainMenuScenePath, GameScenePath })
            {
                if (File.Exists(path)) scenes.Add(new EditorBuildSettingsScene(path, true));
                else Debug.LogWarning($"[MenuSceneSetup] {path} does not exist yet, so it was not " +
                                      "registered. Run step 1 first.");
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log($"[MenuSceneSetup] Build Settings now lists {scenes.Count} scene(s), " +
                      "starting with the title screen.");
        }

        private static bool AddIfMissing<T>(string objectName) where T : MonoBehaviour
        {
            if (Object.FindFirstObjectByType<T>() != null) return false;

            var go = new GameObject(objectName, typeof(T));
            Undo.RegisterCreatedObjectUndo(go, $"Add {typeof(T).Name}");
            return true;
        }
    }
}
