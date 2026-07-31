using UnityEngine;

namespace Kagemura.CameraRig
{
    /// <summary>
    /// Build Order step 10 (spec §3.3): drifts a background layer against camera movement.
    ///
    /// Pure transform maths, so it does not care whether the layer is a 2D sprite or a slab of
    /// 3D geometry — both are non-interactive scenery either way, and gameplay stays strictly 2D.
    ///
    /// <see cref="followFactor"/> reads as "how much of the camera's travel this layer copies":
    ///   0 — world-locked. Ordinary scenery, slides past at full speed. The near layer.
    ///   1 — camera-locked. Never appears to move at all, i.e. infinitely far away. Mt. Fuji.
    ///   0.7–0.9 — distant hills and sky.
    ///   0.2–0.4 — mid-ground treeline.
    ///
    /// Vertical is separate and defaults lower, because a side-scroller's camera moves much
    /// further horizontally than vertically; matching the two makes hills lurch on every jump.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Camera to parallax against. Falls back to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Strength")]
        [Tooltip("0 = slides past at full speed (near). 1 = never appears to move (infinitely far).")]
        [Range(0f, 1f)][SerializeField] private float followFactor = 0.7f;
        [Tooltip("Vertical strength. Usually lower than horizontal — a side-scroller's camera " +
                 "barely moves in Y, so matching them makes the sky lurch on every jump.")]
        [Range(0f, 1f)][SerializeField] private float verticalFactor = 0.35f;

        [Tooltip("Re-read the layer's authored position every frame in the editor, so nudging it " +
                 "in the scene view while not playing behaves the way you'd expect.")]
        [SerializeField] private bool liveEditInEditor = true;

        private Vector3 _layerStart;
        private Vector3 _cameraStart;
        private bool _anchored;

        private void OnEnable() => Anchor();

        /// <summary>
        /// Pin the layer's rest position and the camera position it corresponds to. Everything
        /// after this is measured as travel away from that pair, so the layer sits exactly where
        /// it was authored when the camera is back at its starting point.
        /// </summary>
        public void Anchor()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            if (cameraTransform == null)
            {
                _anchored = false;
                return;
            }

            _layerStart = transform.position;
            _cameraStart = cameraTransform.position;
            _anchored = true;
        }

        private void LateUpdate()
        {
            // After the camera has moved, not before — otherwise the background lags the
            // gameplay camera by a frame and the whole scene shears on fast movement.
            if (!_anchored)
            {
                Anchor();
                if (!_anchored) return;
            }

#if UNITY_EDITOR
            if (liveEditInEditor && !Application.isPlaying)
            {
                Anchor();
                return;
            }
#endif

            Vector3 travel = cameraTransform.position - _cameraStart;

            transform.position = new Vector3(
                _layerStart.x + travel.x * followFactor,
                _layerStart.y + travel.y * verticalFactor,
                _layerStart.z);
        }
    }
}
