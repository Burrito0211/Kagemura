using System;
using System.Collections.Generic;
using Kagamura.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kagamura.Narrative
{
    /// <summary>
    /// Build Order step 12: the text box story beats are delivered through (spec §3.2).
    ///
    /// One of these in the scene, found by triggers through <see cref="Instance"/>. Built from
    /// code as greybox, the same as the HUD and the settings window, and replaced wholesale at
    /// the art pass by authoring the box in the scene and assigning the same fields.
    ///
    /// While a beat plays it suspends the Player action map, exactly as the settings window does
    /// — a story box the player can swing a sword through undercuts the pause it is asking for.
    /// Advance is bound directly rather than through the action asset for the same reason the
    /// settings toggle is: the map it would live in is the one being suspended.
    ///
    /// The typewriter reveal is skippable on the first press, which is the whole reason it is
    /// safe to have: a reader who is faster than it never has to wait for it.
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        [Header("Advance")]
        [Tooltip("Controls that advance or skip. Bound directly, since the Player map is " +
                 "suspended while a beat plays.")]
        [SerializeField] private string[] advanceControls =
        {
            "<Keyboard>/space", "<Keyboard>/enter", "<Mouse>/leftButton", "<Gamepad>/buttonSouth"
        };

        [Header("Layout (reference pixels, 1920x1080)")]
        [SerializeField] private float boxHeight = 190f;
        [SerializeField] private float sideMargin = 140f;
        [SerializeField] private float bottomMargin = 60f;
        [SerializeField] private float padding = 24f;

        [Header("Colours")]
        [SerializeField] private Color boxColor = new Color(0.05f, 0.06f, 0.12f, 0.94f);
        [SerializeField] private Color textColor = new Color(0.95f, 0.93f, 0.86f);
        [SerializeField] private Color speakerColor = new Color(0.85f, 0.19f, 0.16f);   // vermillion
        [SerializeField] private Color promptColor = new Color(0.6f, 0.6f, 0.66f);

        /// <summary>The one in the scene. Triggers reach it through this rather than searching.</summary>
        public static DialogueUI Instance { get; private set; }

        /// <summary>Raised when a beat finishes, so a trigger can open a door or start a fight.</summary>
        public event Action OnDialogueFinished;

        public bool IsPlaying { get; private set; }

        private GameObject _box;
        private Text _speakerLabel;
        private Text _bodyLabel;
        private Text _promptLabel;

        private InputAction _advanceAction;
        private InputActionMap _playerMap;
        private readonly List<InputAction> _suspendedActions = new List<InputAction>();

        private DialogueData _data;
        private int _lineIndex;
        private float _revealed;
        private bool _paused;
        private float _previousTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[{name}] A second DialogueUI exists; this one will sit idle. " +
                                 "Keep exactly one in the scene.", this);
                enabled = false;
                return;
            }
            Instance = this;

            _playerMap = InputSystem.actions?.FindActionMap("Player", throwIfNotFound: false);

            BuildBox();
            _box.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            _advanceAction = new InputAction("AdvanceDialogue", InputActionType.Button);
            foreach (var control in advanceControls)
                if (!string.IsNullOrEmpty(control)) _advanceAction.AddBinding(control);
            _advanceAction.Enable();
        }

        private void OnDisable()
        {
            // Never leave the game frozen or the player deaf because this was switched off.
            if (IsPlaying) Finish();

            _advanceAction?.Disable();
            _advanceAction?.Dispose();
            _advanceAction = null;
        }

        /// <summary>Play a beat. Ignored if one is already running, so two triggers can't overlap.</summary>
        public void Play(DialogueData data)
        {
            if (IsPlaying || data == null || data.lines == null || data.lines.Length == 0) return;

            _data = data;
            _lineIndex = 0;
            IsPlaying = true;

            _box.SetActive(true);
            SuspendPlayer();

            if (data.pauseGame)
            {
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _paused = true;
            }

            ShowLine();
        }

        private void Update()
        {
            if (!IsPlaying || _advanceAction == null) return;

            // Unscaled throughout: a beat that pauses the game would otherwise never reveal a
            // character, since its own pause stops the clock it is counting on.
            var line = _data.lines[_lineIndex];
            int total = line.text != null ? line.text.Length : 0;

            if (_revealed < total)
            {
                _revealed += _data.charactersPerSecond <= 0f
                    ? total
                    : _data.charactersPerSecond * Time.unscaledDeltaTime;

                _bodyLabel.text = line.text.Substring(0, Mathf.Min(total, Mathf.FloorToInt(_revealed)));
            }

            bool complete = _revealed >= total;
            _promptLabel.text = complete ? "▸" : string.Empty;

            if (!_advanceAction.WasPressedThisFrame()) return;

            // First press completes the line rather than skipping it, so an accidental double
            // press can never swallow a line unread.
            if (!complete)
            {
                _revealed = total;
                _bodyLabel.text = line.text;
                return;
            }

            _lineIndex++;
            if (_lineIndex >= _data.lines.Length) Finish();
            else ShowLine();
        }

        private void ShowLine()
        {
            var line = _data.lines[_lineIndex];
            _revealed = 0f;

            _speakerLabel.text = line.speaker ?? string.Empty;
            _bodyLabel.text = string.Empty;
            _promptLabel.text = string.Empty;
        }

        private void Finish()
        {
            IsPlaying = false;
            _box.SetActive(false);

            if (_paused)
            {
                Time.timeScale = _previousTimeScale;
                _paused = false;
            }

            RestorePlayer();
            OnDialogueFinished?.Invoke();
        }

        /// <summary>
        /// Same approach the settings window takes: remember exactly which actions were live and
        /// put back that set, rather than enabling the whole map and quietly switching on actions
        /// nothing has asked for.
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

        // --- Greybox construction -------------------------------------------------------

        private void BuildBox()
        {
            var canvasGo = new GameObject("Dialogue Canvas (greybox)",
                typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;          // over the HUD, under the settings window

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _box = new GameObject("Dialogue Box", typeof(Image));
            _box.transform.SetParent(canvasGo.transform, false);

            // Anchored across the bottom so the box stretches with the screen instead of being
            // sized once for one resolution.
            var rect = (RectTransform)_box.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(sideMargin, bottomMargin);
            rect.offsetMax = new Vector2(-sideMargin, bottomMargin + boxHeight);

            _box.GetComponent<Image>().color = boxColor;

            _speakerLabel = CreateText(rect, "Speaker", 20, TextAnchor.UpperLeft, speakerColor,
                new Vector2(padding, -padding), new Vector2(-padding * 2f, 26f),
                new Vector2(0f, 1f), new Vector2(1f, 1f));

            _bodyLabel = CreateText(rect, "Body", 18, TextAnchor.UpperLeft, textColor,
                new Vector2(padding, -(padding + 32f)), new Vector2(-padding * 2f, -(padding * 2f + 32f)),
                new Vector2(0f, 0f), new Vector2(1f, 1f));

            _promptLabel = CreateText(rect, "Prompt", 20, TextAnchor.LowerRight, promptColor,
                new Vector2(-padding - 14f, padding), new Vector2(20f, 24f),
                new Vector2(1f, 0f), new Vector2(1f, 0f));
        }

        /// <summary>
        /// Anchored rather than absolutely placed, so every label tracks the box as it stretches.
        /// The two anchor arguments are what decide whether a label is pinned to a corner or
        /// stretched along an edge.
        /// </summary>
        private static Text CreateText(RectTransform parent, string textName, int fontSize,
                                       TextAnchor alignment, Color color,
                                       Vector2 offsetMin, Vector2 offsetMax,
                                       Vector2 anchorMin, Vector2 anchorMax)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return null;

            var go = new GameObject(textName, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;

            // Stretched axes take offsets as insets; pinned axes take them as position/size.
            if (Mathf.Approximately(anchorMin.x, anchorMax.x) || Mathf.Approximately(anchorMin.y, anchorMax.y))
            {
                rect.pivot = anchorMin;
                rect.anchoredPosition = offsetMin;
                rect.sizeDelta = offsetMax;
            }
            else
            {
                rect.offsetMin = new Vector2(offsetMin.x, offsetMax.y);
                rect.offsetMax = new Vector2(offsetMax.x, offsetMin.y);
            }

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
