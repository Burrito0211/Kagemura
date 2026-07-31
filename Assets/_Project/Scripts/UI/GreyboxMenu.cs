using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Kagemura.UI
{
    /// <summary>
    /// Build Order step 13: the shared body of every full-screen menu (spec §5) — main menu,
    /// pause, game over, win.
    ///
    /// All four are the same object: a dimmed screen, a title, and a column of buttons. Writing
    /// that four times would mean fixing every layout and pause bug four times, so the screens
    /// below supply a title and a list of buttons and inherit the rest.
    ///
    /// Greybox, like the HUD and the settings window: authored in code now, replaced at the art
    /// pass by building the screens in the scene.
    /// </summary>
    public abstract class GreyboxMenu : MonoBehaviour
    {
        [Header("Menu")]
        [Tooltip("Freeze the game while this screen is up.")]
        [SerializeField] protected bool pauseWhileOpen = true;
        [Tooltip("Show as soon as the scene starts. On for the main menu, off for the rest.")]
        [SerializeField] protected bool openOnStart;

        [Header("Layout (reference pixels, 1920x1080)")]
        [SerializeField] private float buttonWidth = 340f;
        [SerializeField] private float buttonHeight = 52f;
        [SerializeField] private float buttonSpacing = 14f;

        [Header("Colours")]
        [SerializeField] private Color scrimColor = new Color(0.02f, 0.02f, 0.05f, 0.85f);
        [SerializeField] private Color titleColor = new Color(0.85f, 0.19f, 0.16f);    // vermillion
        [SerializeField] private Color textColor = new Color(0.95f, 0.93f, 0.86f);
        [SerializeField] private Color buttonColor = new Color(0.13f, 0.15f, 0.24f);

        /// <summary>Label and handler for one button, supplied by the concrete screen.</summary>
        protected readonly struct MenuItem
        {
            public readonly string Label;
            public readonly UnityAction OnClick;

            public MenuItem(string label, UnityAction onClick)
            {
                Label = label;
                OnClick = onClick;
            }
        }

        private GameObject _root;
        private Text _titleLabel;
        private InputActionMap _playerMap;
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();

        private bool _isOpen;
        private float _previousTimeScale = 1f;

        public bool IsOpen => _isOpen;

        /// <summary>Heading, e.g. "PAUSED". Read once when the screen is built.</summary>
        protected abstract string Title { get; }

        /// <summary>Buttons top to bottom.</summary>
        protected abstract IEnumerable<MenuItem> BuildItems();

        protected virtual void Awake()
        {
            _playerMap = InputSystem.actions?.FindActionMap("Player", throwIfNotFound: false);

            EnsureEventSystem();
            Build();
            _root.SetActive(false);
        }

        protected virtual void Start()
        {
            if (openOnStart) Open();
        }

        protected virtual void OnDisable()
        {
            // Never leave the game frozen because this was switched off mid-menu.
            if (_isOpen) Close();
        }

        public virtual void Open()
        {
            if (_isOpen) return;
            _isOpen = true;

            _root.SetActive(true);
            SuspendPlayer();

            if (pauseWhileOpen)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
        }

        public virtual void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;

            _root.SetActive(false);
            RestorePlayer();

            if (pauseWhileOpen) Time.timeScale = _previousTimeScale;
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        /// <summary>
        /// Same contract the settings window and dialogue box use: put back exactly the actions
        /// that were live, rather than enabling the whole map and switching on actions nothing
        /// has asked for.
        /// </summary>
        private void SuspendPlayer()
        {
            _suspendedActions.Clear();
            if (_playerMap == null) return;

            foreach (var action in _playerMap.actions)
                if (action.enabled) _suspendedActions.Add(action);

            _playerMap.Disable();
        }

        private void RestorePlayer()
        {
            foreach (var action in _suspendedActions) action.Enable();
            _suspendedActions.Clear();
        }

        /// <summary>
        /// Time.timeScale is left alone here on purpose. A screen that loads a scene or quits is
        /// about to stop existing, and restoring the scale on the way out is what stops the next
        /// scene starting frozen — a bug that looks like a hang rather than a menu fault.
        /// </summary>
        protected void ReleasePause()
        {
            if (pauseWhileOpen) Time.timeScale = _previousTimeScale <= 0f ? 1f : _previousTimeScale;
            else Time.timeScale = 1f;
        }

        // --- Greybox construction -------------------------------------------------------

        private void Build()
        {
            var canvasGo = new GameObject($"{GetType().Name} Canvas (greybox)",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 250;          // above the settings window

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = canvasGo;

            // Full-screen scrim: dims the game, and its raycastTarget swallows clicks so nothing
            // behind the menu can be hit through it.
            var scrim = new GameObject("Scrim", typeof(Image));
            scrim.transform.SetParent(canvasGo.transform, false);
            var scrimRect = (RectTransform)scrim.transform;
            scrimRect.anchorMin = Vector2.zero;
            scrimRect.anchorMax = Vector2.one;
            scrimRect.offsetMin = scrimRect.offsetMax = Vector2.zero;
            scrim.GetComponent<Image>().color = scrimColor;

            var items = new List<MenuItem>(BuildItems());

            // Centred as one block, so adding or removing a button re-centres the column instead
            // of leaving the layout weighted to one side.
            float totalHeight = items.Count * buttonHeight + (items.Count - 1) * buttonSpacing;
            float y = totalHeight * 0.5f;

            _titleLabel = CreateLabel(canvasGo.transform, "Title", Title, 46, titleColor,
                new Vector2(0f, y + 90f), new Vector2(buttonWidth * 1.6f, 60f));

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                CreateButton(canvasGo.transform, item.Label, item.OnClick,
                    new Vector2(0f, y - i * (buttonHeight + buttonSpacing) - buttonHeight * 0.5f));
            }
        }

        /// <summary>Retitle after construction — the end screen shows a different word per outcome.</summary>
        protected void SetTitle(string title)
        {
            if (_titleLabel != null) _titleLabel.text = title;
        }

        private void CreateButton(Transform parent, string label, UnityAction onClick, Vector2 position)
        {
            var go = new GameObject(label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);
            rect.anchoredPosition = position;

            var image = go.GetComponent<Image>();
            image.color = buttonColor;

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);

            var colors = button.colors;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            CreateLabel(go.transform, "Label", label, 20, textColor, Vector2.zero,
                new Vector2(buttonWidth, buttonHeight));
        }

        private static Text CreateLabel(Transform parent, string labelName, string content,
                                        int fontSize, Color color, Vector2 position, Vector2 size)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return null;

            var go = new GameObject(labelName, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Buttons need something to route clicks through, and a gameplay scene may not have one.
        /// Made here rather than demanded of the scene, so dropping a menu into a bare scene works.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = go.GetComponent<InputSystemUIInputModule>();
            if (InputSystem.actions != null) module.actionsAsset = InputSystem.actions;
        }
    }
}
