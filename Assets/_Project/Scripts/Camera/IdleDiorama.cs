using UnityEngine;

namespace Kagemura.CameraRig
{
    /// <summary>
    /// The slow-turning 3D silhouette the spec asks for behind gameplay (§3.3) — a torii gate,
    /// a pine ridge, a Fuji-esque peak. Motion only; it owns no gameplay state and is never
    /// collided with.
    ///
    /// Deliberately tiny. The "wow" in this project is meant to come from the backdrop existing
    /// at all (design pillar 3), not from anything simulated in it, and a rotation plus an
    /// optional drift is enough to stop a static diorama reading as a painted flat.
    ///
    /// The bob is unscaled by choice: the backdrop should keep breathing while the game is
    /// paused behind a menu, because a frozen sky makes a pause feel like a crash.
    /// </summary>
    public class IdleDiorama : MonoBehaviour
    {
        [Header("Rotation")]
        [Tooltip("Degrees per second. Small — this should be noticed only if you look for it.")]
        [SerializeField] private Vector3 degreesPerSecond = new Vector3(0f, 2f, 0f);

        [Header("Bob")]
        [Tooltip("Vertical drift amplitude in world units. 0 disables the bob.")]
        [SerializeField] private float bobAmplitude = 0.15f;
        [Tooltip("Full bob cycles per second.")]
        [SerializeField] private float bobFrequency = 0.08f;

        [Tooltip("Keep moving while the game is paused. On by default: a backdrop that freezes " +
                 "with a menu makes the pause read as a hang.")]
        [SerializeField] private bool ignoreTimeScale = true;

        private Vector3 _start;
        private float _phase;

        private void Awake() => _start = transform.position;

        private void Update()
        {
            float dt = ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;

            if (degreesPerSecond != Vector3.zero)
                transform.Rotate(degreesPerSecond * dt, Space.Self);

            if (bobAmplitude <= 0f) return;

            // Phase accumulated rather than read off Time.time, so toggling ignoreTimeScale or
            // pausing never snaps the diorama to a different point in the cycle.
            _phase += dt * bobFrequency * Mathf.PI * 2f;
            transform.position = _start + Vector3.up * (Mathf.Sin(_phase) * bobAmplitude);
        }
    }
}
