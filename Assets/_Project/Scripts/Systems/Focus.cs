using System;
using UnityEngine;

namespace Kagemura.Systems
{
    /// <summary>
    /// Build Order step 7: the single resource both specials spend (spec §2.4 — one pool, not
    /// two economies). Answers the §9 question as "shared Focus", and the bow deliberately
    /// doesn't touch it: that weapon is limited by its draw, so the pool stays purely about
    /// specials and there's only one place to look when they feel too cheap or too rare.
    ///
    /// Focus is counted in whole **charges**, not a percentage. A player mid-fight can't read a
    /// sliding bar fast enough to decide anything with it, but "I have 3, the slam costs 3" is a
    /// decision they can make at a glance. Costs are small integers for the same reason.
    ///
    /// Progress toward the next charge is tracked in points underneath, where
    /// <see cref="pointsPerCharge"/> is one charge — so an award of 34 reads as "about a third
    /// of a charge" and the HUD can part-fill the next pip instead of jumping.
    ///
    /// Charges are earned two ways, both of which require being in the fight:
    ///   - landing hits (weapons award focusPerHit from their WeaponData)
    ///   - avoiding a hit with dodge i-frames
    /// There is no passive regen. Retreating to recharge isn't a strategy — the meter only
    /// moves while the player is attacking or reading attacks, which is the loop worth
    /// rewarding.
    ///
    /// The dodge award can't be farmed by mashing: it fires off Health.OnDamageAvoided, so it
    /// takes a real incoming attack to earn, and rolling through empty air pays nothing.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Focus : MonoBehaviour
    {
        [Tooltip("How many charges the player can bank. Keep it small — this is the number they " +
                 "have to be able to count without looking away from the fight.")]
        [SerializeField] private int maxCharges = 5;
        [Tooltip("Charges held at the start of a level. Non-zero hands the player one special up front.")]
        [SerializeField] private int startingCharges = 0;

        [Tooltip("Points that make up one charge. Treat it as a scale, not a tuning knob: leave " +
                 "it at 100 and every award below reads as a percentage of a charge.")]
        [SerializeField] private int pointsPerCharge = 100;

        [Header("Earning")]
        [Tooltip("Points when an attack the player dodged through would have landed. Worth " +
                 "several hits — a read is harder than a swing.")]
        [SerializeField] private int focusPerAvoidedHit = 50;

        private Health _health;
        private int _points;

        /// <summary>(charges, maxCharges, progress 0..1 toward the next charge).</summary>
        public event Action<int, int, float> OnFocusChanged;

        /// <summary>Raised when a special was pressed and couldn't be paid for. Feedback hook.</summary>
        public event Action OnSpendFailed;

        /// <summary>Raised when a whole new charge lands — the moment worth a sound.</summary>
        public event Action<int> OnChargeGained;

        /// <summary>Whole charges available to spend.</summary>
        public int Charges => _points / pointsPerCharge;
        public int MaxCharges => maxCharges;

        /// <summary>0..1 progress into the charge currently being filled.</summary>
        public float PartialCharge => (_points % pointsPerCharge) / (float)pointsPerCharge;

        public bool IsFull => Charges >= maxCharges;

        private int MaxPoints => maxCharges * pointsPerCharge;

        private void Awake()
        {
            _health = GetComponent<Health>();
            pointsPerCharge = Mathf.Max(1, pointsPerCharge);
            maxCharges = Mathf.Max(1, maxCharges);
            _points = Mathf.Clamp(startingCharges, 0, maxCharges) * pointsPerCharge;
        }

        private void OnEnable()
        {
            if (_health != null) _health.OnDamageAvoided += HandleDamageAvoided;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnDamageAvoided -= HandleDamageAvoided;
        }

        private void Start() => Raise();

        public bool CanAfford(int chargeCost) => Charges >= chargeCost;

        /// <summary>
        /// Pay for a special, in whole charges. Returns false and leaves the pool untouched if
        /// it can't be paid, so the caller can bail before starting an animation. Partial
        /// progress toward the next charge is kept — spending never wastes it.
        /// </summary>
        public bool TrySpend(int chargeCost)
        {
            if (chargeCost <= 0) return true;
            if (Charges < chargeCost)
            {
                OnSpendFailed?.Invoke();
                return false;
            }

            _points -= chargeCost * pointsPerCharge;
            Raise();
            return true;
        }

        /// <summary>Add progress, in points, where pointsPerCharge is one whole charge.</summary>
        public void Gain(int points)
        {
            if (points <= 0 || _points >= MaxPoints) return;

            int before = Charges;
            _points = Mathf.Min(MaxPoints, _points + points);
            Raise();

            int gained = Charges - before;
            if (gained > 0) OnChargeGained?.Invoke(gained);
        }

        private void Raise() => OnFocusChanged?.Invoke(Charges, maxCharges, PartialCharge);

        /// <summary>
        /// Health raises OnDamageAvoided for post-hit i-frames too, and those are handed out
        /// automatically — paying Focus for them would reward getting hit. Only an ability's
        /// held i-frames count as a read.
        /// </summary>
        private void HandleDamageAvoided(DamageInfo info)
        {
            if (_health != null && _health.IsInvulnerableFromAbility) Gain(focusPerAvoidedHit);
        }
    }
}
