using System.Collections;
using System.Collections.Generic;
using Kagemura.Enemies;
using Kagemura.Player;
using Kagemura.Systems;
using UnityEngine;

namespace Kagemura.UI
{
    /// <summary>
    /// Game over and the ending, in one screen (spec §5).
    ///
    /// Both are the same object — a title, Retry, and Quit to menu — differing only in a word and
    /// in what raised them, so two scripts would be one script and a copy. The outcome is set
    /// when it opens.
    ///
    /// It wires itself to the player's death and, if a boss is present, to the boss dying. That
    /// keeps the ending condition next to the thing that ends it, rather than in a GameManager
    /// that has to be told about every scene.
    /// </summary>
    public class EndScreen : GreyboxMenu
    {
        public enum Outcome { Defeat, Victory }

        [Header("Wiring")]
        [Tooltip("Player health. Found from the PlayerController if left empty.")]
        [SerializeField] private Health playerHealth;
        [Tooltip("Boss. Found in the scene if left empty; without one there is no victory here.")]
        [SerializeField] private BossController boss;

        [Header("Timing")]
        [Tooltip("Beat before the screen appears, so the death or the kill gets to land first.")]
        [SerializeField] private float delay = 1.1f;

        [Header("Routes")]
        [SerializeField] private string mainMenuScene = SceneRoutes.DefaultMainMenu;

        [Header("Wording")]
        [SerializeField] private string defeatTitle = "FALLEN";
        [SerializeField] private string victoryTitle = "THE BLADE RESTS";

        private Outcome _outcome = Outcome.Defeat;
        private bool _resolved;

        protected override string Title => defeatTitle;

        protected override void Awake()
        {
            if (playerHealth == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) playerHealth = player.GetComponent<Health>();
            }

            if (boss == null) boss = FindFirstObjectByType<BossController>();

            base.Awake();
        }

        private void OnEnable()
        {
            if (playerHealth != null) playerHealth.OnDied += HandlePlayerDied;
            if (boss != null)
            {
                var bossHealth = boss.GetComponent<Health>();
                if (bossHealth != null) bossHealth.OnDied += HandleBossDied;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (playerHealth != null) playerHealth.OnDied -= HandlePlayerDied;
            if (boss != null)
            {
                var bossHealth = boss.GetComponent<Health>();
                if (bossHealth != null) bossHealth.OnDied -= HandleBossDied;
            }
        }

        private void HandlePlayerDied() => Resolve(Outcome.Defeat);
        private void HandleBossDied() => Resolve(Outcome.Victory);

        /// <summary>
        /// First outcome wins. A player and a boss can die within the same frame — a killing blow
        /// traded for a killing blow — and without this the screen would show one title and then
        /// flip to the other.
        /// </summary>
        public void Resolve(Outcome outcome)
        {
            if (_resolved) return;
            _resolved = true;

            _outcome = outcome;
            StartCoroutine(ShowAfterDelay());
        }

        private IEnumerator ShowAfterDelay()
        {
            // Unscaled, so the delay still elapses if something else has already frozen the game.
            yield return new WaitForSecondsRealtime(delay);

            SetTitle(_outcome == Outcome.Victory ? victoryTitle : defeatTitle);
            Open();
        }

        protected override IEnumerable<MenuItem> BuildItems()
        {
            yield return new MenuItem("Retry", () => { ReleasePause(); SceneRoutes.Reload(); });
            yield return new MenuItem("Quit to Menu",
                () => { ReleasePause(); SceneRoutes.Load(mainMenuScene); });
        }
    }
}
