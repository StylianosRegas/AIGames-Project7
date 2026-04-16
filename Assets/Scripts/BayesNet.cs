using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents a single node in the Bayesian Network.
/// Each node has a name, a list of possible discrete values,
/// a list of parent node names, and a Conditional Probability Table (CPT).
///
/// CPT key format: parent_val_0|parent_val_1|...|this_val
/// Example: "True|None|Patrolling" → P(GuardAlertState=Patrolling | PlayerPresence=True, NoiseLevel=None)
/// </summary>
[System.Serializable]
public class BNNode
{
    public string Name;
    public string[] Values;           // All possible discrete values for this node
    public string[] ParentNames;      // Ordered list of parent node names
    public Dictionary<string, float> CPT = new Dictionary<string, float>();

    public BNNode(string name, string[] values, string[] parentNames)
    {
        Name = name;
        Values = values;
        ParentNames = parentNames ?? new string[0];
    }

    /// <summary>
    /// Build the CPT key from an ordered array of parent values + this node's value.
    /// </summary>
    public static string MakeKey(string[] parentValues, string nodeValue)
    {
        if (parentValues == null || parentValues.Length == 0)
            return nodeValue;
        return string.Join("|", parentValues) + "|" + nodeValue;
    }

    /// <summary>
    /// Look up P(this=nodeValue | parents=parentValues).
    /// Returns 0 and logs a warning if the key is missing.
    /// </summary>
    public float GetProbability(string[] parentValues, string nodeValue)
    {
        string key = MakeKey(parentValues, nodeValue);
        if (CPT.TryGetValue(key, out float prob))
            return prob;

        Debug.LogWarning($"[BayesNet] Missing CPT entry for node '{Name}', key='{key}'. Returning 0.");
        return 0f;
    }
}

/// <summary>
/// The full Bayesian Network for the guard AI.
///
/// Network structure:
///
///   PlayerPresence
///       |         \
///   NoiseLevel  LightExposure
///       \           /      \
///        \         /        \
///       VisualContact    (also parent)
///            |         /
///         GuardAlertState
///
/// Nodes:
///   PlayerPresence  → hidden (Boolean: True / False)
///   NoiseLevel      → observable (None / Low / High)
///   LightExposure   → observable (None / Partial / Full)
///   VisualContact   → observable (True / False)
///   GuardAlertState → query (Patrolling / Investigating / Chasing)
///
/// Conditional independence:
///   NoiseLevel ⊥ LightExposure | PlayerPresence
///   (Once we know PlayerPresence, noise and light are independent of each other.)
/// </summary>
public class GuardBayesNet
{
    public Dictionary<string, BNNode> Nodes = new Dictionary<string, BNNode>();

    public GuardBayesNet()
    {
        BuildNetwork();
    }

