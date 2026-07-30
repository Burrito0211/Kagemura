using Kagamura.Systems;
using UnityEngine;

namespace Kagamura.Enemies
{
    /// <summary>
    /// Spec §2.5 Type C — the shielded yokai: punishes button-mashing, rewards the heavier
    /// weapon or a special.
    ///
    /// It advances like the rusher but slower, and holds a guard that simply refuses any frontal
    /// hit under <see cref="EnemyData.guardBreakDamage"/>. Sickle spam bounces off it forever.
    /// One committed swing, a charged arrow or a special breaks the guard and buys a long
    /// opening. That is the entire type: a check on whether the player can stop mashing and
    /// pick the right tool.
    ///
    /// Three ways through it, so it never reads as a wall:
    ///   - Break the guard with a big enough single hit.
    ///   - Get behind it — the guard only faces forward.
    ///   - Wait. The guard drops the moment it commits to its own swing and stays down through
    ///     the recovery, so the patient answer is the same dodge-and-punish the rusher teaches.
    ///
    /// Bleed already on it keeps ticking regardless: a shield explains stopping a blade, not
    /// stopping a wound. It can only be applied from behind or through a broken guard anyway.
    /// </summary>
    public class EnemyShielded : EnemyBase, IDamageFilter
    {
        private enum State { Idle, Advance, Windup, Strike, Recover, GuardBroken }

        private State _state = State.Idle;
        private float _stateTimer;
        private float _nextAttackTime;
        private bool _hitLandedThisSwing;
        private float _blockFlashUntil;

        /// <summary>
        /// The guard is down while the enemy is committing to its own attack and through the
        /// recovery after it. Sharing the punish window with the rusher's is deliberate — one
        /// timing to learn, applied to a tougher target.
        /// </summary>
        private bool GuardUp => _state != State.Strike
                                && _state != State.Recover
                                && _state != State.GuardBroken;

        private void Update()
        {
            if (data == null || !_health.IsAlive) return;

            _stateTimer -= Time.deltaTime;

            if (Time.time >= _blockFlashUntil && _state != State.Windup && _state != State.GuardBroken)
                ResetTint();

            // A broken guard outranks stagger: it is a longer, louder state and being hit during
            // it must not cut it short, or the reward for breaking it evaporates.
            if (_state == State.GuardBroken)
            {
                SetHorizontalVelocity(0f);
                if (_stateTimer <= 0f)
                {
                    ResetTint();
                    Enter(State.Advance);
                }
                return;
            }

            if (IsStaggered && _state != State.Strike)
            {
                if (_state == State.Windup) CancelSwing();
                SetHorizontalVelocity(0f);
                return;
            }

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Advance: TickAdvance(); break;
                case State.Windup: TickWindup(); break;
                case State.Strike: TickStrike(); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickIdle()
        {
            SetHorizontalVelocity(0f);
            if (HasTarget && DistanceToTarget <= data.chaseRange)
                Enter(State.Advance);
        }

        private void TickAdvance()
        {
            if (!HasTarget || DistanceToTarget > data.chaseRange)
            {
                Enter(State.Idle);
                return;
            }

            FaceTarget();

            if (DistanceToTarget <= data.attackRange && Time.time >= _nextAttackTime)
            {
                Enter(State.Windup, data.windupTime);
                SetTint(data.windupColor);
                SetHorizontalVelocity(0f);
                return;
            }

            float gap = Mathf.Abs(SignedDistanceToTarget);
            SetHorizontalVelocity(gap > data.stopDistance ? _facing * data.moveSpeed : 0f);
        }

        private void TickWindup()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer > 0f) return;

            ResetTint();
            _hitLandedThisSwing = false;
            Enter(State.Strike, data.activeTime);
        }

        private void TickStrike()
        {
            SetHorizontalVelocity(0f);

            if (!_hitLandedThisSwing && PerformAttackHit() > 0)
                _hitLandedThisSwing = true;

            if (_stateTimer <= 0f)
            {
                _nextAttackTime = Time.time + data.attackCooldown;
                Enter(State.Recover, data.recoveryTime);
            }
        }

        private void TickRecover()
        {
            SetHorizontalVelocity(0f);
            if (_stateTimer <= 0f) Enter(State.Advance);
        }

        // --- Guard ----------------------------------------------------------------------

        /// <summary>
        /// Health hands every incoming hit here before spending it (see <see cref="IDamageFilter"/>).
        /// </summary>
        public bool FilterDamage(ref DamageInfo info)
        {
            // Damage-over-time is never guarded. A shield stops a blade, not a wound already
            // opened — and this also sidesteps DoT ticks having no meaningful hit position.
            if (info.IgnoresStagger) return true;

            if (!GuardUp) return true;
            if (!IsFromFront(info.HitPoint)) return true;

            if (info.Amount >= data.guardBreakDamage)
            {
                BreakGuard();
                return true;      // the breaking hit still lands, it just costs the guard too
            }

            // Refused. Health routes this through OnDamageAvoided, so the world health bar and
            // anything else watching for a negated hit already knows.
            SetTint(data.guardBlockColor);
            _blockFlashUntil = Time.time + 0.1f;
            return false;
        }

        /// <summary>
        /// Front is simply the side the enemy is facing. HitPoint is the point on this enemy's
        /// own collider nearest the attack, so a blow from behind lands behind the midline
        /// whatever the attacker's own position.
        /// </summary>
        private bool IsFromFront(Vector2 hitPoint)
        {
            float dx = hitPoint.x - transform.position.x;
            if (Mathf.Abs(dx) < 0.01f) return true;    // dead-on counts as blocked
            return (dx > 0f) == (_facing > 0);
        }

        private void BreakGuard()
        {
            SetTint(data.guardBrokenColor);
            _hitLandedThisSwing = true;                        // kill any swing in progress
            _nextAttackTime = Time.time + data.attackCooldown;
            Enter(State.GuardBroken, data.guardBrokenDuration);
        }

        /// <summary>
        /// Parried. Breaking the guard outright rather than only dropping the swing: a parry is
        /// already the hardest read in the game, and against the one enemy built to punish
        /// impatience it should pay the same opening a heavy hit does.
        /// </summary>
        public override void InterruptAttack()
        {
            if (_state == State.GuardBroken) return;
            BreakGuard();
        }

        private void CancelSwing()
        {
            ResetTint();
            _nextAttackTime = Time.time + data.attackCooldown * 0.5f;
            Enter(State.Advance);
        }

        private void Enter(State next, float duration = 0f)
        {
            _state = next;
            _stateTimer = duration;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (data == null) return;

            // Which side the guard covers, drawn as a bar in front of the enemy.
            Gizmos.color = new Color(0.65f, 0.7f, 0.8f, 0.9f);
            Vector3 front = transform.position + new Vector3(0.6f * _facing, 0f, 0f);
            Gizmos.DrawLine(front + Vector3.down * 0.7f, front + Vector3.up * 0.7f);
        }
    }
}
