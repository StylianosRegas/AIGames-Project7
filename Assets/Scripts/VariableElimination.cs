using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Represents a factor in the Variable Elimination algorithm.
/// A factor is a function over a subset of variables storing
/// a probability for every combination of their values.
///
/// Internal key format: "val_0|val_1|...|val_n" matching Variables order.
/// </summary>
public class Factor
{
    public List<string> Variables = new List<string>();              // ordered variable names
    public Dictionary<string, float> Table = new Dictionary<string, float>(); // key → probability

    public Factor() { }

    public Factor(List<string> variables)
    {
        Variables = new List<string>(variables);
    }

    // Build a key from an assignment dictionary in Variables order
    public string KeyFromAssignment(Dictionary<string, string> assignment)
    {
        var parts = Variables.Select(v =>
        {
            if (!assignment.TryGetValue(v, out string val))
                throw new System.Exception($"[Factor] Variable '{v}' missing from assignment.");
            return val;
        });
        return string.Join("|", parts);
    }

    // Parse a key back into a partial assignment for this factor's variables
    public Dictionary<string, string> AssignmentFromKey(string key)
    {
        string[] parts = key.Split('|');
        var result = new Dictionary<string, string>();
        for (int i = 0; i < Variables.Count; i++)
            result[Variables[i]] = parts[i];
        return result;
    }
}

/// <summary>
/// Variable Elimination (VE) inference engine for the guard Bayesian Network.
///
/// Answers the query:
///   P(GuardAlertState | NoiseLevel=e1, LightExposure=e2, VisualContact=e3)
///
/// Steps:
///   1. Build factors from CPTs.
///   2. Restrict evidence variables to their observed values.
///   3. Eliminate the hidden variable PlayerPresence by summing it out.
///   4. Multiply all remaining factors.
///   5. Normalize to get a valid posterior over GuardAlertState.
/// </summary>
public class VariableElimination
{
    private readonly GuardBayesNet _net;

    // Elimination order for hidden variables (only PlayerPresence in our network)
    private static readonly string[] EliminationOrder = { "PlayerPresence" };

    public VariableElimination(GuardBayesNet net)
    {
        _net = net;
    }

    /// <summary>
    /// Run VE and return P(queryVariable | evidence).
    /// Returns a dictionary: value → probability (normalized, sums to 1).
    ///
    /// queryVariable: "GuardAlertState"
    /// evidence: e.g. { "NoiseLevel":"High", "LightExposure":"Partial", "VisualContact":"False" }
    /// </summary>
    public Dictionary<string, float> Query(string queryVariable, Dictionary<string, string> evidence)
    {
        // Step 1: Build one factor per node from its CPT
        var factors = BuildFactors();

        // Step 2: Restrict evidence — zero out (then remove) rows that contradict observed values
        factors = RestrictEvidence(factors, evidence);

        // Step 3: Eliminate each hidden variable
        foreach (string hiddenVar in EliminationOrder)
        {
            if (evidence.ContainsKey(hiddenVar)) continue; // already observed, skip
            factors = EliminateVariable(factors, hiddenVar);
        }

        // Step 4: Multiply all remaining factors into one
        Factor result = MultiplyAll(factors);

        // Step 5: Marginalize to query variable, then normalize
        return Normalize(Marginalize(result, queryVariable, _net.GetNode(queryVariable).Values));
    }

    // ── Step 1: Build Factors ────────────────────────────────────────────

    private List<Factor> BuildFactors()
    {
        var factors = new List<Factor>();
        foreach (var node in _net.Nodes.Values)
        {
            var vars = new List<string>(node.ParentNames) { node.Name };
            var factor = new Factor(vars);

            // Enumerate all combinations of parent values × node values
            var parentNodes = node.ParentNames.Select(p => _net.GetNode(p)).ToList();
            foreach (var combo in EnumerateCombinations(parentNodes, node))
            {
                string key = factor.KeyFromAssignment(combo);
                string[] parentVals = node.ParentNames.Select(p => combo[p]).ToArray();
                factor.Table[key] = node.GetProbability(parentVals, combo[node.Name]);
            }

            factors.Add(factor);
        }
        return factors;
    }

    // ── Step 2: Restrict Evidence ────────────────────────────────────────

    private List<Factor> RestrictEvidence(List<Factor> factors, Dictionary<string, string> evidence)
    {
        var result = new List<Factor>();
        foreach (var factor in factors)
        {
            bool hasEvidenceVar = factor.Variables.Any(v => evidence.ContainsKey(v));
            if (!hasEvidenceVar)
            {
                result.Add(factor);
                continue;
            }

            // Build a new factor with evidence variables fixed
            var newVars = factor.Variables.Where(v => !evidence.ContainsKey(v)).ToList();
            var newFactor = new Factor(newVars);

            foreach (var entry in factor.Table)
            {
                var assignment = factor.AssignmentFromKey(entry.Key);

                // Check that this row is consistent with evidence
                bool consistent = evidence.All(ev =>
                    !assignment.ContainsKey(ev.Key) || assignment[ev.Key] == ev.Value);

                if (!consistent) continue;

                // Build reduced key (only non-evidence vars)
                if (newVars.Count == 0)
                {
                    // Scalar factor — accumulate into a single entry
                    if (!newFactor.Table.ContainsKey("scalar"))
                        newFactor.Table["scalar"] = 0f;
                    newFactor.Table["scalar"] += entry.Value;
                }
                else
                {
                    string newKey = string.Join("|", newVars.Select(v => assignment[v]));
                    if (!newFactor.Table.ContainsKey(newKey))
                        newFactor.Table[newKey] = 0f;
                    newFactor.Table[newKey] += entry.Value;
                }
            }

            result.Add(newFactor);
        }
        return result;
    }

