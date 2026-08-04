using UnityEngine;

namespace Kagemura.Player.Weapons
{
    /// <summary>
    /// The long-range option (spec §2.2): slow to fire, medium-high damage, and unspammable.
    /// Build Order step 6.
    ///
    /// The limiter is the draw, not a resource. Hold to draw, release to fire; damage and
    /// arrow speed both scale with how long the string was held. That keeps the bow
    /// self-contained — no meter to build — and leaves the shared-Focus question (spec §9)
    /// open until the specials need an answer at step 7. A resource cost can be layered on
    /// top later without reworking the weapon.
    ///
    /// The cost the player actually feels is time: drawing slows them to a fraction of top
    /// speed, so committing to a full-power shot with a rusher closing is a real decision,
    /// and rolling out of it drops the shot entirely.
    /// </summary>
    public class BowWeapon : WeaponBase
    {
        [Header("Arrow")]
        [Tooltip("Layers that stop an arrow dead. Set to your Ground layer.")]
        [SerializeField] private LayerMask blockingLayers;
        [Tooltip("Optional arrow prefab. Left empty, a greybox arrow is built from code.")]
        [SerializeField] private ArrowProjectile arrowPrefab;
        [Tooltip("Greybox arrow size in world units.")]
        [SerializeField] private Vector2 greyboxArrowSize = new Vector2(0.5f, 0.08f);

        [Header("Draw Tell")]
        [Tooltip("Sprite tint while at full draw — the read that the shot is ready.")]
        [SerializeField] private Color fullDrawTint = new Color(0.75f, 0.9f, 1f);

        private PlayerController _player;
        private SpriteRenderer _sprite;
        private Color _baseColor = Color.white;

        private bool _drawing;
        private float _drawStartTime;
        private bool _tinted;

        /// <summary>
        /// Seconds of draw for a full-power shot, after the seasonal edge (spec §2.6).
        ///
        /// Summer spends its edge here rather than on damage. The bow already hits hardest, so
        /// more damage would just make it the answer to everything; a shorter draw instead buys
        /// back the thing the bow actually pays — time stood still while something closes on you.
        /// Summer is the level with archers on perches, and that is the season it matters in.
        /// </summary>
        private float FullDrawTime => Mathf.Max(0.01f, data.fullDrawTime / Edge);

        /// <summary>0..1 draw progress. Read by the HUD or a charge VFX later.</summary>
        public float DrawProgress => _drawing && data != null
            ? Mathf.Clamp01((Time.time - _drawStartTime) / FullDrawTime)
            : 0f;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        private void Update()
        {
            if (!_drawing) return;

            // Tint once at full draw rather than lerping — a hard switch is a clearer tell.
            bool full = DrawProgress >= 1f;
            if (full != _tinted) SetTint(full);
        }

        /// <summary>Pressing doesn't fire — it starts the draw.</summary>
        protected override void DoAttack(int facing)
        {
            if (data == null) return;

            _drawing = true;
            _drawStartTime = Time.time;
            _player?.SetMoveSpeedMultiplier(data.drawMoveSpeedMultiplier);
        }

        public override void ReleaseAttack(int facing)
        {
            if (!_drawing || data == null) return;

            float charge = DrawProgress;
            EndDraw();

            // The cooldown runs from the shot, not from when the draw began, so holding a
            // full draw isn't secretly also serving the cooldown.
            _lastAttackTime = Time.time;

            int damage = Mathf.RoundToInt(
                data.damage * Mathf.Lerp(data.minDrawDamageMultiplier, 1f, charge));
            float speed = Mathf.Lerp(data.minProjectileSpeed, data.maxProjectileSpeed, charge);

            SpawnArrow(facing, speed, damage);
        }

        public override void CancelAttack()
        {
            if (!_drawing) return;
            EndDraw();
            // No cooldown penalty: losing the shot to a dodge is punishment enough.
        }

        private void EndDraw()
        {
            _drawing = false;
            SetTint(false);
            _player?.SetMoveSpeedMultiplier(1f);
        }

        private void SetTint(bool full)
        {
            _tinted = full;
            if (_sprite != null) _sprite.color = full ? fullDrawTint : _baseColor;
        }

        private void SpawnArrow(int facing, float speed, int damage)
        {
            Vector2 direction = new Vector2(facing, 0f);
            Vector3 spawn = transform.position
                            + new Vector3(data.hitboxOffset.x * facing, data.hitboxOffset.y, 0f);

            ArrowProjectile arrow = arrowPrefab != null
                ? Instantiate(arrowPrefab, spawn, Quaternion.identity)
                : BuildGreyboxArrow(spawn);

            arrow.FocusOnHit = data.focusPerHit;
            arrow.Launch(direction, speed, damage, data.knockbackForce, targetLayers,
                         blockingLayers, data.projectileLifetime, gameObject);
        }

        // --- Greybox construction -------------------------------------------------------
        // Placeholder only. Assign an arrow prefab and none of this runs.

        private ArrowProjectile BuildGreyboxArrow(Vector3 position)
        {
            var go = new GameObject("Arrow (greybox)");
            go.transform.position = position;
            // A 1-unit square sprite scaled to the arrow's proportions — the collider below
            // is 1x1 for the same reason, so both stay in step with greyboxArrowSize.
            go.transform.localScale = new Vector3(greyboxArrowSize.x, greyboxArrowSize.y, 1f);

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = Kagemura.Systems.GreyboxArt.WhiteSprite();
            sprite.color = new Color(0.95f, 0.9f, 0.78f);
            sprite.sortingOrder = 10;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;               // flat flight, not an arc
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;             // scaled to size by the transform above
            col.isTrigger = true;

            return go.AddComponent<ArrowProjectile>();
        }

    }
}
