using Kagamura.Systems;
using UnityEngine;

namespace Kagamura.Player.Weapons
{
    /// <summary>
    /// The bow's arrow (spec §2.2). Travels in a straight line, damages the first thing it
    /// hits on the target layers, and dies on terrain or after its lifetime — flight is
    /// deliberately flat rather than arced, because a predictable line is easier to aim
    /// with in a 2D fight than a lobbed arc.
    ///
    /// Spawned from code by BowWeapon, so there's no prefab to keep in sync during greybox.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class ArrowProjectile : MonoBehaviour
    {
        private int _damage;
        private float _knockbackForce;
        private LayerMask _targetLayers;
        private LayerMask _blockingLayers;
        private GameObject _source;
        private Vector2 _direction = Vector2.right;

        /// <summary>
        /// Focus awarded to the shooter on a hit. Set by BowWeapon before Launch — the arrow
        /// resolves its own damage well away from the bow, so it has to credit the pool itself.
        /// </summary>
        public int FocusOnHit { get; set; }

        /// <summary>Set everything the arrow needs, then let it fly. Called right after spawn.</summary>
        public void Launch(Vector2 direction, float speed, int damage, float knockbackForce,
                           LayerMask targetLayers, LayerMask blockingLayers, float lifetime,
                           GameObject source)
        {
            _direction = direction.normalized;
            _damage = damage;
            _knockbackForce = knockbackForce;
            _targetLayers = targetLayers;
            _blockingLayers = blockingLayers;
            _source = source;

            var rb = GetComponent<Rigidbody2D>();
            rb.linearVelocity = _direction * speed;

            // Point the arrow along its flight so the greybox sprite reads as a direction.
            transform.right = _direction;

            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            int otherBit = 1 << other.gameObject.layer;

            if ((_blockingLayers.value & otherBit) != 0)
            {
                Destroy(gameObject);
                return;
            }

            if ((_targetLayers.value & otherBit) == 0) return;
            if (!other.TryGetComponent<IDamageable>(out var victim) || !victim.IsAlive) return;

            victim.TakeDamage(new DamageInfo
            {
                Amount = _damage,
                HitPoint = other.ClosestPoint(transform.position),
                // Knock along the arrow's flight, not away from the player — at bow range
                // those aren't the same direction.
                KnockbackDir = new Vector2(_direction.x, 0.2f).normalized,
                KnockbackForce = _knockbackForce,
                Source = _source
            });

            if (_source != null && _source.TryGetComponent<Focus>(out var focus))
                focus.Gain(FocusOnHit);

            // One target per arrow: piercing would undercut the positioning the bow is for.
            Destroy(gameObject);
        }
    }
}