    // ── Step 3: Eliminate a Hidden Variable ──────────────────────────────

    private List<Factor> EliminateVariable(List<Factor> factors, string variable)
    {
        // Partition: factors that mention `variable` vs those that don't
        var relevant   = factors.Where(f => f.Variables.Contains(variable)).ToList();
        var irrelevant = factors.Where(f => !f.Variables.Contains(variable)).ToList();

        if (relevant.Count == 0) return factors;

        // Multiply all relevant factors together
        Factor product = MultiplyAll(relevant);

        // Sum out the variable → new factor over the remaining variables
        var remainingVars = product.Variables.Where(v => v != variable).ToList();
        var summedFactor = new Factor(remainingVars);

        string[] varValues = _net.GetNode(variable).Values;

        foreach (var entry in product.Table)
        {
            var assignment = product.AssignmentFromKey(entry.Key);
            // Build key without the eliminated variable
            string newKey = remainingVars.Count > 0
                ? string.Join("|", remainingVars.Select(v => assignment[v]))
                : "scalar";

            if (!summedFactor.Table.ContainsKey(newKey))
                summedFactor.Table[newKey] = 0f;
            summedFactor.Table[newKey] += entry.Value;
        }

        var result = new List<Factor>(irrelevant) { summedFactor };
        return result;
    }

    // ── Step 4: Multiply All Factors ─────────────────────────────────────

    private Factor MultiplyAll(List<Factor> factors)
    {
        if (factors.Count == 0)
            return new Factor(new List<string>());

        Factor result = factors[0];
        for (int i = 1; i < factors.Count; i++)
            result = Multiply(result, factors[i]);
        return result;
    }

    private Factor Multiply(Factor a, Factor b)
    {
        // Union of variables (preserve order: a's vars first, then b's extras)
        var allVars = new List<string>(a.Variables);
        foreach (string v in b.Variables)
            if (!allVars.Contains(v)) allVars.Add(v);

        var result = new Factor(allVars);

        // Get all node value domains for enumeration
        var domains = allVars.ToDictionary(
            v => v,
            v => _net.GetNode(v)?.Values ?? new[] { "scalar" });

        foreach (var combo in EnumerateAssignments(allVars, domains))
        {
            float pA = GetFactorValue(a, combo);
            float pB = GetFactorValue(b, combo);
            string key = string.Join("|", allVars.Select(v => combo[v]));
            result.Table[key] = pA * pB;
        }
        return result;
    }

    private float GetFactorValue(Factor f, Dictionary<string, string> fullAssignment)
    {
        if (f.Variables.Count == 0)
            return f.Table.ContainsKey("scalar") ? f.Table["scalar"] : 1f;

        string key = string.Join("|", f.Variables.Select(v => fullAssignment[v]));
        return f.Table.TryGetValue(key, out float val) ? val : 0f;
    }

    // ── Step 5: Marginalize + Normalize ──────────────────────────────────

    private Dictionary<string, float> Marginalize(Factor factor, string queryVar, string[] queryValues)
    {
        var result = new Dictionary<string, float>();
        foreach (string qv in queryValues) result[qv] = 0f;

        if (factor.Variables.Count == 0)
        {
            float scalar = factor.Table.ContainsKey("scalar") ? factor.Table["scalar"] : 1f;
            foreach (string qv in queryValues) result[qv] = scalar / queryValues.Length;
            return result;
        }

        foreach (var entry in factor.Table)
        {
            var assignment = factor.AssignmentFromKey(entry.Key);
            if (assignment.TryGetValue(queryVar, out string qval))
                result[qval] += entry.Value;
        }
        return result;
    }

    private Dictionary<string, float> Normalize(Dictionary<string, float> dist)
    {
        float total = dist.Values.Sum();
        if (total <= 0f)
        {
            // Fallback: uniform
            Debug.LogWarning("[VE] Posterior sums to 0 — returning uniform distribution.");
            var uniform = new Dictionary<string, float>();
            foreach (var k in dist.Keys) uniform[k] = 1f / dist.Count;
            return uniform;
        }
        return dist.ToDictionary(kv => kv.Key, kv => kv.Value / total);
    }

    // ── Enumeration Helpers ──────────────────────────────────────────────

    /// <summary>
    /// Enumerate all value combinations for a list of nodes (parentNodes + the child node).
    /// Returns one assignment dictionary per combination.
    /// </summary>
    private IEnumerable<Dictionary<string, string>> EnumerateCombinations(List<BNNode> parentNodes, BNNode childNode)
    {
        var allNodes = new List<BNNode>(parentNodes) { childNode };
        var varNames = allNodes.Select(n => n.Name).ToList();
        var domains  = allNodes.ToDictionary(n => n.Name, n => n.Values);
        return EnumerateAssignments(varNames, domains);
    }

    private IEnumerable<Dictionary<string, string>> EnumerateAssignments(
        List<string> vars,
        Dictionary<string, string[]> domains)
    {
        if (vars.Count == 0)
        {
            yield return new Dictionary<string, string>();
            yield break;
        }

        string first = vars[0];
        var rest = vars.Skip(1).ToList();

        foreach (string val in domains[first])
        {
            foreach (var subAssignment in EnumerateAssignments(rest, domains))
            {
                var assignment = new Dictionary<string, string>(subAssignment) { [first] = val };
                yield return assignment;
            }
        }
    }
}
