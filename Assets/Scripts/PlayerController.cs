using UnityEngine;

/// <summary>
/// PlayerController — handles WASD movement, sneaking, treasure pickup, and exit.
///
/// Setup:
///   - Attach to the Player GameObject.
///   - Assign a Rigidbody2D (set to Kinematic).
///   - Tag the Treasure object "Treasure" and the Exit object "Exit".
///
/// Controls:
///   WASD / Arrow Keys — move
///   Left Shift (held) — sneak (reduced speed, NoiseLevel = None)
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float WalkSpeed  = 3.5f;
    public float SneakSpeed = 1.5f;
    public float RunSpeed   = 5.5f;

    [Header("State")]
    public bool HasTreasure { get; private set; } = false;

    /// <summary>
    /// Current movement speed this frame — read by NoiseSensor.
    /// </summary>
    public float CurrentSpeed { get; private set; } = 0f;

    /// <summary>
    /// True when the player is actively sneaking (Shift held).
    /// </summary>
    public bool IsSneaking { get; private set; } = false;

    private Rigidbody2D _rb;
    private Vector2     _moveInput;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f; // top-down — no gravity
    }

    void Update()
    {
        // Read input
        _moveInput.x = Input.GetAxisRaw("Horizontal");
        _moveInput.y = Input.GetAxisRaw("Vertical");
        _moveInput.Normalize();

        IsSneaking = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    void FixedUpdate()
    {
        float speed = IsSneaking ? SneakSpeed : WalkSpeed;
        Vector2 velocity = _moveInput * speed;
        _rb.MovePosition(_rb.position + velocity * Time.fixedDeltaTime);

        CurrentSpeed = velocity.magnitude;

        // Face direction of movement
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
