using Kagemura.Player.Weapons;
using Kagemura.Systems;
using UnityEngine;

namespace Kagemura.Enemies
{
    /// <summary>
    /// Spec §2.5 Type B — the ranged yokai: punishes reckless approach, rewards dodge timing.
    ///
    /// Where the rusher asks "can you read one swing", this one asks "can you cross open ground".
    /// It never advances. It holds its line, telegraphs a shot with the same windup tint the
    /// rusher uses, and gives ground when the player gets inside <see cref="EnemyData.retreatDistance"/>.
    /// Walking straight at it eats a bolt; dodging through the bolt and closing gets you a
    /// retreating enemy that can't shoot back. That trade is the whole type.
    ///
    /// Two dodge windows rather than the rusher's one — the windup, and then the bolt's travel
    /// time. Both are tuned from the same EnemyData asset, so making this type more or less
    /// dangerous is a data change (spec §6).
    ///
    /// The shot direction is locked when the windup begins, exactly as the rusher commits its
    /// swing: an enemy that re-aims at the moment of release makes its own telegraph a lie and
    /// leaves the player nothing to time against.
    /// </summary>
    public class EnemyRanged : EnemyBase
    {
        [Header("Ranged")]
        [Tooltip("Terrain that stops bolts and blocks line of sight. Set this to your Ground layer.")]
        [SerializeField] private LayerMask blockingLayers;
        [Tooltip("Where bolts leave the enemy, relative to it (+X = forward).")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.7f, 0.2f);

        [Header("Bolt (leave the prefab empty for the greybox one)")]
        [SerializeField] private ArrowProjectile boltPrefab;
        [SerializeField] private Vector2 greyboxBoltSize = new Vector2(0.4f, 0.16f);
        [SerializeField] private Color boltColor = new Color(0.62f, 0.4f, 0.85f);

        private enum State { Idle, Track, Aim, Recover, Retreat }

        /// <summary>
        /// Slack on the retreat threshold. Without it the enemy sits exactly on the boundary and
        /// flips between Track and Retreat every frame, which reads as a twitch rather than a
        /// decision.
        /// </summary>
        private const float RetreatHysteresis = 0.75f;

        private State _state = State.Idle;
        private float _stateTimer;
        private float _nextAttackTime;
        private Vector2 _aimDirection = Vector2.right;

        /// <summary>Also takes the blocking layers, which the base has no field for.</summary>
        public override void Configure(EnemyData enemyData, LayerMask target, LayerMask blocking)
        {
            base.Configure(enemyData, target, blocking);
            blockingLayers = blocking;
        }

        private void Update()
        {
            if (data == null || !_health.IsAlive) return;

            _stateTimer -= Time.deltaTime;

            // Simpler than the rusher's stagger case: there's no live hitbox to protect, because
            // a bolt that already exists is a separate object and outlives its shooter's state.
            if (IsStaggered)
            {
                if (_state == State.Aim) CancelAim();
                SetHorizontalVelocity(0f);
                return;
            }

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Track: TickTrack(); break;
                case State.Aim: TickAim(); break;
                case State.Recover: TickRecover(); break;
                case State.Retreat: TickRetreat(); break;
            }
        }

        private void TickIdle()
        {
            SetHorizontalVelocity(0f);
            if (HasTarget && DistanceToTarget <= data.chaseRange)
                Enter(State.Track);
        }

        /// <summary>Holding position, looking for a shot.</summary>
        private void TickTrack()
        {
            if (!HasTarget || DistanceToTarget > data.chaseRange)
            {
                Enter(State.Idle);
                return;
            }

            FaceTarget();
            SetHorizontalVelocity(0f);

            // Backing off outranks shooting. At point-blank this thing is free damage, and
            // surrendering ground rather than trading blows is what stops it being a rusher
            // that happens to throw things.
            if (DistanceToTarget < data.retreatDistance)
            {
                Enter(State.Retreat);
                return;
            }

            if (DistanceToTarget <= data.attackRange && Time.time >= _nextAttackTime && HasLineOfSight())
                BeginAim();
        }

