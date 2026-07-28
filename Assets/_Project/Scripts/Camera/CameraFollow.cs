using UnityEngine;

namespace Kagamura.CameraRig
{
    /// <summary>
    /// Build Order step 1: smooth camera follow for 2D side-scrolling.
    /// Uses SmoothDamp for a non-jerky follow with a configurable offset.
    /// Vertical follow can be softened/disabled so small hops don't jitter the frame.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Framing")]
        [Tooltip("World-space offset from the target. Z should stay negative for a 2D camera.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);

        [Header("Smoothing")]
        [Tooltip("Approx time (seconds) to reach the target. Lower = tighter follow.")]
        [SerializeField] private float smoothTime = 0.15f;

        [Tooltip("If off, the camera holds a fixed Y and only tracks horizontally.")]
        [SerializeField] private bool followVertical = true;
        [Tooltip("Fixed Y used when 'Follow Vertical' is off.")]
        [SerializeField] private float fixedY = 0f;

        private Vector3 _velocity;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            if (!followVertical)
                desired.y = fixedY + offset.y;

            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        /// <summary>Assign the follow target at runtime (e.g., after spawning the player).</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;
    }
}
