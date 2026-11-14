using UnityEngine;

namespace Kagemura.Feel
{
    /// <summary>
    /// Every number behind the polish pass (Build Order step 15): hit-stop, screen shake and
    /// hit sparks. One asset for the whole game, because these are tuned against each other —
    /// a longer freeze wants a smaller shake — and splitting them across prefabs would hide that.
    ///
    /// Lives in a Resources folder and is loaded by name, so <see cref="CombatFeel"/> needs no
    /// scene wiring. Per-attack hit-stop is NOT here: it stays on WeaponData / SpecialData, where
    /// it has been waiting since those assets were written. These are the global dials around it.
    /// </summary>
    [CreateAssetMenu(fileName = "FeelSettings", menuName = "Kagemura/Feel Settings")]
    public class FeelSettings : ScriptableObject
    {
        public const string ResourcePath = "FeelSettings";

        [Header("Hit-stop")]
        public bool hitStopEnabled = true;
        [Tooltip("Multiplier on every hit-stop in the game. The one dial to turn if the whole " +
                 "thing feels sticky or weightless.")]
        [Range(0f, 3f)] public float hitStopScale = 1f;
        [Tooltip("Ceiling on a single freeze, after scaling. Stops stacked requests from ever " +
                 "reading as a hitch.")]
        public float maxHitStop = 0.16f;
        [Tooltip("Freeze when the player takes a hit. Longer than their own hits on purpose: " +
                 "being hit should register harder than hitting.")]
        public float playerHurtHitStop = 0.09f;
        [Tooltip("Extra freeze on the hit that kills an enemy.")]
        public float killHitStop = 0.07f;
        [Tooltip("Freeze on a successful parry.")]
        public float parryHitStop = 0.11f;

        [Header("Screen shake (trauma: 0 = still, 1 = maximum)")]
        [Tooltip("Multiplier on all shake. 0 switches it off — keep that reachable, some " +
                 "players can't tolerate shake at all.")]
        [Range(0f, 2f)] public float shakeScale = 1f;
        [Tooltip("Camera displacement at full trauma, in world units.")]
        public float maxShakeOffset = 0.35f;
        [Tooltip("How fast the shake jitters.")]
        public float shakeFrequency = 26f;
        [Tooltip("Trauma lost per second. Higher = shorter shakes.")]
        public float traumaDecay = 2.4f;
        [Tooltip("Trauma per second of an attack's hit-stop, so heavier attacks shake harder " +
                 "without a second number to author. Sword 0.05s -> 0.2.")]
        public float traumaPerSecondOfHitStop = 4f;
        public float playerHurtTrauma = 0.55f;
        public float killTrauma = 0.3f;
        public float parryTrauma = 0.35f;
        [Tooltip("A hit refused by a guard or armour.")]
        public float blockedTrauma = 0.12f;
        public float bossPhaseTrauma = 0.95f;

        [Header("Sparks (greybox particles)")]
        public bool sparksEnabled = true;
        public int hitSparkCount = 6;
        public int killSparkCount = 16;
        public float sparkSpeed = 8f;
        public float sparkLifetime = 0.22f;
        [Tooltip("Side length of one spark, in world units.")]
        public float sparkSize = 0.13f;
        [Tooltip("Half-angle of the cone sparks fly in, in degrees, around the hit direction.")]
        public float sparkSpread = 38f;
        [Tooltip("Sorting order of the sparks. Above the characters.")]
        public int sparkSortingOrder = 500;

        [Header("Spark colours")]
        public Color hitColor = new Color(0.95f, 0.9f, 0.78f);       // parchment
        public Color hurtColor = new Color(0.85f, 0.19f, 0.16f);     // vermillion
        public Color bleedColor = new Color(0.6f, 0.08f, 0.12f);
        public Color avoidColor = new Color(0.6f, 0.85f, 1f);        // the dodge tint
        public Color blockedColor = new Color(0.7f, 0.72f, 0.78f);
    }
}
