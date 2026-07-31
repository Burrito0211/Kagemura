using UnityEngine;

namespace Kagemura.Player.Weapons
{
    /// <summary>
    /// The balanced default weapon (spec §2.2): a 2-hit combo. Chain the second swing within
    /// the combo window for a stronger finisher; let the window lapse and the combo restarts.
    /// This is the first weapon to prototype and tune — everything else is measured against it.
    /// </summary>
    public class SwordWeapon : WeaponBase
    {
        private int _comboStep;
        private float _prevSwingTime = -999f;

        protected override void DoAttack(int facing)
        {
            // Restart the combo if too much time passed since the last swing.
            if (Time.time - _prevSwingTime > data.comboResetTime)
                _comboStep = 0;

            bool isFinisher = _comboStep >= data.comboMaxHits - 1;
            int damage = isFinisher
                ? Mathf.RoundToInt(data.damage * data.finisherDamageMultiplier)
                : data.damage;

            int hits = PerformHit(facing, damage, out _);

            Debug.Log($"[Sword] swing {_comboStep + 1}/{data.comboMaxHits}" +
                      $"{(isFinisher ? " (finisher)" : "")} dmg={damage} hits={hits}");

            _prevSwingTime = Time.time;
            _comboStep = isFinisher ? 0 : _comboStep + 1;
        }
    }
}
