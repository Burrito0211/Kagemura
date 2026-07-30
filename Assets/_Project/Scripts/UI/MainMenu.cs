using System.Collections.Generic;
using UnityEngine;

namespace Kagamura.UI
{
    /// <summary>
    /// Title screen (spec §5): Start, Controls, Quit.
    ///
    /// Controls opens the same <see cref="SettingsWindow"/> the pause menu uses rather than a
    /// second options screen — rebinding is rebinding, and one of them is enough to maintain.
    /// </summary>
    public class MainMenu : GreyboxMenu
    {
        [Header("Routes")]
        [SerializeField] private string firstLevelScene = SceneRoutes.DefaultFirstLevel;

        [Header("Optional")]
        [Tooltip("Controls window. Found in the scene if left empty; the button hides without one.")]
        [SerializeField] private SettingsWindow settings;

        protected override string Title => "HAZAKURA";

        protected override void Awake()
        {
            // Resolved before the base builds the buttons, since whether Controls appears at all
            // depends on finding it.
            if (settings == null) settings = FindFirstObjectByType<SettingsWindow>();
            base.Awake();

            // The title screen is the one menu that is up from the first frame.
            openOnStart = true;
        }

        protected override IEnumerable<MenuItem> BuildItems()
        {
            yield return new MenuItem("Start", () => SceneRoutes.Load(firstLevelScene));

            if (settings != null)
                yield return new MenuItem("Controls", () => settings.Open());

            yield return new MenuItem("Quit", SceneRoutes.Quit);
        }
    }
}
