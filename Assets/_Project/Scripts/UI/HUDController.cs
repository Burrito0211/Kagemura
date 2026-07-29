using Kagamura.Player;
using Kagamura.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Kagamura.UI
{
    /// <summary>
    /// Build Order step 4: the HUD half. Listens to the player's Health events (spec §6 —
    /// event-driven, so this holds no reference the gameplay code knows about) and draws
    /// health plus the current weapon.
    ///
    /// The bar is two layers: the fill snaps to the real value, and a trail behind it drains
    /// down a moment later, so a big hit reads as a chunk lost rather than a number changing.
    ///
    /// Leave the bindings empty and this builds a greybox canvas for itself at runtime —
    /// enough to tune combat against now, and replaced wholesale at the art pass by assigning
    /// real Images to the same fields. No Focus meter yet: the resource system is Build Order
    /// step 7, and whether it's one shared pool is still open (spec §9).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

        [Header("Placement (greybox HUD only)")]
        [Tooltip("Which corner the HUD sits in.")]
        [SerializeField] private ScreenCorner corner = ScreenCorner.TopLeft;
        [Tooltip("Distance in from that corner, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 screenOffset = new Vector2(28f, 28f);
        [Tooltip("Health bar size in reference pixels.")]
        [SerializeField] private Vector2 barSize = new Vector2(360f, 24f);

        [Header("Source")]
        [Tooltip("Player health. Found automatically from the PlayerController if left empty.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("Player combat, for the weapon readout. Found automatically if left empty.")]
        [SerializeField] private PlayerCombat playerCombat;

        [Header("Bindings (leave empty for the greybox HUD)")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Image healthTrail;
        [SerializeField] private Text healthLabel;
        [SerializeField] private Text weaponLabel;

        [Header("Feel")]
        [Tooltip("Pause before the trail starts draining, so the lost chunk is readable.")]
        [SerializeField] private float trailDelay = 0.25f;
        [Tooltip("How fast the trail catches up, in fractions of the full bar per second.")]
        [SerializeField] private float trailSpeed = 0.7f;
        [Tooltip("Fraction of health below which the bar switches to the danger colour.")]
        [Range(0f, 1f)][SerializeField] private float lowHealthThreshold = 0.3f;
        [Tooltip("How long the bar flashes when a hit is shrugged off by i-frames.")]
        [SerializeField] private float avoidFlashDuration = 0.12f;

        [Header("Colours")]
        [SerializeField] private Color healthColor = new Color(0.85f, 0.19f, 0.16f);   // vermillion
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.45f, 0.1f);
        [SerializeField] private Color trailColor = new Color(0.95f, 0.9f, 0.78f, 0.7f); // parchment
        [SerializeField] private Color avoidFlashColor = new Color(0.6f, 0.85f, 1f);
        [SerializeField] private Color backingColor = new Color(0.05f, 0.06f, 0.12f, 0.85f);

        private float _fill = 1f;
        private float _trail = 1f;
        private float _trailHoldUntil;
        private float _flashUntil;
        private bool _dead;

        private void Awake()
        {
            if (playerHealth == null)
            {
                // By type rather than by the "Player" tag — the greybox player isn't tagged,
                // and this keeps the HUD working in a bare test scene.
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    playerHealth = player.GetComponent<Health>();
                    if (playerCombat == null) playerCombat = player.GetComponent<PlayerCombat>();
                }
            }

            if (playerHealth == null)
                Debug.LogWarning("[HUDController] No player Health found — the HUD will sit idle.", this);

            if (healthFill == null) BuildGreyboxHud();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += HandleHealthChanged;
                playerHealth.OnDamageAvoided += HandleDamageAvoided;
                playerHealth.OnDied += HandleDied;
            }
            if (playerCombat != null) playerCombat.OnWeaponChanged += HandleWeaponChanged;
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= HandleHealthChanged;
                playerHealth.OnDamageAvoided -= HandleDamageAvoided;
                playerHealth.OnDied -= HandleDied;
            }
            if (playerCombat != null) playerCombat.OnWeaponChanged -= HandleWeaponChanged;
        }

        private void Start()
        {
            // Health fires its opening OnHealthChanged from Start, so subscribing in OnEnable
            // usually catches it — but push once here too in case the HUD was enabled late.
            if (playerHealth != null) HandleHealthChanged(playerHealth.Current, playerHealth.Max);
            if (playerCombat != null) HandleWeaponChanged(playerCombat.CurrentWeapon);
            _trail = _fill;
            ApplyFill();
        }

        private void Update()
        {
            if (healthFill == null) return;

            if (healthTrail != null && !Mathf.Approximately(_trail, _fill))
            {
                if (_trail < _fill) _trail = _fill;                       // healing: no lag upward
                else if (Time.time >= _trailHoldUntil)
                    _trail = Mathf.MoveTowards(_trail, _fill, trailSpeed * Time.deltaTime);

                healthTrail.fillAmount = _trail;
            }

            healthFill.color = ResolveFillColor();
        }

        private Color ResolveFillColor()
        {
            if (Time.time < _flashUntil) return avoidFlashColor;
            if (_dead) return Color.Lerp(healthColor, Color.black, 0.6f);
            return _fill <= lowHealthThreshold ? lowHealthColor : healthColor;
        }

        private void HandleHealthChanged(int current, int max)
        {
            float next = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (next < _fill) _trailHoldUntil = Time.time + trailDelay;
            _fill = next;
            _dead = current <= 0;

            ApplyFill();
            if (healthLabel != null) healthLabel.text = $"{current} / {max}";
        }

        /// <summary>The i-frame payoff: a dodged hit should still register on screen (spec §2.3).</summary>
        private void HandleDamageAvoided(DamageInfo info) => _flashUntil = Time.time + avoidFlashDuration;

        private void HandleDied() => _dead = true;

        private void HandleWeaponChanged(WeaponBase weapon)
        {
            if (weaponLabel == null) return;
            weaponLabel.text = weapon != null && weapon.Data != null ? weapon.Data.displayName : "—";
        }

        private void ApplyFill()
        {
            if (healthFill != null) healthFill.fillAmount = _fill;
            if (healthTrail != null && _trail < _fill) healthTrail.fillAmount = _trail = _fill;
        }

        // --- Greybox construction -------------------------------------------------------
        // Placeholder only. At the art pass, author the HUD in the scene, drag the Images
        // into the fields above, and none of this runs.

        private RectTransform _root;

        private void BuildGreyboxHud()
        {
            var canvasGo = new GameObject("HUD Canvas (greybox)", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Everything hangs off one root, so moving the HUD is a single transform change.
            var rootGo = new GameObject("HUD Root", typeof(RectTransform));
            rootGo.transform.SetParent(canvasGo.transform, false);
            _root = (RectTransform)rootGo.transform;

            // Draw order is sibling order: backing, then the trail, then the fill on top.
            CreateBar(_root, "Health Backing", backingColor, false, Vector2.zero);
            healthTrail = CreateBar(_root, "Health Trail", trailColor, true, Vector2.zero);
            healthFill = CreateBar(_root, "Health Fill", healthColor, true, Vector2.zero);

            healthLabel = CreateLabel(_root, "Health Label", 14, TextAnchor.MiddleRight,
                new Vector2(-8f, 0f), new Vector2(barSize.x, barSize.y));
            weaponLabel = CreateLabel(_root, "Weapon Label", 16, TextAnchor.MiddleLeft,
                new Vector2(0f, -(barSize.y + 6f)), new Vector2(barSize.x, 22f));

            ApplyLayout();
        }

        /// <summary>
        /// Park the HUD in its chosen corner. Split out from the build so it can be re-run
        /// whenever the inspector values change — including while the game is playing, which
        /// is the point: find a spot where the player bar and the enemy bars are both readable
        /// without pausing.
        /// </summary>
        public void ApplyLayout()
        {
            if (_root == null) return;

            // Anchor and pivot both sit in the chosen corner, so the offset always reads as
            // "distance in from that corner" whichever one is picked.
            Vector2 anchor = corner switch
            {
                ScreenCorner.TopRight => new Vector2(1f, 1f),
                ScreenCorner.BottomLeft => new Vector2(0f, 0f),
                ScreenCorner.BottomRight => new Vector2(1f, 0f),
                _ => new Vector2(0f, 1f)
            };

            _root.anchorMin = _root.anchorMax = _root.pivot = anchor;
            _root.sizeDelta = new Vector2(barSize.x, barSize.y + 28f);
            _root.anchoredPosition = new Vector2(
                anchor.x > 0.5f ? -screenOffset.x : screenOffset.x,
                anchor.y > 0.5f ? -screenOffset.y : screenOffset.y);

            foreach (var bar in _root.GetComponentsInChildren<Image>(true))
                ((RectTransform)bar.transform).sizeDelta = barSize;
        }

#if UNITY_EDITOR
        // Drag the values in the inspector during play and the HUD moves with them.
        private void OnValidate()
        {
            if (Application.isPlaying) ApplyLayout();
        }
#endif

        private Image CreateBar(Transform parent, string barName, Color color, bool filled, Vector2 offset)
        {
            var go = new GameObject(barName, typeof(Image));
            SetUpRect(go, parent, offset, barSize);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (filled)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)Image.OriginHorizontal.Left;
                img.fillAmount = 1f;
            }

            return img;
        }

        private Text CreateLabel(Transform parent, string labelName, int fontSize, TextAnchor anchor,
                                 Vector2 offset, Vector2 size)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) return null;   // no built-in font in this player: skip the text

            var go = new GameObject(labelName, typeof(Text));
            SetUpRect(go, parent, offset, size);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = new Color(0.95f, 0.93f, 0.86f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>
        /// Lay a child out from the top-left of the HUD root. The root owns screen placement,
        /// so these offsets stay the same whichever corner the HUD is parked in.
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
