using Kagemura.Player.Weapons;
using UnityEngine;

namespace Kagemura.Enemies
{
    /// <summary>
    /// Build Order step 11 — the final boss (spec §2.5): two attack phases, the second triggered
    /// at an HP threshold with a visual tell.
    ///
    /// A plain enum + switch, as the spec asks (§6): a two-phase fight does not need a behaviour
    /// tree, and one would be harder to tune than the fight is to write.
    ///
    /// It deliberately fights with the vocabulary the player has already been taught rather than
    /// with new mechanics. Its slash is the rusher's committed swing; its bolts are the archer's
    /// telegraphed shot. Everything the four levels drilled — read the windup, dodge through,
    /// punish the recovery — is the answer here too. The fight is a test of those reads under
    /// pressure, not a fresh set of rules at the very end of a 30-minute game.
    ///
    /// So phase 2 changes tempo, not vocabulary: shorter telegraphs, less time between attacks,
    /// faster movement, and single shots becoming a fan that has to be moved out of rather than
    /// sidestepped. Same reads, less room.
    /// </summary>
    public class BossController : EnemyBase
    {
        [Header("Boss")]
        [Tooltip("Terrain that stops bolts. Set this to your Ground layer.")]
        [SerializeField] private LayerMask blockingLayers;
        [Tooltip("Where bolts leave the boss, relative to it (+X = forward).")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0.9f, 0.3f);

        [Header("Bolt (leave the prefab empty for the greybox one)")]
        [SerializeField] private ArrowProjectile boltPrefab;
        [SerializeField] private Vector2 greyboxBoltSize = new Vector2(0.5f, 0.2f);
        [SerializeField] private Color boltColor = new Color(0.9f, 0.35f, 0.4f);

        private enum State { Idle, Approach, SlashWindup, SlashStrike, CastWindup, Recover, PhaseChange }

        private State _state = State.Idle;
        private float _stateTimer;
        private float _nextAttackTime;
        private bool _hitLandedThisSwing;
        private Vector2 _aimDirection = Vector2.right;

        private BossData _boss;
        private int _phase = 1;

        /// <summary>Phase the boss is in: 1 or 2. Read by the HUD/arena for the visual tell.</summary>
        public int Phase => _phase;

        /// <summary>Raised when phase 2 begins, for the arena change, music swap and screen shake.</summary>
        public event System.Action<int> OnPhaseChanged;

        // Phase 2 sharpens the numbers rather than replacing them, so every timing below is the
        // asset's value scaled — one place to tune, and phase 1 stays the reference.
        private float WindupTime => data.windupTime * (_phase == 2 ? _boss.phase2WindupScale : 1f);
        private float Cooldown => data.attackCooldown * (_phase == 2 ? _boss.phase2CooldownScale : 1f);
        private float MoveSpeed => data.moveSpeed * (_phase == 2 ? _boss.phase2SpeedScale : 1f);
        private int BoltCount => _phase == 2 ? _boss.phase2Bolts : _boss.phase1Bolts;

        /// <summary>Also takes the blocking layers, which the base has no field for.</summary>
        public override void Configure(EnemyData enemyData, LayerMask target, LayerMask blocking)
        {
            base.Configure(enemyData, target, blocking);
            blockingLayers = blocking;
        }

        protected override void Awake()
        {
            base.Awake();

            _boss = data as BossData;
            if (_boss == null)
                Debug.LogError($"[{name}] BossController needs a BossData asset (Create > " +
                               "Kagemura > Boss Data), not a plain EnemyData. The fight will " +
                               "not start.", this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            _health.OnHealthChanged += HandleHealthChanged;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _health.OnHealthChanged -= HandleHealthChanged;
        }

        private void Update()
        {
            if (_boss == null || !_health.IsAlive) return;

            _stateTimer -= Time.deltaTime;

            // The turn outranks everything, stagger included. It is the one moment the fight
            // stops to tell the player something, and a stray hit must not talk over it.
            if (_state == State.PhaseChange)
            {
                SetHorizontalVelocity(0f);
                if (_stateTimer <= 0f) FinishPhaseChange();
                return;
            }

            if (IsStaggered && _state != State.SlashStrike)
            {
                if (_state == State.SlashWindup || _state == State.CastWindup) CancelAttack();
                SetHorizontalVelocity(0f);
                return;
            }

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Approach: TickApproach(); break;
                case State.SlashWindup: TickSlashWindup(); break;
                case State.SlashStrike: TickSlashStrike(); break;
                case State.CastWindup: TickCastWindup(); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickIdle()
        {
            SetHorizontalVelocity(0f);
            if (HasTarget && DistanceToTarget <= data.chaseRange) Enter(State.Approach);
        }

        /// <summary>
        /// Picks between the two attacks purely on range, so the player can always tell which is
        /// coming from where they are standing. A boss that chose at random would make positioning
        /// meaningless and the telegraphs unlearnable.
        /// </summary>
        private void TickApproach()
        {
            if (!HasTarget)
            {
                Enter(State.Idle);
                return;
            }

            FaceTarget();

            if (Time.time >= _nextAttackTime)
            {
                if (DistanceToTarget <= data.attackRange)
                {
                    BeginSlash();
                    return;
                }

                if (DistanceToTarget >= _boss.rangedThreshold)
                {
                    BeginCast();
                    return;
                }
            }

            float gap = Mathf.Abs(SignedDistanceToTarget);
            SetHorizontalVelocity(gap > data.stopDistance ? _facing * MoveSpeed : 0f);
        }

        private void BeginSlash()
        {
            SetTint(data.windupColor);
            SetHorizontalVelocity(0f);
            Enter(State.SlashWindup, WindupTime);
        }

        private void TickSlashWindup()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer > 0f) return;

            RestorePhaseTint();
            _hitLandedThisSwing = false;
            Enter(State.SlashStrike, data.activeTime);
        }

