using System;
using System.Collections;
using UnityEngine;

namespace Kagamura.Systems
{
    /// <summary>
    /// Event-driven health, shared by the player, enemies, and the boss (spec §6).
    /// HUD, SFX, and game logic subscribe to the events instead of holding direct
    /// references to each other. For step 2 it doubles as the dummy enemy's health.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private int maxHealth = 100;

        [Header("Hit Feedback")]
        [Tooltip("Sprite flashes this color briefly when hit.")]
        [SerializeField] private Color flashColor = Color.red;
        [SerializeField] private float flashDuration = 0.08f;

        [Header("Invulnerability")]
        [Tooltip("Seconds of invulnerability granted automatically after taking a hit. " +
                 "Use a small value on the player (~0.4s) to stop contact damage draining " +
                 "health every frame; leave at 0 for enemies.")]
        [SerializeField] private float hitInvulnerability = 0f;

        [Header("Death")]
        [Tooltip("Destroy this GameObject when health reaches 0 (fine for dummy enemies).")]
        [SerializeField] private bool destroyOnDeath = true;

        private int _current;
        private SpriteRenderer _sprite;
        private Color _baseColor;
        private Rigidbody2D _rb;
        private Coroutine _flashRoutine;

        // Timed i-frames (post-hit) and held i-frames (dodge) are tracked separately so a
        // dodge ending never cancels post-hit invulnerability, and vice versa.
        private float _invulnerableUntil = -999f;
        private int _invulnerabilityHolds;

        // --- Events (current, max) / (damage info) / (death) ---
        public event Action<int, int> OnHealthChanged;
        public event Action<DamageInfo> OnDamaged;
        public event Action OnDied;

        /// <summary>Raised when a hit was ignored because the target was invulnerable —
        /// the hook for a "perfect dodge" spark/sfx during the polish pass.</summary>
        public event Action<DamageInfo> OnDamageAvoided;

        public int Current => _current;
        public int Max => maxHealth;
        public bool IsAlive => _current > 0;

        /// <summary>True while i-frames are active from either a dodge or a recent hit.</summary>
        public bool IsInvulnerable => _invulnerabilityHolds > 0 || Time.time < _invulnerableUntil;

        /// <summary>
        /// True only while an ability is holding i-frames open (dodge, dash-strike) — not for
        /// the automatic post-hit window. Lets Focus tell a real read from a free one.
        /// </summary>
        public bool IsInvulnerableFromAbility => _invulnerabilityHolds > 0;

        private void Awake()
        {
            _current = maxHealth;
            _rb = GetComponent<Rigidbody2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        private void Start() => OnHealthChanged?.Invoke(_current, maxHealth);

        /// <summary>
        /// Override the inspector value from a stats asset (EnemyData/WeaponData-style tuning).
        /// Call before Start so the first OnHealthChanged reports the real maximum.
        /// </summary>
        public void SetMaxHealth(int newMax, bool refill = true)
        {
            maxHealth = Mathf.Max(1, newMax);
            _current = refill ? maxHealth : Mathf.Min(_current, maxHealth);
            OnHealthChanged?.Invoke(_current, maxHealth);
        }

        /// <summary>
        /// Hold/release i-frames for as long as an ability lasts (dodge). Calls are counted,
        /// so overlapping sources each release their own hold without ending the others.
        /// </summary>
        public void SetInvulnerable(bool active)
        {
            _invulnerabilityHolds = Mathf.Max(0, _invulnerabilityHolds + (active ? 1 : -1));
        }

        /// <summary>Grant i-frames for a fixed duration (never shortens an existing window).</summary>
        public void GrantInvulnerability(float duration)
        {
            _invulnerableUntil = Mathf.Max(_invulnerableUntil, Time.time + duration);
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            if (IsInvulnerable)
            {
                OnDamageAvoided?.Invoke(info);
                return;
            }

            if (hitInvulnerability > 0f) GrantInvulnerability(hitInvulnerability);

            _current = Mathf.Max(0, _current - info.Amount);
            OnHealthChanged?.Invoke(_current, maxHealth);
            OnDamaged?.Invoke(info);

            // Knockback (only meaningful on a dynamic body).
            if (_rb != null && _rb.bodyType == RigidbodyType2D.Dynamic && info.KnockbackForce > 0f)
                _rb.linearVelocity = info.KnockbackDir * info.KnockbackForce;

            if (_sprite != null)
            {
                if (_flashRoutine != null) StopCoroutine(_flashRoutine);
                _flashRoutine = StartCoroutine(Flash());
            }

            if (_current <= 0) Die();
        }

        public void Heal(int amount)
        {
            if (!IsAlive) return;
            _current = Mathf.Min(maxHealth, _current + amount);
            OnHealthChanged?.Invoke(_current, maxHealth);
        }

        private void Die()
        {
            OnDied?.Invoke();
            if (destroyOnDeath) Destroy(gameObject);
        }

        private IEnumerator Flash()
        {
            _sprite.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            _sprite.color = _baseColor;
            _flashRoutine = null;
        }
    }
}
