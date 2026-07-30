using UnityEngine;

namespace Kagamura.Enemies
{
    /// <summary>
    /// Spec §2.5 Type A — the melee rusher: closes distance and punishes standing still.
    /// Exists at this point in the build order for one reason: dodge i-frames can only be
    /// tuned against a real, telegraphed enemy attack (Build Order step 3).
    ///
    /// Deliberately a plain enum + switch state machine rather than a behaviour tree (spec §6).
    /// The attack is fully committed once Windup begins — it won't re-aim at the player, so
    /// rolling through it is the correct answer and the player can learn the timing.
    /// </summary>
    public class EnemyRusher : EnemyBase
    {
        private enum State { Idle, Chase, Windup, Strike, Recover }

        private State _state = State.Idle;
        private float _stateTimer;
        private float _nextAttackTime;
        private bool _hitLandedThisSwing;

        private void Update()
        {
            if (data == null || !_health.IsAlive) return;

            _stateTimer -= Time.deltaTime;

            // A hit interrupts anything short of an already-live hitbox.
            if (IsStaggered && _state != State.Strike)
            {
                if (_state == State.Windup) CancelSwing();
                SetHorizontalVelocity(0f);
                return;
            }

            switch (_state)
            {
                case State.Idle: TickIdle(); break;
                case State.Chase: TickChase(); break;
                case State.Windup: TickWindup(); break;
                case State.Strike: TickStrike(); break;
                case State.Recover: TickRecover(); break;
            }
        }

        private void TickIdle()
        {
            SetHorizontalVelocity(0f);
            if (HasTarget && DistanceToTarget <= data.chaseRange)
                Enter(State.Chase);
        }

        private void TickChase()
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

            // Hold at stopDistance so the enemy crowds the player without pushing them.
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

            // One connection per swing — the hitbox is live for several frames, but a dodge
            // that clears the first overlap shouldn't get clipped by the next one.
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
            if (_stateTimer <= 0f) Enter(State.Chase);
        }

        /// <summary>
        /// Parried: drop the swing wherever it is, live hitbox included, and take the full
        /// cooldown rather than the half CancelSwing gives — a read that good should buy the
        /// player more than a plain interrupt does.
        /// </summary>
        public override void InterruptAttack()
        {
            if (_state != State.Windup && _state != State.Strike) return;

            ResetTint();
            _hitLandedThisSwing = true;          // no further connections from this swing
            _nextAttackTime = Time.time + data.attackCooldown;
            Enter(State.Recover, data.recoveryTime);
        }

        private void CancelSwing()
        {
            ResetTint();
            _nextAttackTime = Time.time + data.attackCooldown * 0.5f;
            Enter(State.Chase);
        }

        private void Enter(State next, float duration = 0f)
        {
            _state = next;
            _stateTimer = duration;
        }
    }
}
