using System.Collections.Generic;
using Kagemura.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kagemura.UI
{
    /// <summary>
    /// The controls pop-up: one row per thing the player character can do, each showing the key
    /// it's on, each clickable to bind it to something else. Opens on Escape, or from a menu
    /// button via <see cref="Open"/>.
    ///
    /// Two things happen the moment it opens, and they're separate on purpose:
    /// - The Player action map is disabled, always. The window is full of keys the player is
    ///   about to press, and none of them should also swing a sword.
    /// - Time is frozen, optionally. Rebinding mid-fight is a bad idea, but a menu that opens
    ///   this window has already paused, so the freeze is a field rather than a rule.
    ///
    /// Built from code the same way the HUD is (see <see cref="HUDController"/>) — greybox now,
    /// replaced wholesale at the art pass. Drop the component on an empty GameObject and it makes
    /// its own canvas, and its own EventSystem if the scene hasn't got one.
    /// </summary>
    public class SettingsWindow : MonoBehaviour
    {
        [Header("Open / Close")]
        [Tooltip("Control that opens and closes the window. Bound directly rather than through " +
                 "the action asset, so disabling the Player map can't lock the player out of it.")]
        [SerializeField] private string toggleControl = "<Keyboard>/escape";
        [Tooltip("Freeze the game while the window is open.")]
        [SerializeField] private bool pauseWhileOpen = true;
        [Tooltip("Open on start — useful while testing the window itself.")]
        [SerializeField] private bool openOnStart;

        [Header("Layout (reference pixels, 1920x1080)")]
        [SerializeField] private float panelWidth = 620f;
        [SerializeField] private float rowHeight = 38f;
        [SerializeField] private float keyButtonWidth = 190f;
        [SerializeField] private float padding = 28f;

        [Header("Colours")]
        [SerializeField] private Color scrimColor = new Color(0.02f, 0.02f, 0.05f, 0.72f);
        [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.93f, 0.86f);      // parchment
        [SerializeField] private Color accentColor = new Color(0.85f, 0.19f, 0.16f);    // vermillion
        [SerializeField] private Color keyColor = new Color(0.13f, 0.15f, 0.24f);
        [SerializeField] private Color keyListeningColor = new Color(0.85f, 0.19f, 0.16f);

        /// <summary>One rebindable action and the button showing its key.</summary>
        private sealed class Row
        {
            public InputBindings.Entry Entry;
            public Image KeyBacking;
            public Text KeyLabel;
        }

        private readonly List<Row> _rows = new List<Row>();

        /// <summary>
        /// Which Player actions were live when the window opened, so closing it puts back exactly
        /// that set. Calling Enable() on the whole map instead would quietly switch on every
        /// action in it — Look, Crouch, Interact — that nothing has asked for yet.
        /// </summary>
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();

        private GameObject _window;
        private Text _statusLabel;

        private InputAction _toggleAction;
        private InputActionMap _playerMap;
        private InputActionRebindingExtensions.RebindingOperation _rebind;

        private bool _isOpen;
        private float _previousTimeScale = 1f;
        private int _suppressToggleFrame = -1;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            _playerMap = InputSystem.actions?.FindActionMap(InputBindings.PlayerMapName,
                                                            throwIfNotFound: false);
            if (_playerMap == null)
                Debug.LogWarning("[SettingsWindow] No 'Player' action map found — the window will " +
                                 "still open, but it has nothing to rebind.", this);

            // Overrides are normally applied before the first scene loads. Re-applying here covers
            // the case where the asset wasn't ready that early (entering play mode from a fresh
            // domain reload), and is harmless when they're already in place.
            InputBindings.Load();

            EnsureEventSystem();
            BuildWindow();
            _window.SetActive(false);
        }

        private void OnEnable()
        {
            _toggleAction = new InputAction("ToggleSettings", InputActionType.Button, toggleControl);
            _toggleAction.AddBinding("<Gamepad>/start");
            _toggleAction.Enable();
        }

        private void Start()
        {
            if (openOnStart) Open();
        }

        private void OnDisable()
        {
            // Leaving play mode mid-rebind would otherwise strand a listening operation and a
            // frozen timeScale.
            CancelRebind();
            if (_isOpen) Close();

            _toggleAction?.Disable();
            _toggleAction?.Dispose();
            _toggleAction = null;
        }

        private void Update()
        {
            if (_toggleAction == null) return;

            // Escape is both "cancel this rebind" and "close the window". The Input System
            // resolves the cancel before Update runs, so without this the same press would do
            // both — cancel the rebind, then shut the window the player is still using.
            if (Time.frameCount == _suppressToggleFrame) return;

            if (_toggleAction.WasPressedThisFrame()) Toggle();
        }

        // --- Open / close ---------------------------------------------------------------

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            _window.SetActive(true);

            // Not just "so the player doesn't attack while rebinding" — an interactive rebind
            // refuses to start on an enabled action, so this is also what makes the window work.
            _suspendedActions.Clear();
            if (_playerMap != null)
            {
                foreach (var action in _playerMap.actions)
                    if (action.enabled) _suspendedActions.Add(action);

                _playerMap.Disable();
            }

            if (pauseWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }

            RefreshRows();
            SetStatus("Click a key to change it.");
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            CancelRebind();
            _window.SetActive(false);

            foreach (var action in _suspendedActions) action.Enable();
            _suspendedActions.Clear();

            if (pauseWhileOpen) Time.timeScale = _previousTimeScale;
        }

        // --- Rebinding ------------------------------------------------------------------

        private void BeginRebind(int rowIndex)
        {
            if (_rebind != null) return;                       // one at a time
            if (rowIndex < 0 || rowIndex >= _rows.Count) return;

            var row = _rows[rowIndex];
            var action = InputBindings.FindAction(row.Entry);
            int bindingIndex = InputBindings.BindingIndex(action, row.Entry);

            if (bindingIndex < 0)
            {
                SetStatus($"{row.Entry.DisplayName} has no keyboard binding to change.");
                return;
            }

            // Kept so a rejected rebind can go back to exactly what the player had before,
            // rather than to whatever the asset ships with.
            string previousOverride = action.bindings[bindingIndex].overridePath;

            if (row.KeyLabel != null) row.KeyLabel.text = "press a key…";
            row.KeyBacking.color = keyListeningColor;
            SetStatus("Press a key or a mouse button.   Esc cancels.");

            _rebind = action.PerformInteractiveRebinding(bindingIndex)
                // Pointer movement and the wheel would otherwise match the instant the mouse
                // twitches, binding the action to a nudge the player never meant as input.
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithCancelingThrough(toggleControl)
                // A brief wait after the first match: pressing a key actuates both the specific
                // control and broader ones like anyKey, and this lets the most specific win.
                .OnMatchWaitForAnother(0.05f)
                .OnCancel(_ => EndRebind(row, action, bindingIndex, previousOverride, cancelled: true))
                .OnComplete(_ => EndRebind(row, action, bindingIndex, previousOverride, cancelled: false))
                .Start();
        }

        private void EndRebind(Row row, InputAction action, int bindingIndex,
                               string previousOverride, bool cancelled)
        {
            DisposeRebind();

            if (cancelled)
            {
                SetStatus("Cancelled.");
            }
            else
            {
                // The new binding is already applied at this point, so the clash is checked
                // against reality and then undone if it's a real one. Rejecting rather than
                // swapping: a silent swap moves a second key the player never asked about.
                var conflict = InputBindings.FindConflict(row.Entry);
                if (conflict != null)
                {
                    string attempted = InputBindings.DisplayString(row.Entry);
                    InputBindings.RestoreOverride(action, bindingIndex, previousOverride);
                    SetStatus($"{attempted} is already used by {conflict.DisplayName}.");
                }
                else
                {
                    InputBindings.Save();
                    SetStatus($"{row.Entry.DisplayName} → {InputBindings.DisplayString(row.Entry)}");
                }
            }

            RefreshRows();
        }

        private void CancelRebind()
        {
            if (_rebind == null) return;
            _rebind.Cancel();      // fires OnCancel, which disposes and refreshes
            DisposeRebind();
        }

        private void DisposeRebind()
        {
            _rebind?.Dispose();
            _rebind = null;
            _suppressToggleFrame = Time.frameCount;
        }

        private void ResetToDefaults()
        {
            CancelRebind();
            InputBindings.ResetAll();
            RefreshRows();
            SetStatus("Controls reset to defaults.");
        }

        private void RefreshRows()
        {
            foreach (var row in _rows)
            {
                if (row.KeyLabel != null) row.KeyLabel.text = InputBindings.DisplayString(row.Entry);
                row.KeyBacking.color = keyColor;
            }
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        // --- Greybox construction -------------------------------------------------------
        // Placeholder only, same deal as the HUD: at the art pass this is authored in the scene
        // and none of it runs.

        private void BuildWindow()
        {
            var canvasGo = new GameObject("Settings Canvas (greybox)",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;                 // above the HUD's 100

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _window = canvasGo;

            var entries = InputBindings.Entries;

            const float titleHeight = 46f;
            const float statusHeight = 30f;
            const float footerHeight = 40f;
            float panelHeight = padding * 2f + titleHeight + entries.Count * rowHeight
                                + statusHeight + footerHeight;

            // Full-screen scrim. It dims the game, but its real job is raycastTarget: it swallows
            // clicks so a stray one can't reach anything behind the window.
            var scrim = new GameObject("Scrim", typeof(Image));
            scrim.transform.SetParent(canvasGo.transform, false);
            var scrimRect = (RectTransform)scrim.transform;
            scrimRect.anchorMin = Vector2.zero;
            scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = scrimRect.offsetMax = Vector2.zero;
            scrim.GetComponent<Image>().color = scrimColor;

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(canvasGo.transform, false);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            panelRect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = panelColor;

            float innerWidth = panelWidth - padding * 2f;
            float y = -padding;

            var title = CreateText(panelRect, "Title", "CONTROLS", 22, TextAnchor.MiddleLeft,
                new Vector2(padding, y), new Vector2(innerWidth, titleHeight));
            if (title != null) title.color = accentColor;
            y -= titleHeight;

            for (int i = 0; i < entries.Count; i++)
            {
                int rowIndex = i;   // captured per row, not per loop
                var entry = entries[i];

                CreateText(panelRect, $"{entry.DisplayName} Label", entry.DisplayName, 16,
                    TextAnchor.MiddleLeft, new Vector2(padding, y),
                    new Vector2(innerWidth - keyButtonWidth - 12f, rowHeight));

                var keyBacking = CreateButton(panelRect, $"{entry.DisplayName} Key",
                    new Vector2(padding + innerWidth - keyButtonWidth, y),
                    new Vector2(keyButtonWidth, rowHeight - 6f),
                    () => BeginRebind(rowIndex));

                _rows.Add(new Row
                {
                    Entry = entry,
                    KeyBacking = keyBacking,
                    KeyLabel = keyBacking.GetComponentInChildren<Text>()
                });

                y -= rowHeight;
            }

            _statusLabel = CreateText(panelRect, "Status", string.Empty, 13, TextAnchor.MiddleLeft,
                new Vector2(padding, y), new Vector2(innerWidth, statusHeight));
            if (_statusLabel != null) _statusLabel.color = new Color(0.7f, 0.7f, 0.75f);
            y -= statusHeight;

            const float footerButtonWidth = 150f;
            CreateButton(panelRect, "Reset", new Vector2(padding, y),
                new Vector2(footerButtonWidth, footerHeight - 6f), ResetToDefaults, "Reset Defaults");
            CreateButton(panelRect, "Close",
                new Vector2(padding + innerWidth - footerButtonWidth, y),
                new Vector2(footerButtonWidth, footerHeight - 6f), Close, "Close");
        }

        /// <summary>
        /// uGUI buttons need something to route clicks through, and a scene built for gameplay
        /// may not have one yet. Made here rather than demanded of the scene so dropping this
        /// component into a bare test scene is all it takes.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // Assigned explicitly: the module picks up the project-wide asset on its own in most
            // setups, but being handed it means the UI map is resolved however the project is set.
            var module = go.GetComponent<InputSystemUIInputModule>();
            if (InputSystem.actions != null) module.actionsAsset = InputSystem.actions;
        }

        private Image CreateButton(RectTransform parent, string buttonName, Vector2 offset,
                                   Vector2 size, UnityEngine.Events.UnityAction onClick,
                                   string label = null)
        {
            var go = new GameObject(buttonName, typeof(Image), typeof(Button));
            SetUpRect(go, parent, offset, size);

            var image = go.GetComponent<Image>();
            image.color = keyColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            // Tint on hover/press comes off the backing colour, so recolouring a listening row
            // doesn't need the ColorBlock rebuilt to match.
            var colors = button.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            var text = CreateText((RectTransform)go.transform, "Label", label ?? string.Empty, 15,
                TextAnchor.MiddleCenter, Vector2.zero, size);
            if (text != null) text.rectTransform.anchoredPosition = Vector2.zero;

            return image;
        }

        private Text CreateText(RectTransform parent, string textName, string content, int fontSize,
                                TextAnchor anchor, Vector2 offset, Vector2 size)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return null;   // no built-in font in this player: skip the text

            var go = new GameObject(textName, typeof(Text));
            SetUpRect(go, parent, offset, size);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = textColor;
            text.text = content;
            text.raycastTarget = false;      // clicks belong to the button underneath
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Lay a child out from the top-left of its parent, so every offset above reads as
        /// "down and in from the panel's corner" and the rows stack by subtracting from Y.
        /// </summary>
        private static RectTransform SetUpRect(GameObject go, Transform parent, Vector2 offset, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }
    }
}
