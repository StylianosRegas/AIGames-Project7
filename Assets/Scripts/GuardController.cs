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
///   - NoiseSensor, LightSensor, VisionSensor
///
/// Setup:
///   - Assign PatrolWaypoints (list of Transforms) in Inspector.
///   - Assign PlayerController reference in Inspector.
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
    public float PatrolSpeed = 2.0f;
    public float InvestigateSpeed = 2.8f;
    public float ChaseSpeed = 4.5f;

    [Header("Inference")]
    [Tooltip("How often (seconds) VE is re-run. Lower = more responsive, higher = cheaper.")]
    public float InferenceInterval = 0.2f;

    [Header("Catch Distance")]
    [Tooltip("World units. If guard reaches player within this distance → game over.")]
    public float CatchDistance = 0.4f;

    // ── Public State (read by UI) ────────────────────────────────────────

    public GuardState CurrentState { get; private set; } = GuardState.Patrolling;
    public Dictionary<string, float> LastPosterior { get; private set; }
    public Dictionary<string, string> LastEvidence { get; private set; }

    // ── Private ──────────────────────────────────────────────────────────

    private GuardBayesNet _bayesNet;
    private VariableElimination _ve;
    private NoiseSensor _noiseSensor;
    private LightSensor _lightSensor;
    private VisionSensor _visionSensor;

    private List<Vector2Int> _currentPath = new List<Vector2Int>();
    private int _pathIndex = 0;
    private int _waypointIndex = 0;

    private Vector2Int _lastKnownPlayerTile;
    private float _inferenceTimer = 0f;
    private string _lastEvidenceKey = "";

    // ── Unity Lifecycle ──────────────────────────────────────────────────

    void Awake()
    {
        _bayesNet = new GuardBayesNet();
        _ve = new VariableElimination(_bayesNet);
        _noiseSensor = GetComponent<NoiseSensor>();
        _lightSensor = GetComponent<LightSensor>();
        _visionSensor = GetComponent<VisionSensor>();

        // Initialise with safe defaults so LastPosterior is never null
        LastPosterior = new Dictionary<string, float>
        {
            ["Patrolling"] = 1f,
            ["Investigating"] = 0f,
            ["Chasing"] = 0f
        };
        LastEvidence = new Dictionary<string, string>
        {
            ["NoiseLevel"] = "None",
            ["LightExposure"] = "None",
            ["VisualContact"] = "False"
        };
    }

    void FixedUpdate()
    {
        // If Player not assigned, just patrol — no inference needed
        if (Player == null)
        {
            Navigate();
            return;
        }

        // 1. Collect sensor readings
        var evidence = CollectEvidence();

        // 2. Run VE at the configured interval, or immediately when evidence changes
        _inferenceTimer += Time.fixedDeltaTime;
        string evidenceKey = EvidenceKey(evidence);
        if (_inferenceTimer >= InferenceInterval || evidenceKey != _lastEvidenceKey)
        {
            RunInference(evidence);
            _lastEvidenceKey = evidenceKey;
            _inferenceTimer = 0f;
        }

        // 3. Update floating state label
        UpdateStateLabel();

        // 4. Navigate toward target
        Navigate();

        // 5. Catch check
        CheckCatch();
    }

    // ── Inference Pipeline ───────────────────────────────────────────────

    private Dictionary<string, string> CollectEvidence()
    {
        return new Dictionary<string, string>
        {
            ["NoiseLevel"] = _noiseSensor != null ? _noiseSensor.NoiseLevel : "None",
            ["LightExposure"] = _lightSensor != null ? _lightSensor.LightExposure : "None",
            ["VisualContact"] = _visionSensor != null ? (_visionSensor.VisualContact ? "True" : "False") : "False"
        };
    }

    private void RunInference(Dictionary<string, string> evidence)
    {
        LastEvidence = new Dictionary<string, string>(evidence);

        var posterior = _ve.Query("GuardAlertState", evidence);

        // If VE returns null or empty, keep the previous posterior and skip
        if (posterior == null || posterior.Count == 0)
        {
            Debug.LogWarning($"[Guard:{name}] VE returned null/empty posterior — keeping previous state.");
            return;
        }

        // Assign BEFORE OnStateTransition so the log inside can safely read it
        LastPosterior = posterior;

        GuardState newState = ArgmaxState(posterior);

        if (newState != CurrentState)
        {
            OnStateTransition(CurrentState, newState);
            CurrentState = newState;
        }

        // Cache last known player tile when the guard becomes suspicious
        if (Player != null &&
            (newState == GuardState.Investigating || newState == GuardState.Chasing))
        {
            _lastKnownPlayerTile = WorldToTile(Player.transform.position);
        }
    }

    private GuardState ArgmaxState(Dictionary<string, float> posterior)
    {
        float bestProb = -1f;
        string bestKey = "Patrolling";
        foreach (var kv in posterior)
            if (kv.Value > bestProb) { bestProb = kv.Value; bestKey = kv.Key; }

        return bestKey switch
        {
            "Chasing" => GuardState.Chasing,
            "Investigating" => GuardState.Investigating,
            _ => GuardState.Patrolling
        };
    }

    private void OnStateTransition(GuardState from, GuardState to)
    {
        _currentPath.Clear();
        _pathIndex = 0;

        // LastPosterior is guaranteed non-null here (assigned just before this call)
        LastPosterior.TryGetValue("Patrolling", out float p);
        LastPosterior.TryGetValue("Investigating", out float inv);
        LastPosterior.TryGetValue("Chasing", out float c);

        Debug.Log($"[Guard:{name}] {from} -> {to}  " +
                  $"P(Patrol={p:F2}, Invest={inv:F2}, Chase={c:F2})");
    }

    // ── Navigation (A*) ──────────────────────────────────────────────────

    private void Navigate()
    {
        Vector2Int guardTile = WorldToTile(transform.position);
        Vector2Int targetTile = GetTargetTile(guardTile);
        float speed = CurrentSpeedForState();

        // Recompute A* path if consumed or missing
        if (_currentPath == null || _pathIndex >= _currentPath.Count)
        {
            _currentPath = AStar.FindPath(guardTile, targetTile);
            _pathIndex = 0;
            if (_currentPath == null || _currentPath.Count == 0) return;
        }

        if (_pathIndex < _currentPath.Count)
        {
            Vector3 target = TileToWorld(_currentPath[_pathIndex]);
            transform.position = Vector3.MoveTowards(
                transform.position, target, speed * Time.fixedDeltaTime);

            Vector3 dir = target - transform.position;
            if (dir.sqrMagnitude > 0.001f)
                transform.up = dir.normalized;

            if (Vector3.Distance(transform.position, target) < 0.05f)
            {
                _pathIndex++;
                if (CurrentState == GuardState.Patrolling && _pathIndex >= _currentPath.Count)
                    AdvanceWaypoint();
            }
        }
    }

    private Vector2Int GetTargetTile(Vector2Int guardTile)
    {
        switch (CurrentState)
        {
            case GuardState.Chasing:
                return Player != null ? WorldToTile(Player.transform.position) : guardTile;
            case GuardState.Investigating:
                return _lastKnownPlayerTile;
            default:
                return GetPatrolTargetTile();
        }
    }

    private Vector2Int GetPatrolTargetTile()
    {
        if (PatrolWaypoints == null || PatrolWaypoints.Count == 0)
            return WorldToTile(transform.position);

        // Skip any null waypoints
        int safety = PatrolWaypoints.Count;
        while (safety-- > 0 && PatrolWaypoints[_waypointIndex] == null)
            _waypointIndex = (_waypointIndex + 1) % PatrolWaypoints.Count;

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
            GuardState.Chasing => ChaseSpeed,
            GuardState.Investigating => InvestigateSpeed,
            _ => PatrolSpeed
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
            GuardState.Chasing => "[C]",
            GuardState.Investigating => "[I]",
            _ => "[P]"
        };
        StateLabel.color = CurrentState switch
        {
            GuardState.Chasing => Color.red,
            GuardState.Investigating => Color.yellow,
            _ => Color.green
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