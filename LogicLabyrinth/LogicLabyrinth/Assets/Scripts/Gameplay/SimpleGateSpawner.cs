using UnityEngine;
using System.Collections.Generic;

public class SimpleGateSpawner : MonoBehaviour
{
    public GameObject andGatePrefab;
    public GameObject orGatePrefab;
    public GameObject notGatePrefab;
    public List<Transform> spawnPoints;

    [Header("Gate Distribution")]
    [Tooltip("How many OR gates to spawn (answer key needs 3)")]
    public int orCount = 3;
    [Tooltip("How many AND gates to spawn (answer key needs 2)")]
    public int andCount = 2;
    [Tooltip("Remaining spawn points get NOT gates (bogus / decoys)")]
    public bool fillRestWithNOT = true;

    void Start()
    {
        SpawnGates();
    }

    void SpawnGates()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[SimpleGateSpawner] No spawn points assigned!");
            return;
        }

        // ── Check if we have a saved layout to restore ──
        var player = AccountManager.Instance?.GetCurrentPlayer();
        string savedLayout = (player != null) ? player.savedGateLayout : "";

        if (!string.IsNullOrEmpty(savedLayout))
        {
            string[] types = savedLayout.Split(',');
            if (types.Length == spawnPoints.Count)
            {
                Debug.Log($"[SimpleGateSpawner] Restoring saved gate layout: {savedLayout}");
                SpawnFromSavedLayout(types);
                return;
            }
            else
            {
                Debug.LogWarning($"[SimpleGateSpawner] Saved layout length ({types.Length}) doesn't match spawn points ({spawnPoints.Count}) — re-shuffling.");
            }
        }

        // ── No saved layout — generate a new random one ──
        Debug.Log("[SimpleGateSpawner] No saved layout — shuffling new random gate placement.");
        SpawnRandomAndSaveLayout();
    }

    /// <summary>
    /// Spawns gates using the saved layout string (one gate type per spawn point).
    /// </summary>
    void SpawnFromSavedLayout(string[] types)
    {
        int spawnedOR = 0, spawnedAND = 0, spawnedNOT = 0;

        for (int i = 0; i < spawnPoints.Count && i < types.Length; i++)
        {
            string gateType = types[i].Trim().ToUpper();
            GameObject prefab = GetPrefabForType(gateType);

            if (prefab != null && spawnPoints[i] != null)
            {
                SpawnGateAt(prefab, spawnPoints[i]);
                if (gateType == "OR") spawnedOR++;
                else if (gateType == "AND") spawnedAND++;
                else if (gateType == "NOT") spawnedNOT++;
            }
        }

        Debug.Log($"[SimpleGateSpawner] Restored layout: {spawnedOR} OR, {spawnedAND} AND, {spawnedNOT} NOT gates.");
    }

    /// <summary>
    /// Shuffles spawn points, assigns gate types, spawns them, and saves the layout to PlayerData.
    /// </summary>
    void SpawnRandomAndSaveLayout()
    {
        // Build a shuffled copy of spawn points (with their original indices)
        List<int> indices = new List<int>();
        for (int i = 0; i < spawnPoints.Count; i++)
            indices.Add(i);
        ShuffleList(indices);

        // Build the layout array — one entry per spawn point (in original order)
        string[] layout = new string[spawnPoints.Count];

        int shuffleIdx = 0;
        int spawnedOR = 0, spawnedAND = 0, spawnedNOT = 0;

        // Assign OR gates
        for (int i = 0; i < orCount && shuffleIdx < indices.Count; i++, shuffleIdx++)
        {
            int spawnIdx = indices[shuffleIdx];
            layout[spawnIdx] = "OR";
            SpawnGateAt(orGatePrefab, spawnPoints[spawnIdx]);
            spawnedOR++;
        }

        // Assign AND gates
        for (int i = 0; i < andCount && shuffleIdx < indices.Count; i++, shuffleIdx++)
        {
            int spawnIdx = indices[shuffleIdx];
            layout[spawnIdx] = "AND";
            SpawnGateAt(andGatePrefab, spawnPoints[spawnIdx]);
            spawnedAND++;
        }

        // Fill remaining with NOT gates
        if (fillRestWithNOT)
        {
            while (shuffleIdx < indices.Count)
            {
                int spawnIdx = indices[shuffleIdx];
                layout[spawnIdx] = "NOT";
                SpawnGateAt(notGatePrefab, spawnPoints[spawnIdx]);
                spawnedNOT++;
                shuffleIdx++;
            }
        }

        // Save the layout to PlayerData so Load Game restores the same assignment
        string layoutStr = string.Join(",", layout);
        if (AccountManager.Instance != null && AccountManager.Instance.GetCurrentPlayer() != null)
        {
            AccountManager.Instance.GetCurrentPlayer().savedGateLayout = layoutStr;
            // Save immediately so even a crash preserves the layout
            AccountManager.Instance.SavePlayerProgress();
            Debug.Log($"[SimpleGateSpawner] Saved gate layout: {layoutStr}");
        }

        Debug.Log($"[SimpleGateSpawner] Spawned {spawnedOR} OR, {spawnedAND} AND, {spawnedNOT} NOT gates across {spawnPoints.Count} spawn points.");
    }

    GameObject GetPrefabForType(string gateType)
    {
        switch (gateType)
        {
            case "AND": return andGatePrefab;
            case "OR":  return orGatePrefab;
            case "NOT": return notGatePrefab;
            default:
                Debug.LogWarning($"[SimpleGateSpawner] Unknown gate type: {gateType}");
                return null;
        }
    }

    void SpawnGateAt(GameObject gatePrefab, Transform point)
    {
        if (gatePrefab == null || point == null) return;
        Instantiate(gatePrefab, point.position, point.rotation);
    }

    /// <summary>
    /// Fisher-Yates shuffle for random gate placement.
    /// </summary>
    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}