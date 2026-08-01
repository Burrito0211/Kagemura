using Kagemura.Player;
using Kagemura.UI;
using UnityEngine;

namespace Kagemura.Levels
{
    /// <summary>
    /// The end of a level: walk into it and the next scene loads (spec §3.1 — the levels are
    /// linear, so there is nothing to decide here).
    ///
    /// Routes through <see cref="SceneRoutes"/> rather than SceneManager directly, so an unbuilt
    /// scene says so instead of failing with an error that names the scene and never says why.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class LevelExit : MonoBehaviour
    {
        [Tooltip("Scene to load. Must be in Build Settings.")]
        [SerializeField] private string nextScene;

        private bool _triggered;

        /// <summary>Set the destination from a builder, before the scene is saved.</summary>
        public void SetNextScene(string scene) => nextScene = scene;

        private void Reset()
        {
            // A trigger, not a wall — the point is to walk through it.
            GetComponent<BoxCollider2D>().isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Once only. The player's collider can re-enter within the frames before the load
            // actually happens, and a second Load call mid-transition is at best wasted work.
            if (_triggered) return;

            // GetComponentInParent, not GetComponent: whatever collider hits this may be a child
            // hitbox rather than the player's body.
            if (other.GetComponentInParent<PlayerController>() == null) return;

            if (string.IsNullOrEmpty(nextScene))
            {
                Debug.LogWarning("[LevelExit] Reached, but no next scene is set. Nothing to load.",
                                 this);
                return;
            }

            _triggered = true;
            SceneRoutes.Load(nextScene);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;

            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.35f);
            Gizmos.DrawWireCube((Vector2)transform.position + box.offset, box.size);
        }
    }
}
