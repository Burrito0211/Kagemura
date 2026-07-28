using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagamura.Player
{
    /// <summary>
    /// Build Order step 1: run, jump, single mid-air jump, fall.
    /// Rigidbody2D-based controller tuned for a tight, non-floaty platforming feel
    /// (fast fall + short-hop gravity) as called for in the spec (§2.1).
    /// Reads the project-wide Input System actions (Player/Move, Player/Jump) so no
    /// inspector wiring of inputs is required.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Run")]
        [Tooltip("Top horizontal speed in units/second.")]
        [SerializeField] private float moveSpeed = 8f;
        [Tooltip("How quickly the player reaches top speed. Higher = snappier.")]
        [SerializeField] private float acceleration = 90f;
        [Tooltip("How quickly the player stops. Higher = less slide.")]
        [SerializeField] private float deceleration = 110f;

        [Header("Jump")]
        [Tooltip("Initial upward velocity applied on jump.")]
        [SerializeField] private float jumpForce = 15f;
        [Tooltip("Extra mid-air jumps after leaving the ground. 1 = double jump, 0 = ground jump only.")]
        [SerializeField] private int maxAirJumps = 1;
        [Tooltip("Grace period after leaving a ledge where a jump still counts as grounded.")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("How early a jump press is remembered before landing.")]
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Gravity Feel")]
        [Tooltip("Gravity multiplier while falling. >1 = snappier, less floaty descent.")]
        [SerializeField] private float fallMultiplier = 2.5f;
        [Tooltip("Gravity multiplier while rising but jump released (short hop).")]
        [SerializeField] private float lowJumpMultiplier = 2f;

        [Header("Ground Check")]
        [Tooltip("Local offset from the player's origin to the feet.")]
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.9f);
        [SerializeField] private float groundCheckRadius = 0.2f;
        [Tooltip("Which layers count as ground. Set this to your Ground layer in the inspector.")]
        [SerializeField] private LayerMask groundLayer;

        private Rigidbody2D _rb;
        private InputAction _moveAction;
        private InputAction _jumpAction;

        private float _moveInput;
        private bool _isGrounded;
        private int _airJumpsRemaining;
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private int _facing = 1;

        /// <summary>Current horizontal facing: +1 = right, -1 = left. Read by weapons/combat.</summary>
        public int Facing => _facing;

        /// <summary>True while standing on ground (read by combat/other systems).</summary>
        public bool IsGrounded => _isGrounded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _rb.freezeRotation = true;
            // We drive gravity feel manually via multipliers below.
            _rb.gravityScale = 1f;
        }

        private void OnEnable()
        {
            // Unity 6 project-wide actions — set under Project Settings > Input System Package.
            _moveAction = InputSystem.actions?.FindAction("Move");
            _jumpAction = InputSystem.actions?.FindAction("Jump");

            if (_moveAction == null || _jumpAction == null)
            {
                Debug.LogError("[PlayerController] Could not find 'Move'/'Jump' actions. " +
                               "Ensure InputSystem_Actions is set as the project-wide action asset " +
                               "(Project Settings > Input System Package).", this);
                return;
            }

            _moveAction.Enable();
            _jumpAction.Enable();
        }

        private void Update()
        {
            if (_moveAction == null || _jumpAction == null) return;

            // --- Read input ---
            _moveInput = _moveAction.ReadValue<Vector2>().x;

            if (_jumpAction.WasPressedThisFrame())
                _jumpBufferCounter = jumpBufferTime;

            // --- Timers ---
            _jumpBufferCounter -= Time.deltaTime;
            _coyoteCounter = _isGrounded ? coyoteTime : _coyoteCounter - Time.deltaTime;

            // --- Sprite facing ---
            if (Mathf.Abs(_moveInput) > 0.01f)
            {
                int dir = _moveInput > 0f ? 1 : -1;
                if (dir != _facing)
                {
                    _facing = dir;
                    Vector3 s = transform.localScale;
                    s.x = Mathf.Abs(s.x) * _facing;
                    transform.localScale = s;
                }
            }
        }

        private void FixedUpdate()
        {
            GroundCheck();
            ApplyHorizontalMovement();
            HandleJump();
            ApplyBetterGravity();
        }

        private void GroundCheck()
        {
            Vector2 point = (Vector2)transform.position + groundCheckOffset;
            bool wasGrounded = _isGrounded;
            _isGrounded = Physics2D.OverlapCircle(point, groundCheckRadius, groundLayer);

            if (_isGrounded && !wasGrounded)
                _airJumpsRemaining = maxAirJumps; // refill air jumps on landing
        }

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = _moveInput * moveSpeed;
            float rate = Mathf.Abs(targetSpeed) > 0.01f ? acceleration : deceleration;
            float newX = Mathf.MoveTowards(_rb.linearVelocity.x, targetSpeed, rate * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
        }

        private void HandleJump()
        {
            if (_jumpBufferCounter <= 0f) return;

            bool canGroundJump = _coyoteCounter > 0f;
            bool canAirJump = !canGroundJump && _airJumpsRemaining > 0;

            if (!canGroundJump && !canAirJump) return;

            if (canAirJump) _airJumpsRemaining--;

            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);
            _jumpBufferCounter = 0f;
            _coyoteCounter = 0f;
        }

        private void ApplyBetterGravity()
        {
            bool jumpHeld = _jumpAction != null && _jumpAction.IsPressed();
            float extra = 0f;

            if (_rb.linearVelocity.y < 0f)
                extra = fallMultiplier - 1f;                 // falling: heavier
            else if (_rb.linearVelocity.y > 0f && !jumpHeld)
                extra = lowJumpMultiplier - 1f;              // rising + released: short hop

            if (extra > 0f)
                _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * extra * Time.fixedDeltaTime;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector2 point = (Vector2)transform.position + groundCheckOffset;
            Gizmos.DrawWireSphere(point, groundCheckRadius);
        }
    }
}
