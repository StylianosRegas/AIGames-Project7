using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PlayerController — handles WASD movement, sneaking, treasure pickup, and exit.
/// Uses the Unity Input System package.
///
/// Setup:
///   - Attach to the Player GameObject.
///   - Assign a Rigidbody2D (set to Kinematic).
///   - Tag the Treasure object "Treasure" and the Exit object "Exit".
///
/// Controls:
///   WASD / Arrow Keys — move
///   Left Shift (held) — sneak (reduced speed, NoiseLevel stays low)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float WalkSpeed = 3.5f;
    public float SneakSpeed = 1.5f;

    [Header("State")]
    public bool HasTreasure { get; private set; } = false;

    /// <summary>Current movement speed this frame — read by NoiseSensor.</summary>
    public float CurrentSpeed { get; private set; } = 0f;

    /// <summary>True when the player is actively sneaking (Shift held).</summary>
    public bool IsSneaking { get; private set; } = false;

    private Rigidbody2D _rb;
    private Vector2 _moveInput;

    // Input System action references — resolved once in Awake
    private InputAction _moveAction;
    private InputAction _sneakAction;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;

        // Use the default "Player" action map that ships with the Input System.
        // If you have a custom Input Actions asset, replace these with your own bindings.
        var playerInput = GetComponent<PlayerInput>();
        if (playerInput != null)
        {
            // Prefer a PlayerInput component if one is attached
            _moveAction = playerInput.actions["Move"];
            _sneakAction = playerInput.actions.FindAction("Sneak");
        }
        else
        {
            // Fallback: create inline actions so the script works without
            // a PlayerInput component or a custom asset
            _moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/s")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/a")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/d")
                .With("Right", "<Keyboard>/rightArrow");
            _moveAction.Enable();

            _sneakAction = new InputAction("Sneak", InputActionType.Button);
            _sneakAction.AddBinding("<Keyboard>/leftShift");
            _sneakAction.AddBinding("<Keyboard>/rightShift");
            _sneakAction.Enable();
        }
    }

    void OnDestroy()
    {
        // Clean up inline actions to avoid input system leaks
        if (_moveAction != null && GetComponent<PlayerInput>() == null) _moveAction.Disable();
        if (_sneakAction != null && GetComponent<PlayerInput>() == null) _sneakAction.Disable();
    }

    void Update()
    {
        _moveInput = _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        _moveInput = Vector2.ClampMagnitude(_moveInput, 1f); // normalise analog sticks

        IsSneaking = _sneakAction?.IsPressed() ?? false;
    }

    void FixedUpdate()
    {
        float speed = IsSneaking ? SneakSpeed : WalkSpeed;
        Vector2 delta = _moveInput * speed * Time.fixedDeltaTime;
        _rb.MovePosition(_rb.position + delta);

        CurrentSpeed = delta.magnitude / Time.fixedDeltaTime; // world units / second

        // Rotate sprite to face movement direction
        if (_moveInput.sqrMagnitude > 0.01f)
            transform.up = _moveInput;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Treasure") && !HasTreasure)
        {
            HasTreasure = true;
            other.gameObject.SetActive(false);
            GameManager.Instance?.OnTreasurePickedUp();
        }

        if (other.CompareTag("Exit") && HasTreasure)
        {
            GameManager.Instance?.TriggerWin();
        }
    }
}