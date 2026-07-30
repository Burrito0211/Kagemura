using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kagamura.CameraRig
{
    /// <summary>
    /// Build Order step 10 (spec §3.3): the separate background camera that 3D atmosphere is
    /// drawn through, composited behind the 2D gameplay camera.
    ///
    /// The point of the split is isolation. Gameplay stays strictly 2D — 2D physics, 2D colliders,
    /// sprites — and nothing in it can see, collide with, or be occluded by the 3D behind it,
    /// because the two cameras cull disjoint layers. Torii gates and pine ridges can get as
    /// elaborate as they like without a single gameplay script learning that 3D exists.
    ///
    /// Attach to the background camera. It owns three jobs:
    ///   - Cull: this camera draws only <see cref="backgroundLayers"/>, and the gameplay camera
    ///     is made to draw everything except them.
    ///   - Follow: it trails the gameplay camera at <see cref="followFactor"/>, which is the
    ///     parallax for 3D geometry. A perspective camera also gives depth parallax for free,
    ///     so this mostly controls how far the whole diorama drifts.
    ///   - Composite: wires the URP camera stack so this renders first and gameplay draws on top.
    ///
    /// Perspective is the default here even though gameplay is orthographic. That is the whole
    /// reason for a second camera: perspective is what makes a 3D backdrop read as depth rather
    /// than as flat painted layers.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BackgroundRig : MonoBehaviour
    {
        [Header("Cameras")]
        [Tooltip("The 2D gameplay camera. Falls back to Camera.main.")]
        [SerializeField] private Camera gameplayCamera;

        [Header("Layers")]
        [Tooltip("Layers holding 3D background geometry. This camera draws only these; the " +
                 "gameplay camera is made to draw everything but these.")]
        [SerializeField] private LayerMask backgroundLayers;
        [Tooltip("Take the background layers out of the gameplay camera's culling mask too. " +
                 "Off only if something else already manages that mask.")]
        [SerializeField] private bool excludeFromGameplayCamera = true;

        [Header("Follow")]
        [Tooltip("How much of the gameplay camera's travel the backdrop copies. 1 = locked to " +
                 "the camera and apparently infinitely distant; 0 = slides past like scenery.")]
        [Range(0f, 1f)][SerializeField] private float followFactor = 0.85f;
        [Range(0f, 1f)][SerializeField] private float verticalFactor = 0.4f;
        [Tooltip("World offset of the backdrop from the gameplay camera, before follow is applied.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -40f);

        [Header("URP Camera Stack")]
        [Tooltip("Wire this camera as the stack Base and the gameplay camera as an Overlay on " +
                 "top of it. Turn off to set the stack up by hand in the inspector instead.")]
        [SerializeField] private bool configureStack = true;
        [Tooltip("Colour behind everything — the sky. Shifts per season (spec §4).")]
        [SerializeField] private Color skyColor = new Color(0.16f, 0.18f, 0.35f);

        private Camera _camera;
        private Vector3 _cameraStart;
        private bool _anchored;

        private void Awake()
        {
            _camera = GetComponent<Camera>();

            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (gameplayCamera == this._camera) gameplayCamera = null;

            if (gameplayCamera == null)
            {
                Debug.LogError("[BackgroundRig] No gameplay camera assigned and Camera.main did " +
                               "not resolve to a different camera. The backdrop will not follow " +
                               "or composite.", this);
                return;
            }

            ApplyCulling();
            if (configureStack) ApplyStack();

            _cameraStart = gameplayCamera.transform.position;
            _anchored = true;
        }

        /// <summary>
        /// Split the world between the two cameras. Done in code rather than left to the
        /// inspector because the two masks have to be exact complements — any layer both cameras
        /// draw appears twice, and the duplicate is only visible as a faint double-image that is
        /// miserable to track down.
        /// </summary>
        private void ApplyCulling()
        {
            if (backgroundLayers.value == 0)
            {
                Debug.LogWarning("[BackgroundRig] No background layers set, so this camera has " +
                                 "nothing to draw. Make a 'Background' layer, put the 3D scenery " +
                                 "on it, and select it here.", this);
                return;
            }

            _camera.cullingMask = backgroundLayers.value;

            if (excludeFromGameplayCamera)
                gameplayCamera.cullingMask &= ~backgroundLayers.value;
        }

        /// <summary>
        /// URP composites through camera stacks rather than camera depth: the Base camera renders,
        /// then each Overlay draws on top of it. So the backdrop is the Base and gameplay is the
        /// Overlay — the reverse of how the depth ordering reads.
        ///
        /// Bails out rather than half-configuring if either camera has no URP data, since a
        /// partly-built stack renders black and is worse than an untouched one.
        /// </summary>
        private void ApplyStack()
        {
            var backdropData = _camera.GetUniversalAdditionalCameraData();
            var gameplayData = gameplayCamera.GetUniversalAdditionalCameraData();

            if (backdropData == null || gameplayData == null)
            {
                Debug.LogWarning("[BackgroundRig] One of the cameras has no URP camera data, so " +
                                 "the stack was left alone. Set it up in the inspector: this " +
                                 "camera Base, the gameplay camera Overlay in its stack.", this);
                return;
            }

            backdropData.renderType = CameraRenderType.Base;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = skyColor;

            gameplayData.renderType = CameraRenderType.Overlay;

            if (!backdropData.cameraStack.Contains(gameplayCamera))
                backdropData.cameraStack.Add(gameplayCamera);
        }

        private void LateUpdate()
        {
            // After CameraFollow has moved the gameplay camera, so the backdrop never trails it
            // by a frame.
            if (!_anchored || gameplayCamera == null) return;

            Vector3 travel = gameplayCamera.transform.position - _cameraStart;

            transform.position = new Vector3(
                _cameraStart.x + offset.x + travel.x * followFactor,
                _cameraStart.y + offset.y + travel.y * verticalFactor,
                _cameraStart.z + offset.z);
        }
    }
}
