using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// BNDemoScene — standalone Bayesian Network demonstration (Plan B fallback).
///
/// This scene lets you manually set evidence variable values via UI dropdowns
/// and immediately see the Variable Elimination output printed to both the
/// Unity console and an on-screen text panel.
///
/// This scene is completely independent of the main game.
/// It can be used to demonstrate / debug the BN inference engine in isolation.
///
/// Setup:
///   1. Create a new scene called "BNDemo".
///   2. Add a Canvas with:
///      - Three TMP_Dropdown elements (NoiseDropdown, LightDropdown, VisionDropdown)
///      - A Button labeled "Run Inference" → OnClick → BNDemoScene.RunInference()
///      - A TextMeshProUGUI for OutputText
///   3. Attach this script to any GameObject in the scene.
///   4. Assign all fields in the Inspector.
/// </summary>
public class BNDemoScene : MonoBehaviour
{
    [Header("Evidence Dropdowns")]
    [Tooltip("Options: None / Low / High")]
    public TMP_Dropdown NoiseDropdown;
    [Tooltip("Options: None / Partial / Full")]
    public TMP_Dropdown LightDropdown;
    [Tooltip("Options: False / True")]
    public TMP_Dropdown VisionDropdown;

    [Header("Output")]
    public TextMeshProUGUI OutputText;
    public Button RunButton;

    private GuardBayesNet       _net;
    private VariableElimination _ve;

    // Value arrays matching dropdown option order
    private static readonly string[] NoiseValues  = { "None", "Low", "High" };
    private static readonly string[] LightValues  = { "None", "Partial", "Full" };
    private static readonly string[] VisionValues = { "False", "True" };

    void Start()
    {
        _net = new GuardBayesNet();
        _ve  = new VariableElimination(_net);

        // Populate dropdowns
        SetupDropdown(NoiseDropdown,  new[] { "None", "Low", "High" });
        SetupDropdown(LightDropdown,  new[] { "None", "Partial", "Full" });
        SetupDropdown(VisionDropdown, new[] { "False", "True" });

        if (RunButton != null)
            RunButton.onClick.AddListener(RunInference);

        // Run once with defaults
        RunInference();
    }

    public void RunInference()
    {
        string noise  = NoiseValues[NoiseDropdown?.value  ?? 0];
        string light  = LightValues[LightDropdown?.value  ?? 0];
        string vision = VisionValues[VisionDropdown?.value ?? 0];

        var evidence = new Dictionary<string, string>
        {
            ["NoiseLevel"]    = noise,
            ["LightExposure"] = light,
            ["VisualContact"] = vision
        };

        var posterior = _ve.Query("GuardAlertState", evidence);

        string result =
            $"━━━━━ BN Inference Result ━━━━━\n\n" +
            $"EVIDENCE:\n" +
            $"  NoiseLevel    = {noise}\n" +
            $"  LightExposure = {light}\n" +
            $"  VisualContact = {vision}\n\n" +
            $"P(GuardAlertState | evidence):\n" +
            $"  Patrolling    = {posterior.GetValueOrDefault("Patrolling",   0f):F4}\n" +
            $"  Investigating = {posterior.GetValueOrDefault("Investigating", 0f):F4}\n" +
            $"  Chasing       = {posterior.GetValueOrDefault("Chasing",       0f):F4}\n\n" +
            $"→ Decision: {Argmax(posterior)}\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";

        if (OutputText != null) OutputText.text = result;
        Debug.Log(result);
    }

    private string Argmax(Dictionary<string, float> dist)
    {
        string best = "";
        float bestVal = -1f;
        foreach (var kv in dist)
            if (kv.Value > bestVal) { bestVal = kv.Value; best = kv.Key; }
        return best;
    }

    private void SetupDropdown(TMP_Dropdown dropdown, string[] options)
    {
        if (dropdown == null) return;
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
    }
}
