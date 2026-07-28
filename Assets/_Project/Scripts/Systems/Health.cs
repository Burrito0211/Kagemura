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

        [Header("Death")]
        [Tooltip("Destroy this GameObject when health reaches 0 (fine for dummy enemies).")]
        [SerializeField] private bool destroyOnDeath = true;

        private int _current;
        private SpriteRenderer _sprite;
        private Color _baseColor;
        private Rigidbody2D _rb;
        private Coroutine _flashRoutine;

        // --- Events (current, max) / (damage info) / (death) ---
        public event Action<int, int> OnHealthChanged;
        public event Action<DamageInfo> OnDamaged;
        public event Action OnDied;

        public int Current => _current;
        public int Max => maxHealth;
        public bool IsAlive => _current > 0;

        private void Awake()
        {
            _current = maxHealth;
            _rb = GetComponent<Rigidbody2D>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        private void Start() => OnHealthChanged?.Invoke(_current, maxHealth);

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

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
