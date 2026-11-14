using Kagemura.Enemies;
using Kagemura.Player;
using Kagemura.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kagemura.Feel
{
    /// <summary>
    /// Build Order step 15, the juice: hit-stop, screen shake and hit sparks.
    ///
    /// One listener for the whole game. It hears every hit through Health's static events and
    /// decides how that hit should feel, so no weapon, enemy or scene knows any of this exists
    /// (spec §6, event-driven). It installs itself before the first scene loads and survives
    /// scene changes — there is nothing to place in a level and nothing to forget in the next one.
    ///
    /// What each event gets:
    ///   player lands a hit   — the attack's own hit-stop (WeaponData / SpecialData), a shake
    ///                          scaled from it, sparks along the knockback
    ///   bleed tick           — a couple of dark sparks only; a tick is not a blow
    ///   enemy dies to a hit  — a longer freeze, a burst, a bigger shake
    ///   player is hit        — the hardest freeze and shake in normal play
    ///   dodge / parry        — sparks in the dodge colour; a parry also freezes
    ///   guard blocks a hit   — grey sparks thrown back at the attacker, a small shake
    ///   boss enters phase 2  — the biggest shake in the game
    /// </summary>
    public sealed class CombatFeel : MonoBehaviour
    {
        // Hit-stop is a time scale of this, not 0. The value doubles as a signature: a time
        // scale of exactly this can only have come from here, which is what makes it safe to
        // share Time.timeScale with the menus (see TickHitStop).
        private const float FrozenTimeScale = 0.02f;

        // Avoided hits can arrive every frame (a live hitbox against i-frames); one spray per
        // this many seconds is plenty.
        private const float AvoidFeedbackInterval = 0.08f;

        private static CombatFeel _instance;

        private FeelSettings _settings;
        private SparkPool _sparks;

        private bool _stopping;
        private float _stopUntil;          // unscaled time
        private float _trauma;
        private float _nextAvoidFeedback;  // unscaled time

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;

            var go = new GameObject("Combat Feel");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CombatFeel>();
        }

        private void Awake()
        {
            _settings = Resources.Load<FeelSettings>(FeelSettings.ResourcePath);
            if (_settings == null)
            {
                // Missing asset: run on the defaults rather than silently losing all feedback.
                Debug.LogWarning("[CombatFeel] No FeelSettings asset in a Resources folder — " +
                                 "using built-in defaults.");
                _settings = ScriptableObject.CreateInstance<FeelSettings>();
            }

            _sparks = new SparkPool(transform, _settings.sparkSortingOrder);
        }

        private void OnEnable()
        {
            Health.AnyDamaged += HandleDamaged;
            Health.AnyDamageAvoided += HandleDamageAvoided;
            Health.AnyDied += HandleDied;
            BossController.AnyPhaseChanged += HandleBossPhaseChanged;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= HandleDamaged;
            Health.AnyDamageAvoided -= HandleDamageAvoided;
            Health.AnyDied -= HandleDied;
            BossController.AnyPhaseChanged -= HandleBossPhaseChanged;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            // Never leave the game in slow motion or the camera off-centre behind us.
            if (IsFrozenScale) Time.timeScale = 1f;
            _stopping = false;
            ScreenShake.Offset = Vector2.zero;
        }

        private void Update()
        {
            TickHitStop();
            TickShake();
            _sparks.Tick(Time.deltaTime);
        }

        // --- Events -----------------------------------------------------------------------

        private void HandleDamaged(Health victim, DamageInfo info)
        {
            Vector2 dir = info.KnockbackDir.sqrMagnitude > 0.0001f ? info.KnockbackDir.normalized : Vector2.up;

            if (IsPlayer(victim))
            {
                RequestHitStop(_settings.playerHurtHitStop);
                AddTrauma(_settings.playerHurtTrauma);
                Spray(info.HitPoint, dir, _settings.hitSparkCount + 2, _settings.hurtColor);
                return;
            }

            // No hit-stop authored means damage over time (bleed), not a blow.
            if (info.HitStop <= 0f)
            {
                Spray(victim.transform.position, Vector2.up, 2, _settings.bleedColor, 0.5f);
                return;
            }

            RequestHitStop(info.HitStop);
            AddTrauma(info.HitStop * _settings.traumaPerSecondOfHitStop);
            Spray(info.HitPoint, dir, _settings.hitSparkCount, _settings.hitColor);
        }

        private void HandleDamageAvoided(Health victim, DamageInfo info)
        {
            if (info.HitStop <= 0f && !IsPlayer(victim)) return;    // a bleed tick on a guard

            if (IsPlayer(victim))
            {
                // Only a real read gets feedback. The automatic window after being hit also
                // lands here, and celebrating that would reward standing in an attack.
                if (!victim.IsInvulnerableFromAbility) return;

                bool parried = victim.TryGetComponent<ParryController>(out var parry) && parry.IsParrying;
                if (parried)
                {
                    RequestHitStop(_settings.parryHitStop);
                    AddTrauma(_settings.parryTrauma);
                }

                if (!AvoidFeedbackReady()) return;
                Burst(victim.transform.position, parried ? _settings.hitSparkCount + 4 : _settings.hitSparkCount,
                      _settings.avoidColor);
                return;
            }

            // An enemy's guard or armour refused the hit: throw the sparks back at the attacker.
            if (!AvoidFeedbackReady()) return;
            Vector2 back = info.KnockbackDir.sqrMagnitude > 0.0001f
                ? new Vector2(-info.KnockbackDir.x, Mathf.Abs(info.KnockbackDir.y)).normalized
                : Vector2.up;
            AddTrauma(_settings.blockedTrauma);
            Spray(info.HitPoint, back, _settings.hitSparkCount, _settings.blockedColor);
        }

        private void HandleDied(Health victim, bool killedByHit)
        {
            // Falling out of the level is not a kill: no freeze, no burst.
            if (!killedByHit || IsPlayer(victim)) return;

            RequestHitStop(_settings.killHitStop);
            AddTrauma(_settings.killTrauma);
            Burst(victim.transform.position, _settings.killSparkCount, _settings.hitColor);
        }

        private void HandleBossPhaseChanged(BossController boss, int phase)
        {
            if (phase < 2) return;

            AddTrauma(_settings.bossPhaseTrauma);
            if (boss != null)
                Burst(boss.transform.position, _settings.killSparkCount, _settings.hurtColor);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A new level starts clean: no leftover freeze, shake or sparks from the last one.
            _stopping = false;
            if (IsFrozenScale) Time.timeScale = 1f;
            _trauma = 0f;
            ScreenShake.Offset = Vector2.zero;
            _sparks.Clear();
        }

        private static bool IsPlayer(Health health) => health.TryGetComponent<PlayerController>(out _);

        private bool AvoidFeedbackReady()
        {
            if (Time.unscaledTime < _nextAvoidFeedback) return false;
            _nextAvoidFeedback = Time.unscaledTime + AvoidFeedbackInterval;
            return true;
        }

        // --- Hit-stop ---------------------------------------------------------------------

        private static bool IsFrozenScale => Mathf.Abs(Time.timeScale - FrozenTimeScale) < 0.0005f;

        /// <summary>
        /// Freeze the game for a moment. Overlapping requests extend to the longest rather than
        /// adding up, so a swing that catches three enemies freezes once, not three times.
        /// </summary>
        private void RequestHitStop(float seconds)
        {
            if (!_settings.hitStopEnabled) return;

            seconds = Mathf.Min(seconds * _settings.hitStopScale, _settings.maxHitStop);
            if (seconds <= 0f) return;

            if (_stopping)
            {
                _stopUntil = Mathf.Max(_stopUntil, Time.unscaledTime + seconds);
                return;
            }

            // Only ever start from normal speed. If a menu or a story beat already owns the
            // time scale, the hit simply doesn't freeze.
            if (!Mathf.Approximately(Time.timeScale, 1f)) return;

            _stopping = true;
            _stopUntil = Time.unscaledTime + seconds;
            Time.timeScale = FrozenTimeScale;
        }

        /// <summary>
        /// End the freeze, and repair the time scale if a menu carried our value away.
        ///
        /// The pause menu, the settings window and the dialogue box all remember the time scale
        /// they found and put it back when they close. Open one during a hit-stop and what it
        /// remembers is the frozen value: it would close and restore a game running at 2%, long
        /// after the hit-stop that caused it is over. So this only ever restores a scale that
        /// is still exactly ours, and outside a hit-stop treats that exact value as a leftover
        /// and clears it. Nothing else in the game uses it, and no menu has to know about this.
        /// </summary>
        private void TickHitStop()
        {
            if (_stopping)
            {
                if (Time.unscaledTime < _stopUntil) return;
                _stopping = false;
            }

            if (IsFrozenScale) Time.timeScale = 1f;
        }

        // --- Screen shake -----------------------------------------------------------------

        private void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount * _settings.shakeScale);
        }

        private void TickShake()
        {
            if (_trauma <= 0f)
            {
                ScreenShake.Offset = Vector2.zero;
                return;
            }

            // Unscaled: the shake has to play through the hit-stop that came with it.
            _trauma = Mathf.Max(0f, _trauma - _settings.traumaDecay * Time.unscaledDeltaTime);

            // Squared, so small hits barely move the camera and big ones kick. Perlin rather
            // than random so the motion is continuous instead of a per-frame teleport.
            float strength = _trauma * _trauma * _settings.maxShakeOffset;
            float t = Time.unscaledTime * _settings.shakeFrequency;
            ScreenShake.Offset = new Vector2(
                (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f * strength,
                (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f * strength);
        }

        // --- Sparks -----------------------------------------------------------------------

        private void Spray(Vector2 position, Vector2 direction, int count, Color color, float speedScale = 1f)
        {
            if (!_settings.sparksEnabled) return;
            _sparks.Emit(position, direction, count, _settings.sparkSpread,
                         _settings.sparkSpeed * speedScale, _settings.sparkLifetime,
                         _settings.sparkSize, color);
        }

        private void Burst(Vector2 position, int count, Color color)
        {
            if (!_settings.sparksEnabled) return;
            _sparks.Emit(position, Vector2.up, count, 180f, _settings.sparkSpeed,
                         _settings.sparkLifetime * 1.4f, _settings.sparkSize, color);
        }
    }
}
