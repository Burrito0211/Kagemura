using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagemura.UI
{
    /// <summary>
    /// Pause screen (spec §5): Resume, Controls, Restart, Quit to menu.
    ///
    /// Escape is shared with the settings window, so this deliberately stands down while that is
    /// open: pressing Escape inside Controls should close Controls, not stack a pause menu on top
    /// of it. One key, one meaning at a time.
    ///
    /// It also refuses to open over a story beat, which pauses on its own — two things fighting
    /// over timeScale is how a game ends up unpausable.
    /// </summary>
    public class PauseMenu : GreyboxMenu
    {
        [Header("Pause")]
        [Tooltip("Control that opens and closes the pause menu.")]
        [SerializeField] private string toggleControl = "<Keyboard>/escape";

        [Header("Routes")]
        [SerializeField] private string mainMenuScene = SceneRoutes.DefaultMainMenu;

        [Header("Optional")]
        [Tooltip("Controls window. Found in the scene if left empty; the button hides without one.")]
        [SerializeField] private SettingsWindow settings;

        private InputAction _toggleAction;

        protected override string Title => "PAUSED";

        protected override void Awake()
        {
            if (settings == null) settings = FindFirstObjectByType<SettingsWindow>();
            base.Awake();
        }

        private void OnEnable()
        {
            // Bound directly, not through the action asset: the Player map is suspended while
            // this is up, so an action living in it could never close the menu again.
            _toggleAction = new InputAction("TogglePause", InputActionType.Button, toggleControl);
            _toggleAction.AddBinding("<Gamepad>/start");
            _toggleAction.Enable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            _toggleAction?.Disable();
            _toggleAction?.Dispose();
            _toggleAction = null;
        }

        private void Update()
        {
            if (_toggleAction == null || !_toggleAction.WasPressedThisFrame()) return;

            // Stand down while Controls owns the key.
            if (settings != null && settings.IsOpen) return;

            // And while a story beat owns the pause.
            var dialogue = Narrative.DialogueUI.Instance;
            if (!IsOpen && dialogue != null && dialogue.IsPlaying) return;

            Toggle();
        }

        protected override IEnumerable<MenuItem> BuildItems()
        {
            yield return new MenuItem("Resume", Close);

            if (settings != null)
                yield return new MenuItem("Controls", () => { settings.Open(); Close();});

            yield return new MenuItem("Restart", () => { ReleasePause(); SceneRoutes.Reload(); });
            yield return new MenuItem("Quit to Menu",
                () => { ReleasePause(); SceneRoutes.Load(mainMenuScene); });
        }
    }
}
