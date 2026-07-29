using Kagamura.Systems;
using UnityEngine;

namespace Kagamura.Enemies
{
    /// <summary>
    /// Shared plumbing for every enemy (spec §6): target tracking, facing, stagger on hit,
    /// and the attack hit query. Subclasses supply the behaviour pattern that actually
    /// differentiates the type — rusher, ranged, shielded (spec §2.5).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Tooltip("Stats asset for this enemy (Create > Kagamura > Enemy Data).")]
        [SerializeField] protected EnemyData data;

        [Tooltip("Which layers this enemy's attacks can hit. Set to your Player layer.")]
        [SerializeField] protected LayerMask targetLayers;

        [Tooltip("Optional. Left empty, the player is found by the 'Player' tag at startup.")]
        [SerializeField] protected Transform target;

        protected Rigidbody2D _rb;
        protected Health _health;
        protected SpriteRenderer _sprite;
        protected Color _baseColor = Color.white;

        protected int _facing = 1;
        protected float _staggerUntil;

        protected bool HasTarget => target != null;
        protected bool IsStaggered => Time.time < _staggerUntil;

        /// <summary>Signed horizontal distance to the target (+ = target is to the right).</summary>
        protected float SignedDistanceToTarget => HasTarget ? target.position.x - transform.position.x : 0f;
        protected float DistanceToTarget => HasTarget
            ? Vector2.Distance(target.position, transform.position)
            : float.MaxValue;

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;

            _rb.freezeRotation = true;

            if (data == null)
            {
                Debug.LogError($"[{name}] No EnemyData assigned — the enemy will not act.", this);
                return;
            }

            // The stats asset is the single source of truth, so a seasonal reskin only means
            // swapping the EnemyData — no per-prefab health edits.
            _health.SetMaxHealth(data.maxHealth);
        }

        protected virtual void OnEnable() => _health.OnDamaged += HandleDamaged;
        protected virtual void OnDisable() => _health.OnDamaged -= HandleDamaged;

        protected virtual void Start()
        {
            if (target != null) return;

            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else Debug.LogWarning($"[{name}] No target set and no GameObject tagged 'Player' found.", this);
        }

        /// <summary>Getting hit interrupts whatever the enemy was doing, so trades feel fair.</summary>
        protected virtual void HandleDamaged(DamageInfo info)
        {
            if (data == null || info.IgnoresStagger) return;
            _staggerUntil = Time.time + data.hitStagger;
        }

        /// <summary>Point the sprite at the target. Skipped mid-attack so a committed swing can be dodged past.</summary>
        protected void FaceTarget()
        {
            if (!HasTarget) return;

            float dx = SignedDistanceToTarget;
            if (Mathf.Abs(dx) < 0.05f) return;

            int dir = dx > 0f ? 1 : -1;
            if (dir == _facing) return;

            _facing = dir;
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * _facing;
            transform.localScale = s;
        }

        protected void SetHorizontalVelocity(float x) =>
            _rb.linearVelocity = new Vector2(x, _rb.linearVelocity.y);

        /// <summary>
        /// Overlap the attack hitbox in front of the enemy and damage everything alive on the
        /// target layers. Mirrors WeaponBase.PerformHit so player and enemy hits behave identically —
        /// which is what makes i-frames a reliable answer to both.
        /// </summary>
        protected int PerformAttackHit()
        {
            Vector2 center = HitboxCenter();
            var cols = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, targetLayers);

            int count = 0;
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<IDamageable>(out var victim) || !victim.IsAlive) continue;

                var info = new DamageInfo
                {
                    Amount = data.damage,
                    HitPoint = col.ClosestPoint(center),
                    KnockbackDir = new Vector2(_facing, 0.35f).normalized,
                    KnockbackForce = data.knockbackForce,
                    Source = gameObject
                };
                victim.TakeDamage(info);
                count++;
            }
            return count;
        }

        protected Vector2 HitboxCenter() => data == null
            ? (Vector2)transform.position
            : (Vector2)transform.position + new Vector2(data.hitboxOffset.x * _facing, data.hitboxOffset.y);

        protected void SetTint(Color c)
        {
            if (_sprite != null) _sprite.color = c;
        }

        protected void ResetTint() => SetTint(_baseColor);

        protected virtual void OnDrawGizmosSelected()
        {
            if (data == null) return;

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.8f);
            Gizmos.DrawWireCube(HitboxCenter(), data.hitboxSize);

            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, data.chaseRange);

            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, data.attackRange);
        }
    }
}