        private void TickSlashStrike()
        {
            SetHorizontalVelocity(0f);

            if (!_hitLandedThisSwing && PerformAttackHit() > 0)
                _hitLandedThisSwing = true;

            if (_stateTimer <= 0f)
            {
                _nextAttackTime = Time.time + Cooldown;
                Enter(State.Recover, data.recoveryTime);
            }
        }

        private void BeginCast()
        {
            Vector2 muzzle = Muzzle();
            Vector2 toTarget = (Vector2)target.position - muzzle;

            // Locked at the start of the windup, like the archer's shot: an attack that re-aims
            // at the moment of release makes its own telegraph a lie.
            _aimDirection = toTarget.sqrMagnitude > 0.0001f
                ? toTarget.normalized
                : new Vector2(_facing, 0f);

            SetTint(data.windupColor);
            SetHorizontalVelocity(0f);
            Enter(State.CastWindup, WindupTime);
        }

        private void TickCastWindup()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer > 0f) return;

            RestorePhaseTint();
            FireVolley();

            _nextAttackTime = Time.time + Cooldown;
            Enter(State.Recover, data.recoveryTime);
        }

        /// <summary>
        /// One bolt is a single aimed shot. More than one fans symmetrically around the aim, so
        /// the fan always has the same centre the player was already reading.
        /// </summary>
        private void FireVolley()
        {
            int count = Mathf.Max(1, BoltCount);
            float spread = count > 1 ? _boss.volleySpreadDegrees : 0f;
            float step = count > 1 ? spread / (count - 1) : 0f;
            float start = -spread * 0.5f;

            Vector3 muzzle = Muzzle();

            for (int i = 0; i < count; i++)
            {
                Vector2 direction = Quaternion.Euler(0f, 0f, start + step * i) * _aimDirection;

                var bolt = EnemyBolt.Spawn(boltPrefab, muzzle, greyboxBoltSize, boltColor);
                bolt.Launch(direction, data.projectileSpeed, data.damage, data.knockbackForce,
                            targetLayers, blockingLayers, data.projectileLifetime, gameObject);
            }
        }

        private void TickRecover()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer <= 0f) Enter(State.Approach);
        }

        // --- Phases ---------------------------------------------------------------------

        /// <summary>
        /// Watches its own health rather than being told to change phase, so the threshold holds
        /// however the damage arrived — a slam, a bleed tick, or a parry-punish burst.
        /// </summary>
        private void HandleHealthChanged(int current, int max)
        {
            if (_phase != 1 || _boss == null || max <= 0) return;
            if (current <= 0) return;                       // dying is not a phase change
            if ((float)current / max > _boss.phase2HealthFraction) return;

            BeginPhaseChange();
        }

        private void BeginPhaseChange()
        {
            _phase = 2;

            // Untouchable through the turn. Without this the threshold could be crossed and the
            // whole tell eaten by the same combo, and the player would never see the fight change.
            _health.SetInvulnerable(true);

            SetTint(_boss.phase2Color);
            _hitLandedThisSwing = true;                     // drop any swing in progress
            Enter(State.PhaseChange, _boss.phaseTransitionTime);
        }

        private void FinishPhaseChange()
        {
            _health.SetInvulnerable(false);
            RestorePhaseTint();

            // A beat of grace before it acts, so the player gets to move first out of the turn.
            _nextAttackTime = Time.time + Cooldown * 0.5f;
            Enter(State.Approach);

            OnPhaseChanged?.Invoke(_phase);
        }

        /// <summary>Back to the body colour for the current phase — red for the rest of the fight.</summary>
        private void RestorePhaseTint()
        {
            if (_phase == 2 && _boss != null) SetTint(_boss.phase2Color);
            else ResetTint();
        }

        /// <summary>Parried: drop the attack and pay the full cooldown, as the lesser enemies do.</summary>
        public override void InterruptAttack()
        {
            if (_state != State.SlashWindup && _state != State.CastWindup && _state != State.SlashStrike)
                return;

            RestorePhaseTint();
            _hitLandedThisSwing = true;
            _nextAttackTime = Time.time + Cooldown;
            Enter(State.Recover, data.recoveryTime);
        }

        private void CancelAttack()
        {
            RestorePhaseTint();
            _nextAttackTime = Time.time + Cooldown * 0.5f;
            Enter(State.Approach);
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
            if (_boss == null && data is BossData asset) _boss = asset;
            if (_boss == null) return;

            // Where it stops closing and starts shooting.
            Gizmos.color = new Color(0.9f, 0.35f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _boss.rangedThreshold);
        }
    }
}
