using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Guard states driven by Bayesian Network inference.
/// </summary>
public enum GuardState { Patrolling, Investigating, Chasing }

/// <summary>
/// GuardController — the heart of the guard AI.
///
/// Each FixedUpdate tick:
///   1. Read sensor values (NoiseSensor, LightSensor, VisionSensor).
///   2. Run Variable Elimination: P(GuardAlertState | evidence).
///   3. Transition to the state with the highest posterior probability.
///   4. Navigate using A* toward the appropriate target tile.
///
/// Required components on this GameObject:
///   - NoiseSensor
///   - LightSensor
///   - VisionSensor
///   - SpriteRenderer (for direction indicator)
///
/// Setup:
///   - Assign PatrolWaypoints (list of world positions) in Inspector.
///   - Assign PlayerController reference.
///   - Assign StateLabel (TextMeshPro) for the floating state text.
/// </summary>
[RequireComponent(typeof(NoiseSensor))]
[RequireComponent(typeof(LightSensor))]
[RequireComponent(typeof(VisionSensor))]
public class GuardController : MonoBehaviour
{
    // ── Inspector Fields ─────────────────────────────────────────────────

    [Header("References")]
    public PlayerController Player;
    public TextMeshPro StateLabel;

    [Header("Patrol Waypoints")]
    [Tooltip("World positions the guard cycles through while Patrolling")]
    public List<Transform> PatrolWaypoints;

    [Header("Movement Speed")]
    public float PatrolSpeed      = 2.0f;
    public float InvestigateSpeed = 2.8f;
    public float ChaseSpeed       = 4.5f;

    [Header("Inference")]
    [Tooltip("How often (seconds) VE is re-run. Lower = more responsive, higher = cheaper.")]
    public float InferenceInterval = 0.2f;

    [Header("Catch Distance")]
    [Tooltip("World units. If guard reaches player within this distance → game over.")]
    public float CatchDistance = 0.4f;

    // ── Public State (read by UI) ────────────────────────────────────────

    /// <summary>Current guard state (Patrolling / Investigating / Chasing).</summary>
    public GuardState CurrentState { get; private set; } = GuardState.Patrolling;

    /// <summary>Latest VE posterior: key=state name, value=probability.</summary>
    public Dictionary<string, float> LastPosterior { get; private set; }

    /// <summary>Latest evidence snapshot (for debug panel).</summary>
    public Dictionary<string, string> LastEvidence { get; private set; }

    // ── Private ──────────────────────────────────────────────────────────

    private GuardBayesNet      _bayesNet;
    private VariableElimination _ve;
    private NoiseSensor  _noiseSensor;
    private LightSensor  _lightSensor;
    private VisionSensor _visionSensor;

    private List<Vector2Int> _currentPath = new List<Vector2Int>();
    private int              _pathIndex   = 0;
    private int              _waypointIndex = 0;

    private Vector2Int _lastKnownPlayerTile;
    private float      _inferenceTimer = 0f;

    // Cache for performance: only re-run VE when evidence changes
    private string _lastEvidenceKey = "";

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    void Awake()
    {
        _bayesNet    = new GuardBayesNet();
        _ve          = new VariableElimination(_bayesNet);
        _noiseSensor = GetComponent<NoiseSensor>();
        _lightSensor = GetComponent<LightSensor>();
        _visionSensor = GetComponent<VisionSensor>();

        LastPosterior = new Dictionary<string, float>
        {
            ["Patrolling"]   = 1f,
            ["Investigating"] = 0f,
            ["Chasing"]       = 0f
        };
        LastEvidence = new Dictionary<string, string>();
    }

    void FixedUpdate()
    {
        // 1. Collect sensor readings
        var evidence = CollectEvidence();

        // 2. Run VE at the configured interval (or when evidence changes)
        _inferenceTimer += Time.fixedDeltaTime;
        string evidenceKey = EvidenceKey(evidence);
        if (_inferenceTimer >= InferenceInterval || evidenceKey != _lastEvidenceKey)
        {
            RunInference(evidence);
            _lastEvidenceKey = evidenceKey;
            _inferenceTimer  = 0f;
        }

        // 3. Update state label
        UpdateStateLabel();

        // 4. Navigate
        Navigate();

        // 5. Catch check
        CheckCatch();
    }

    // ── Inference Pipeline ───────────────────────────────────────────────

    private Dictionary<string, string> CollectEvidence()
    {
        return new Dictionary<string, string>
        {
            ["NoiseLevel"]    = _noiseSensor.NoiseLevel,
            ["LightExposure"] = _lightSensor.LightExposure,
            ["VisualContact"] = _visionSensor.VisualContact ? "True" : "False"
        };
    }

