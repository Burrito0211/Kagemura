using Kagemura.Systems;
using UnityEngine;

namespace Kagemura.Levels
{
    /// <summary>
    /// The floor under the level. Anything with <see cref="Health"/> that falls into it dies.
    ///
    /// Needed the moment a level has a gap in it. Without one, missing a jump drops the player out
    /// of the world at terminal velocity with nothing to stop them — no death, no respawn, no end
    /// screen, just a camera following something falling forever, which looks like a crash rather
    /// than a missed jump.
    ///
    /// Enemies are caught too, so one knocked into a pit is gone rather than left alive somewhere
    /// far below still counting as a live target.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KillVolume : MonoBehaviour
    {
        private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            // In parents, since the collider that falls in may be a child hitbox.
            var health = other.GetComponentInParent<Health>();
            if (health == null) return;

            // Kill rather than damage: a dodge's i-frames should not survive a fall out of the
            // level. See Health.Kill.
            health.Kill();
        }

        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider2D>();
            if (box == null) return;

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
            Gizmos.DrawCube((Vector2)transform.position + box.offset, box.size);
        }
    }
}
