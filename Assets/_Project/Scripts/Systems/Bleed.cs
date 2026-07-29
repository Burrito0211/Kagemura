using System;
using UnityEngine;

namespace Kagamura.Systems
{
    /// <summary>
    /// Stacking damage-over-time, applied by the sickle (spec §2.2). Added to a victim on
    /// demand rather than sitting on every enemy prefab, so nothing pays for it until it bleeds.
    ///
    /// This is what stops the sickle being a reskin: its per-hit damage is poor, so the payoff
    /// only arrives if the player stays in range long enough to stack. In fiction the stacks are
    /// a curse-mark (spec §1.1), which is where the VFX hangs at the art pass — see
    /// OnStacksChanged.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Bleed : MonoBehaviour
    {
        private Health _health;
        private GameObject _source;

        private int _stacks;
        private int _damagePerStack;
        private float _tickInterval = 0.5f;
        private float _expiresAt;
        private float _nextTickAt;

        public int Stacks => _stacks;

        /// <summary>Fired whenever the stack count changes — the hook for the curse-mark VFX.</summary>
        public event Action<int> OnStacksChanged;

        /// <summary>
        /// Apply one stack to a target, adding the component if this is the first one.
        /// Every stack refreshes the whole duration, so sustained pressure keeps it burning.
        /// </summary>
        public static void ApplyStack(GameObject target, int damagePerStack, float tickInterval,
                                      float duration, int maxStacks, GameObject source)
        {
            if (target == null) return;
            if (!target.TryGetComponent<Health>(out var health) || !health.IsAlive) return;

            if (!target.TryGetComponent<Bleed>(out var bleed))
                bleed = target.AddComponent<Bleed>();

            bleed.AddStack(damagePerStack, tickInterval, duration, maxStacks, source);
        }

        private void Awake() => _health = GetComponent<Health>();

        private void AddStack(int damagePerStack, float tickInterval, float duration,
                              int maxStacks, GameObject source)
        {
            _damagePerStack = Mathf.Max(1, damagePerStack);
            _tickInterval = Mathf.Max(0.05f, tickInterval);
            _source = source;

            if (_stacks <= 0) _nextTickAt = Time.time + _tickInterval;

            int previous = _stacks;
            _stacks = Mathf.Min(_stacks + 1, Mathf.Max(1, maxStacks));
            _expiresAt = Time.time + duration;

            if (_stacks != previous) OnStacksChanged?.Invoke(_stacks);
        }

        private void Update()
        {
            if (_stacks <= 0) return;

            if (Time.time >= _nextTickAt)
            {
                Tick();
                // Fixed cadence from now, not from the tick — a stack landing mid-interval
                // shouldn't reset the clock and let the player dodge ticks by re-applying.
                _nextTickAt = Time.time + _tickInterval;
            }

            if (Time.time >= _expiresAt) Clear();
        }

        private void Tick()
        {
            if (!_health.IsAlive)
            {
                Clear();
                return;
            }

            // Reuses the normal damage path, so the hit flash and health events come for free.
            _health.TakeDamage(new DamageInfo
            {
                Amount = _damagePerStack * _stacks,
                HitPoint = transform.position,
                KnockbackDir = Vector2.zero,
                KnockbackForce = 0f,
                Source = _source,
                IgnoresStagger = true
            });
        }

        private void Clear()
        {
            if (_stacks == 0) return;
            _stacks = 0;
            // The component stays put rather than destroying itself — a second application in
            // the same frame would otherwise land on an already-doomed component and be lost.
            OnStacksChanged?.Invoke(0);
        }
    }
}