    private void RunInference(Dictionary<string, string> evidence)
    {
        LastEvidence = new Dictionary<string, string>(evidence);

        // Run Variable Elimination: P(GuardAlertState | evidence)
        var posterior = _ve.Query("GuardAlertState", evidence);
        LastPosterior = posterior;

        // Transition to highest-probability state
        GuardState newState = ArgmaxState(posterior);

        if (newState != CurrentState)
        {
            OnStateTransition(CurrentState, newState);
            CurrentState = newState;
        }

        // Remember last known player position when suspicious
        if (newState == GuardState.Investigating || newState == GuardState.Chasing)
            _lastKnownPlayerTile = WorldToTile(Player.transform.position);
    }

    private GuardState ArgmaxState(Dictionary<string, float> posterior)
    {
        float bestProb = -1f;
        string bestKey = "Patrolling";
        foreach (var kv in posterior)
            if (kv.Value > bestProb) { bestProb = kv.Value; bestKey = kv.Key; }

        return bestKey switch
        {
            "Chasing"       => GuardState.Chasing,
            "Investigating" => GuardState.Investigating,
            _               => GuardState.Patrolling
        };
    }

    private void OnStateTransition(GuardState from, GuardState to)
    {
        // Clear path on state change so we recompute for new target
        _currentPath.Clear();
        _pathIndex = 0;

        Debug.Log($"[Guard:{name}] {from} → {to}  " +
                  $"P(P={LastPosterior["Patrolling"]:F2}, " +
                  $"I={LastPosterior["Investigating"]:F2}, " +
                  $"C={LastPosterior["Chasing"]:F2})");
    }

    // ── Navigation (A*) ──────────────────────────────────────────────────

    private void Navigate()
    {
        Vector2Int guardTile = WorldToTile(transform.position);
        Vector2Int targetTile = GetTargetTile(guardTile);
        float speed = CurrentSpeedForState();

        // Recompute path if we've consumed it or have none
        if (_currentPath == null || _pathIndex >= _currentPath.Count)
        {
            _currentPath = AStar.FindPath(guardTile, targetTile);
            _pathIndex   = 0;

            // If no path exists (blocked), stay put
            if (_currentPath == null || _currentPath.Count == 0) return;
        }

        // Move toward the next waypoint in the path
        if (_pathIndex < _currentPath.Count)
        {
            Vector3 target = TileToWorld(_currentPath[_pathIndex]);
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);

            // Aim guard toward movement direction
            Vector3 dir = (target - transform.position);
            if (dir.sqrMagnitude > 0.001f)
                transform.up = dir.normalized;

            if (Vector3.Distance(transform.position, target) < 0.05f)
            {
                _pathIndex++;
                // If patrolling and reached end of sub-path, advance waypoint
                if (CurrentState == GuardState.Patrolling && _pathIndex >= _currentPath.Count)
                    AdvanceWaypoint();
            }
        }
    }

    private Vector2Int GetTargetTile(Vector2Int guardTile)
    {
        return CurrentState switch
        {
            GuardState.Chasing       => WorldToTile(Player.transform.position),
            GuardState.Investigating => _lastKnownPlayerTile,
            _                        => GetPatrolTargetTile()
        };
    }

    private Vector2Int GetPatrolTargetTile()
    {
        if (PatrolWaypoints == null || PatrolWaypoints.Count == 0)
            return WorldToTile(transform.position);
        return WorldToTile(PatrolWaypoints[_waypointIndex].position);
    }

    private void AdvanceWaypoint()
    {
        if (PatrolWaypoints == null || PatrolWaypoints.Count == 0) return;
        _waypointIndex = (_waypointIndex + 1) % PatrolWaypoints.Count;
        _currentPath.Clear();
    }

    private float CurrentSpeedForState()
    {
        return CurrentState switch
        {
            GuardState.Chasing       => ChaseSpeed,
            GuardState.Investigating => InvestigateSpeed,
            _                        => PatrolSpeed
        };
    }

    // ── Catch Check ──────────────────────────────────────────────────────

    private void CheckCatch()
    {
        if (Player == null) return;
        float dist = Vector2.Distance(transform.position, Player.transform.position);
        if (dist <= CatchDistance)
            GameManager.Instance?.TriggerGameOver();
    }

    // ── UI ───────────────────────────────────────────────────────────────

    private void UpdateStateLabel()
    {
        if (StateLabel == null) return;
        StateLabel.text = CurrentState switch
        {
            GuardState.Chasing       => "[C]",
            GuardState.Investigating => "[I]",
            _                        => "[P]"
        };
        StateLabel.color = CurrentState switch
        {
            GuardState.Chasing       => Color.red,
            GuardState.Investigating => Color.yellow,
            _                        => Color.green
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string EvidenceKey(Dictionary<string, string> ev)
    {
        return $"{ev["NoiseLevel"]}|{ev["LightExposure"]}|{ev["VisualContact"]}";
    }

    public static Vector2Int WorldToTile(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x), Mathf.RoundToInt(worldPos.y));
    }

    public static Vector3 TileToWorld(Vector2Int tile)
    {
        return new Vector3(tile.x, tile.y, 0f);
    }
}
