using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

// ────────────────────────────────────────────────────────────────────────────
// SuspicionMeter
//
// Displays the posterior P(GuardAlertState = Chasing) for the nearest guard
// as a color-coded HUD bar in the top-right corner.
//
// Setup:
//   1. Create a Canvas (Screen Space — Overlay).
//   2. Add a Panel anchored top-right. Inside it place:
//      - A TextMeshProUGUI named "AlertLabel" (text: "Nearest Guard Alert Level")
//      - A Slider (non-interactable) as the bar background
//      - Assign the Slider's Fill Image to "MeterFill"
//   3. Attach this script to the Panel.
//   4. Assign all fields in the Inspector.
// ────────────────────────────────────────────────────────────────────────────
public class SuspicionMeter : MonoBehaviour
{
    [Header("References")]
    [Tooltip("All guards in the scene")]
    public List<GuardController> Guards;

    [Header("UI Elements")]
    public Slider MeterSlider;   // non-interactable slider (0–1)
    public Image MeterFill;     // the fill image of the slider
    public TextMeshProUGUI StateText; // e.g. "PATROLLING"

    [Header("Colors")]
    public Color PatrolColor = Color.green;
    public Color InvestigateColor = Color.yellow;
    public Color ChaseColor = Color.red;

    // Cached nearest guard
    private GuardController _nearestGuard;
    private Transform _playerTransform;

    void Start()
    {
        // Find the player
        var player = FindObjectOfType<PlayerController>();
        if (player != null) _playerTransform = player.transform;

        // Auto-find guards if not assigned
        if (Guards == null || Guards.Count == 0)
            Guards = new List<GuardController>(FindObjectsOfType<GuardController>());
    }

    void Update()
    {
        if (Guards == null || Guards.Count == 0) return;

        // Find nearest guard to player
        _nearestGuard = GetNearestGuard();
        if (_nearestGuard == null) return;

        var posterior = _nearestGuard.LastPosterior;
        if (posterior == null || posterior.Count == 0) return;

        float chaseProb = posterior.TryGetValue("Chasing", out float cp) ? cp : 0f;
        float investProb = posterior.TryGetValue("Investigating", out float ip) ? ip : 0f;

        // Update slider
        if (MeterSlider != null)
            MeterSlider.value = chaseProb + investProb * 0.5f; // weight for visual feel

        // Update color
        Color targetColor = _nearestGuard.CurrentState switch
        {
            GuardState.Chasing => ChaseColor,
            GuardState.Investigating => InvestigateColor,
            _ => PatrolColor
        };
        if (MeterFill != null) MeterFill.color = targetColor;

        // Update label
        if (StateText != null)
        {
            StateText.text = _nearestGuard.CurrentState switch
            {
                GuardState.Chasing => "⚠ CHASING",
                GuardState.Investigating => "? INVESTIGATING",
                _ => "✓ PATROLLING"
            };
            StateText.color = targetColor;
        }
    }

    private GuardController GetNearestGuard()
    {
        if (_playerTransform == null) return Guards[0];

        GuardController nearest = null;
        float minDist = float.MaxValue;

        foreach (var g in Guards)
        {
            if (g == null) continue;
            float d = Vector2.Distance(_playerTransform.position, g.transform.position);
            if (d < minDist) { minDist = d; nearest = g; }
        }
        return nearest;
    }
}

// ────────────────────────────────────────────────────────────────────────────
// DebugPanel
//
// Toggleable with the Tab key. Shows raw VE inference data for the nearest guard:
//   - Current evidence values
//   - Full posterior P(GuardAlertState | evidence)
//
// Setup:
//   1. Create a UI Panel (can be the same Canvas as SuspicionMeter).
//   2. Add a TextMeshProUGUI inside it for the debug text.
//   3. Attach this script to the Panel.
//   4. Assign DebugText and SuspicionMeterRef in the Inspector.
// ────────────────────────────────────────────────────────────────────────────
public class DebugPanel : MonoBehaviour
{
    [Header("References")]
    public SuspicionMeter SuspicionMeterRef;
    public TextMeshProUGUI DebugText;

    private bool _visible = false;

    void Start()
    {
        gameObject.SetActive(_visible);
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            _visible = !_visible;
            gameObject.SetActive(_visible);
        }

        if (!_visible) return;
        RefreshText();
    }

    private void RefreshText()
    {
        if (SuspicionMeterRef == null || DebugText == null) return;

        // Get nearest guard from SuspicionMeter (reuse its reference)
        var guard = SuspicionMeterRef.Guards?.FirstOrDefault(g => g != null);
        // Try to get the actual nearest by comparing to player
        var player = FindObjectOfType<PlayerController>();
        if (player != null && SuspicionMeterRef.Guards != null)
        {
            float minD = float.MaxValue;
            foreach (var g in SuspicionMeterRef.Guards)
            {
                if (g == null) continue;
                float d = Vector2.Distance(player.transform.position, g.transform.position);
                if (d < minD) { minD = d; guard = g; }
            }
        }

        if (guard == null) { DebugText.text = "No guards found."; return; }

        var ev = guard.LastEvidence;
        var post = guard.LastPosterior;

        string evStr = ev != null
            ? $"NoiseLevel    = {ev.GetValueOrDefault("NoiseLevel", "?")}\n" +
              $"LightExposure = {ev.GetValueOrDefault("LightExposure", "?")}\n" +
              $"VisualContact = {ev.GetValueOrDefault("VisualContact", "?")}"
            : "(no evidence yet)";

        string postStr = post != null
            ? $"P(Patrolling)   = {post.GetValueOrDefault("Patrolling", 0f):F4}\n" +
              $"P(Investigating) = {post.GetValueOrDefault("Investigating", 0f):F4}\n" +
              $"P(Chasing)       = {post.GetValueOrDefault("Chasing", 0f):F4}"
            : "(no posterior yet)";

        DebugText.text =
            $"── BN Debug (nearest guard: {guard.name}) ──\n\n" +
            $"EVIDENCE:\n{evStr}\n\n" +
            $"POSTERIOR (VE result):\n{postStr}\n\n" +
            $"STATE: {guard.CurrentState}\n\n" +
            "[Tab] to hide";
    }
}