    private void BuildNetwork()
    {
        // ── Node: PlayerPresence ─────────────────────────────────────────
        // Prior probability (no parents)
        var presence = new BNNode("PlayerPresence", new[] { "True", "False" }, null);
        presence.CPT[BNNode.MakeKey(null, "True")]  = 0.3f;
        presence.CPT[BNNode.MakeKey(null, "False")] = 0.7f;
        Nodes["PlayerPresence"] = presence;

        // ── Node: NoiseLevel ─────────────────────────────────────────────
        // Parent: PlayerPresence
        // Conditional independence: NoiseLevel ⊥ LightExposure | PlayerPresence
        var noise = new BNNode("NoiseLevel", new[] { "None", "Low", "High" }, new[] { "PlayerPresence" });
        // P(NoiseLevel | PlayerPresence=True)
        noise.CPT["True|None"] = 0.2f;
        noise.CPT["True|Low"]  = 0.5f;
        noise.CPT["True|High"] = 0.3f;
        // P(NoiseLevel | PlayerPresence=False)
        noise.CPT["False|None"] = 0.9f;
        noise.CPT["False|Low"]  = 0.08f;
        noise.CPT["False|High"] = 0.02f;
        Nodes["NoiseLevel"] = noise;

        // ── Node: LightExposure ──────────────────────────────────────────
        // Parent: PlayerPresence
        // Conditional independence: LightExposure ⊥ NoiseLevel | PlayerPresence
        var light = new BNNode("LightExposure", new[] { "None", "Partial", "Full" }, new[] { "PlayerPresence" });
        // P(LightExposure | PlayerPresence=True)
        light.CPT["True|None"]    = 0.3f;
        light.CPT["True|Partial"] = 0.4f;
        light.CPT["True|Full"]    = 0.3f;
        // P(LightExposure | PlayerPresence=False)
        light.CPT["False|None"]    = 0.7f;
        light.CPT["False|Partial"] = 0.2f;
        light.CPT["False|Full"]    = 0.1f;
        Nodes["LightExposure"] = light;

        // ── Node: VisualContact ──────────────────────────────────────────
        // Parents: PlayerPresence, LightExposure
        var vision = new BNNode("VisualContact", new[] { "True", "False" }, new[] { "PlayerPresence", "LightExposure" });
        // P(VisualContact | PlayerPresence=True, LightExposure=None)
        vision.CPT["True|None|True"]  = 0.1f;
        vision.CPT["True|None|False"] = 0.9f;
        // P(VisualContact | PlayerPresence=True, LightExposure=Partial)
        vision.CPT["True|Partial|True"]  = 0.5f;
        vision.CPT["True|Partial|False"] = 0.5f;
        // P(VisualContact | PlayerPresence=True, LightExposure=Full)
        vision.CPT["True|Full|True"]  = 0.95f;
        vision.CPT["True|Full|False"] = 0.05f;
        // P(VisualContact | PlayerPresence=False, LightExposure=*)
        vision.CPT["False|None|True"]     = 0.01f;
        vision.CPT["False|None|False"]    = 0.99f;
        vision.CPT["False|Partial|True"]  = 0.01f;
        vision.CPT["False|Partial|False"] = 0.99f;
        vision.CPT["False|Full|True"]     = 0.02f;
        vision.CPT["False|Full|False"]    = 0.98f;
        Nodes["VisualContact"] = vision;

        // ── Node: GuardAlertState ────────────────────────────────────────
        // Parents: NoiseLevel, LightExposure, VisualContact
        var alert = new BNNode("GuardAlertState",
            new[] { "Patrolling", "Investigating", "Chasing" },
            new[] { "NoiseLevel", "LightExposure", "VisualContact" });

        // Helper: set one full row (must sum to 1)
        void AddAlertRow(string noise_, string light_, string vision_, float patrol, float invest, float chase)
        {
            string prefix = $"{noise_}|{light_}|{vision_}";
            alert.CPT[$"{prefix}|Patrolling"]   = patrol;
            alert.CPT[$"{prefix}|Investigating"] = invest;
            alert.CPT[$"{prefix}|Chasing"]       = chase;
        }

        // VisualContact=True → almost certainly Chasing
        AddAlertRow("None",  "None",    "True", 0.02f, 0.03f, 0.95f);
        AddAlertRow("None",  "Partial", "True", 0.02f, 0.03f, 0.95f);
        AddAlertRow("None",  "Full",    "True", 0.01f, 0.02f, 0.97f);
        AddAlertRow("Low",   "None",    "True", 0.02f, 0.03f, 0.95f);
        AddAlertRow("Low",   "Partial", "True", 0.01f, 0.02f, 0.97f);
        AddAlertRow("Low",   "Full",    "True", 0.01f, 0.01f, 0.98f);
        AddAlertRow("High",  "None",    "True", 0.01f, 0.02f, 0.97f);
        AddAlertRow("High",  "Partial", "True", 0.01f, 0.01f, 0.98f);
        AddAlertRow("High",  "Full",    "True", 0.00f, 0.01f, 0.99f);

        // VisualContact=False, NoiseLevel=High → likely Investigating
        AddAlertRow("High", "None",    "False", 0.10f, 0.75f, 0.15f);
        AddAlertRow("High", "Partial", "False", 0.08f, 0.72f, 0.20f);
        AddAlertRow("High", "Full",    "False", 0.05f, 0.65f, 0.30f);

        // VisualContact=False, NoiseLevel=Low → slightly suspicious
        AddAlertRow("Low", "None",    "False", 0.60f, 0.35f, 0.05f);
        AddAlertRow("Low", "Partial", "False", 0.50f, 0.40f, 0.10f);
        AddAlertRow("Low", "Full",    "False", 0.40f, 0.45f, 0.15f);

        // VisualContact=False, NoiseLevel=None → relaxed patrol
        AddAlertRow("None", "None",    "False", 0.90f, 0.09f, 0.01f);
        AddAlertRow("None", "Partial", "False", 0.80f, 0.18f, 0.02f);
        AddAlertRow("None", "Full",    "False", 0.70f, 0.25f, 0.05f);

        Nodes["GuardAlertState"] = alert;
    }

    /// <summary>
    /// Retrieve a node by name. Returns null if not found.
    /// </summary>
    public BNNode GetNode(string name)
    {
        Nodes.TryGetValue(name, out BNNode node);
        return node;
    }
}
