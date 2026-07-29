using Kagamura.Systems;
using UnityEngine;
using UnityEngine.UI;

namespace Kagamura.UI
{
    /// <summary>
    /// A small health bar floating above whatever it's attached to — enemies now, the boss
    /// later (spec §2.5, where phase 2 needs a visible HP threshold).
    ///
    /// This exists so combat can be tuned by eye: the player bar is in a screen corner, this
    /// one sits on the enemy, and both are readable at once without pausing to check the
    /// inspector. Like the HUD it builds itself from code as greybox, and is replaced by real
    /// art later by assigning the Image fields.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("Height above the object's origin, in world units.")]
        [SerializeField] private float heightOffset = 1.1f;
        [Tooltip("Bar size in world units.")]
        [SerializeField] private Vector2 size = new Vector2(1.2f, 0.14f);

        [Header("Behaviour")]
        [Tooltip("Hide the bar while the target is at full health, so an untouched room stays clean.")]
        [SerializeField] private bool hideWhenFull = true;
        [Tooltip("Seconds the bar lingers after the last hit before hiding again.")]
        [SerializeField] private float hideDelay = 3f;

        [Header("Colours")]
        [SerializeField] private Color fillColor = new Color(0.85f, 0.19f, 0.16f);
        [SerializeField] private Color backingColor = new Color(0.05f, 0.06f, 0.12f, 0.85f);

        [Header("Bindings (leave empty for the greybox bar)")]
        [SerializeField] private Image fill;
        [SerializeField] private Transform barRoot;

        private Health _health;
        private float _hideAt;
        private float _baseScaleX = 1f;

        private void Awake()
        {
            _health = GetComponent<Health>();
            if (fill == null) BuildGreyboxBar();
            if (barRoot != null) _baseScaleX = Mathf.Abs(barRoot.localScale.x);
        }

        private void OnEnable()
        {
            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnDied += HandleDied;
        }

        private void OnDisable()
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnDied -= HandleDied;
        }

        private void Start() => HandleHealthChanged(_health.Current, _health.Max);

        private void LateUpdate()
        {
            if (barRoot == null) return;

            // Enemies flip by negating localScale.x to face the player (EnemyBase.FaceTarget),
            // which would mirror the bar and fill it from the wrong end. Cancel that out.
            float parentSign = Mathf.Sign(transform.localScale.x);
            var scale = barRoot.localScale;
            scale.x = _baseScaleX * parentSign;
            barRoot.localScale = scale;

            if (hideWhenFull && barRoot.gameObject.activeSelf && Time.time >= _hideAt
                && _health.Current >= _health.Max)
                barRoot.gameObject.SetActive(false);
        }

        private void HandleHealthChanged(int current, int max)
        {
            if (fill != null) fill.fillAmount = max > 0 ? Mathf.Clamp01((float)current / max) : 0f;
            if (barRoot == null) return;

            bool damaged = current < max;
            if (damaged) _hideAt = Time.time + hideDelay;

            barRoot.gameObject.SetActive(!hideWhenFull || damaged);
        }

        private void HandleDied()
        {
            if (barRoot != null) barRoot.gameObject.SetActive(false);
        }

        // --- Greybox construction -------------------------------------------------------

        private const float PixelsPerUnit = 100f;

        private void BuildGreyboxBar()
        {
            var canvasGo = new GameObject("Health Bar (greybox)", typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            // Authored at 100px per unit and scaled down, so sizes stay in sane UI numbers.
            canvasGo.transform.localScale = Vector3.one / PixelsPerUnit;

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;   // above the sprites, below the screen HUD

            var canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = size * PixelsPerUnit;

            CreateImage(canvasGo.transform, "Backing", backingColor, false);
            fill = CreateImage(canvasGo.transform, "Fill", fillColor, true);

            barRoot = canvasGo.transform;
            _baseScaleX = 1f / PixelsPerUnit;
        }

        private Image CreateImage(Transform parent, string imageName, Color color, bool filled)
        {
            var go = new GameObject(imageName, typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

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
    }
}
