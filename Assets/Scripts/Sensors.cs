using UnityEngine;

// ────────────────────────────────────────────────────────────────────────────
// NoiseSensor
// Computes NoiseLevel from the player's movement speed each tick.
//
// Setup:  Attach to the same GameObject as GuardController.
//         Assign the PlayerController reference in the Inspector.
//
// NoiseLevel values: "None" | "Low" | "High"
// ────────────────────────────────────────────────────────────────────────────
public class NoiseSensor : MonoBehaviour
{
    [Header("References")]
    public PlayerController Player;

    [Header("Tuning")]
    [Tooltip("Player speed below this → None")]
    public float StillThreshold  = 0.05f;
    [Tooltip("Player speed below this → Low (above StillThreshold)")]
    public float WalkThreshold   = 2.5f;
    // Above WalkThreshold → High

    [Header("Detection Range")]
    [Tooltip("Guard only hears noise within this world-unit radius")]
    public float HearingRadius = 10f;

    // Read by GuardController each tick
    public string NoiseLevel { get; private set; } = "None";

    void Update()
    {
        if (Player == null) { NoiseLevel = "None"; return; }

        float dist = Vector2.Distance(transform.position, Player.transform.position);
        if (dist > HearingRadius) { NoiseLevel = "None"; return; }

        float speed = Player.CurrentSpeed;

        if (speed < StillThreshold)
            NoiseLevel = "None";
        else if (speed < WalkThreshold)
            NoiseLevel = "Low";
        else
            NoiseLevel = "High";
    }
}

// ────────────────────────────────────────────────────────────────────────────
// LightSensor
// Computes LightExposure based on whether the player's current tile is lit.
// Tiles tagged "LitFull" = Full, "LitPartial" = Partial, else = None.
//
// Setup:  Attach to the same GameObject as GuardController.
//         The sensor does a 2D overlap check at the player's position
//         to find LightZone colliders.
// ────────────────────────────────────────────────────────────────────────────
public class LightSensor : MonoBehaviour
{
    [Header("References")]
    public PlayerController Player;

    [Header("Detection Range")]
    [Tooltip("Guard can only assess light within this radius")]
    public float DetectionRadius = 15f;

    // Read by GuardController each tick
    public string LightExposure { get; private set; } = "None";

    void Update()
    {
        if (Player == null) { LightExposure = "None"; return; }

        float dist = Vector2.Distance(transform.position, Player.transform.position);
        if (dist > DetectionRadius) { LightExposure = "None"; return; }

        // Check which LightZone colliders overlap the player's position
        Collider2D[] hits = Physics2D.OverlapPointAll(Player.transform.position);
        string bestLight = "None";

        foreach (var hit in hits)
        {
            if (hit.CompareTag("LitFull"))
            {
                bestLight = "Full";
                break;               // Full is highest priority
            }
            if (hit.CompareTag("LitPartial"))
                bestLight = "Partial";
        }

        LightExposure = bestLight;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// VisionSensor
// Determines VisualContact via a 2D raycast from the guard toward the player.
// Line of sight is blocked by any collider tagged "Wall".
//
// Setup:  Attach to the same GameObject as GuardController.
//         Set VisionRange and VisionAngle in the Inspector.
//         The guard's forward direction is transform.up (for top-down 2D).
// ────────────────────────────────────────────────────────────────────────────
public class VisionSensor : MonoBehaviour
{
    [Header("References")]
    public PlayerController Player;

    [Header("Cone of Vision")]
    [Tooltip("Maximum distance the guard can see")]
    public float VisionRange = 8f;
    [Tooltip("Half-angle of the vision cone in degrees")]
    public float VisionHalfAngle = 45f;

    [Header("Layer Masks")]
    public LayerMask WallLayer;

    // Read by GuardController each tick
    public bool VisualContact { get; private set; } = false;

    void Update()
    {
        if (Player == null) { VisualContact = false; return; }

        Vector2 toPlayer = (Vector2)(Player.transform.position - transform.position);
        float dist = toPlayer.magnitude;

        // Range check
        if (dist > VisionRange) { VisualContact = false; return; }

        // Angle check (guard faces transform.up in top-down 2D)
        float angle = Vector2.Angle(transform.up, toPlayer);
        if (angle > VisionHalfAngle) { VisualContact = false; return; }

        // Line of sight check
        RaycastHit2D hit = Physics2D.Raycast(transform.position, toPlayer.normalized, dist, WallLayer);
        VisualContact = (hit.collider == null); // no wall in the way
    }

    // Draw vision cone in Scene view for debugging
    void OnDrawGizmosSelected()
    {
        Gizmos.color = VisualContact ? Color.red : Color.yellow;
        Vector3 forward = transform.up * VisionRange;
        Gizmos.DrawLine(transform.position, transform.position + forward);

        float halfAngleRad = VisionHalfAngle * Mathf.Deg2Rad;
        Vector3 left  = Quaternion.Euler(0, 0,  VisionHalfAngle) * transform.up * VisionRange;
        Vector3 right = Quaternion.Euler(0, 0, -VisionHalfAngle) * transform.up * VisionRange;
        Gizmos.DrawLine(transform.position, transform.position + left);
        Gizmos.DrawLine(transform.position, transform.position + right);
    }
}
