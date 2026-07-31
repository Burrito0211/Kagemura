using UnityEngine;

namespace Kagemura.Narrative
{
    /// <summary>
    /// Fires a story beat when the player walks into it (spec §3.2, delivered at natural pacing
    /// breaks). Put it on a trigger collider after a hard fight or in front of a boss door.
    ///
    /// Fires once by default. A beat that replays every time the player backtracks stops being a
    /// story beat and becomes an obstacle.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] private DialogueData dialogue;

        [Tooltip("Which layers set this off. Set to your Player layer.")]
        [SerializeField] private LayerMask triggerLayers;

        [Tooltip("Play only the first time. Off only for something meant to repeat.")]
        [SerializeField] private bool once = true;

        [Tooltip("Destroy this object once it has played. Tidies the hierarchy; leave off if " +
                 "something else still references it.")]
        [SerializeField] private bool destroyAfterPlaying;

        private bool _played;

        private void Reset()
        {
            // A trigger that is not a trigger silently becomes a wall the player walks into.
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null && !col.isTrigger)
                Debug.LogWarning($"[{name}] Collider is not set to Is Trigger, so the player " +
                                 "will collide with this instead of walking through it.", this);

            if (dialogue == null)
                Debug.LogWarning($"[{name}] No DialogueData assigned — this trigger does nothing.", this);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_played && once) return;
            if (dialogue == null) return;
            if ((triggerLayers.value & (1 << other.gameObject.layer)) == 0) return;

            var ui = DialogueUI.Instance;
            if (ui == null)
            {
                Debug.LogWarning($"[{name}] No DialogueUI in the scene, so the beat was skipped.", this);
                return;
            }

            // Checked rather than assumed: two triggers close together would otherwise have the
            // second silently swallowed, and marking it played would lose the beat for good.
            if (ui.IsPlaying) return;

            ui.Play(dialogue);
            _played = true;

            if (destroyAfterPlaying) Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.95f, 0.9f, 0.78f, 0.35f);
            var col = GetComponent<Collider2D>();
            if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
    }
}