        private void TickAim()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer > 0f) return;

            ResetTint();
            Fire();

            _nextAttackTime = Time.time + data.attackCooldown;
            Enter(State.Recover, data.recoveryTime);
        }

        private void TickRecover()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer <= 0f) Enter(State.Track);
        }

        /// <summary>
        /// Giving ground, and deliberately unable to shoot while doing it. A kiting enemy that
        /// also fires would make closing the distance pointless, and closing the distance is the
        /// player's reward for reading the bolt correctly.
        /// </summary>
        private void TickRetreat()
        {
            if (!HasTarget)
            {
                Enter(State.Idle);
                return;
            }

            FaceTarget();      // walks backwards, keeps looking at the player

            if (DistanceToTarget >= data.retreatDistance + RetreatHysteresis)
            {
                Enter(State.Track);
                return;
            }

            SetHorizontalVelocity(-_facing * data.moveSpeed);
        }

        private void BeginAim()
        {
            Vector2 muzzle = Muzzle();
            Vector2 toTarget = (Vector2)target.position - muzzle;

            // Locked now, fired later. Aimed at the target rather than straight ahead so a player
            // on a ledge is still a target, which is what keeps high ground from being a free win.
            _aimDirection = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : new Vector2(_facing, 0f);

            SetTint(data.windupColor);
            Enter(State.Aim, data.windupTime);
        }

        private void Fire()
        {
            Vector3 spawn = Muzzle();

            ArrowProjectile bolt = EnemyBolt.Spawn(boltPrefab, spawn, greyboxBoltSize, boltColor);

            // Source is this enemy, which is what lets a parried bolt punish the shooter at
            // range — ParryController staggers whatever EnemyBase it finds on the source.
            // FocusOnHit is left at 0: the player's pool is the player's to earn.
            bolt.Launch(_aimDirection, data.projectileSpeed, data.damage, data.knockbackForce,
                        targetLayers, blockingLayers, data.projectileLifetime, gameObject);
        }

        /// <summary>
        /// Parried: drop the shot entirely and take the full cooldown, matching the rusher.
        /// Only the windup can be taken away — once a bolt is in the air it's its own object,
        /// and the player's answer to it is the dodge, not a second parry on the shooter.
        /// </summary>
        public override void InterruptAttack()
        {
            if (_state != State.Aim) return;

            ResetTint();
            _nextAttackTime = Time.time + data.attackCooldown;
            Enter(State.Recover, data.recoveryTime);
        }

        /// <summary>Staggered mid-windup: shot lost, but only half the cooldown, as with the rusher.</summary>
        private void CancelAim()
        {
            ResetTint();
            _nextAttackTime = Time.time + data.attackCooldown * 0.5f;
            Enter(State.Track);
        }

        /// <summary>
        /// Terrain between muzzle and target means no shot. Checked at the muzzle rather than the
        /// enemy's centre so it matches where the bolt actually starts — otherwise the enemy
        /// fires confidently into the wall it's standing behind.
        /// </summary>
        private bool HasLineOfSight()
        {
            if (!HasTarget) return false;

            Vector2 muzzle = Muzzle();
            Vector2 toTarget = (Vector2)target.position - muzzle;
            float distance = toTarget.magnitude;
            if (distance < 0.01f) return true;

            return !Physics2D.Raycast(muzzle, toTarget / distance, distance, blockingLayers);
        }

        private Vector2 Muzzle() => (Vector2)transform.position
                                    + new Vector2(muzzleOffset.x * _facing, muzzleOffset.y);

        private void Enter(State next, float duration = 0f)
        {
            _state = next;
            _stateTimer = duration;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (data == null) return;

            // The ring the player is trying to get inside.
            Gizmos.color = new Color(0.62f, 0.4f, 0.85f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, data.retreatDistance);

            Gizmos.color = new Color(0.62f, 0.4f, 0.85f, 0.9f);
            Gizmos.DrawWireSphere(Muzzle(), 0.1f);
        }
    }
}
