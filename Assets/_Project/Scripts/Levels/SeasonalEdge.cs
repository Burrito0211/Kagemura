using Kagemura.Player;
using Kagemura.Player.Weapons;
using Kagemura.UI;
using UnityEngine;

namespace Kagemura.Levels
{
    /// <summary>
    /// Build Order step 13.5: applies the season's weapon edge (spec §2.6).
    ///
    /// One of these sits in each level scene holding that level's <see cref="LevelData"/>. It is
    /// the only thing at runtime that reads a LevelData at all — the rest of a level is baked
    /// geometry — and it exists because the edge is the one part of a season that cannot be baked
    /// into the scene as objects.
    ///
    /// Applied in Awake, before any Start runs, so the HUD's opening readout already knows which
    /// weapon is sharpened rather than showing an unmarked one until the first weapon switch.
    /// </summary>
    public class SeasonalEdge : MonoBehaviour
    {
        [Tooltip("This level's data asset. Without one, nothing is sharpened.")]
        [SerializeField] private LevelData level;

        [Tooltip("Seconds the season banner stays up at level start. 0 hides it.")]
        [SerializeField] private float bannerSeconds = 3.5f;

        /// <summary>Set the level from a builder, before the scene is saved.</summary>
        public void SetLevel(LevelData data) => level = data;

        private void Awake()
        {
            if (level == null)
            {
                Debug.LogWarning("[SeasonalEdge] No LevelData assigned, so no weapon is " +
                                 "sharpened here. Run 'Kagemura/Setup/6' to wire the level " +
                                 "scenes up.", this);
                return;
            }

            ApplyEdge();
        }

        private void Start()
        {
            // Start, not Awake: the HUD builds its greybox labels in its own Awake, so there is
            // nothing to write into until every Awake has run.
            if (level != null && bannerSeconds > 0f) ShowBanner();
        }

        private void ApplyEdge()
        {
            if (level.sharpenedWeapon == SharpenedWeapon.None) return;

            var player = FindFirstObjectByType<PlayerCombat>();
            if (player == null)
            {
                Debug.LogWarning("[SeasonalEdge] No PlayerCombat in the scene, so there are no " +
                                 "weapons to sharpen.", this);
                return;
            }

            // Every WeaponBase on the player, matched by type. Matching on the component rather
            // than on a WeaponData reference means a season cannot be pointed at the wrong stats
            // asset, and a weapon the player is not carrying is simply not found.
            bool found = false;

            foreach (var weapon in player.GetComponents<WeaponBase>())
            {
                if (!Matches(weapon, level.sharpenedWeapon)) continue;

                weapon.SetEdge(level.edgeMultiplier);
                found = true;

                Debug.Log($"[SeasonalEdge] {level.season}: {level.sharpenedWeapon} sharpened " +
                          $"x{level.edgeMultiplier:0.00}.", this);
            }

            if (!found)
                Debug.LogWarning($"[SeasonalEdge] {level.season} sharpens the " +
                                 $"{level.sharpenedWeapon}, but the player is not carrying one. " +
                                 "Nothing was sharpened.", this);
        }

        private static bool Matches(WeaponBase weapon, SharpenedWeapon kind) => kind switch
        {
            SharpenedWeapon.Sword => weapon is SwordWeapon,
            SharpenedWeapon.Sickle => weapon is SickleWeapon,
            SharpenedWeapon.Bow => weapon is BowWeapon,
            _ => false
        };

        /// <summary>
        /// Announce the season and its edge.
        ///
        /// Spec §2.6 rule 4: an unannounced buff is worse than no buff, because it changes the
        /// numbers without changing a single decision the player makes. The permanent marker on
        /// the weapon readout is the other half of this; the banner is what makes someone look at
        /// the readout in the first place.
        /// </summary>
        private void ShowBanner()
        {
            var hud = FindFirstObjectByType<HUDController>();
            if (hud == null) return;

            string text = level.sharpenedWeapon == SharpenedWeapon.None
                // Winter and the arena. Said out loud rather than left blank — the absence is the
                // point, and a player who was told about three edges should be told when the
                // fourth season withholds one.
                ? $"{level.season} — no edge"
                : $"{level.season} — {level.sharpenedWeapon.ToString().ToLowerInvariant()} sharpened";

            hud.ShowSeasonBanner(text, level.accentColor, bannerSeconds);
        }
    }
}
