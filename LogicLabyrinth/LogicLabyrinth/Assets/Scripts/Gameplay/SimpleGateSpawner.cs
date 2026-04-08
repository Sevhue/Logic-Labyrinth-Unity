using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

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

    [Header("Spawn Transform")]
    [Tooltip("Scales every spawned gate uniformly. Lower this if gates appear too large at spawn points.")]
    [Min(0.01f)] public float spawnedGateScaleMultiplier = 1f;

    [Header("Editor Preview")]
    [Tooltip("Always show SpawnPoint markers and indices in Scene view for easier placement.")]
    public bool showSpawnPointMarkers = true;
    [Min(0.01f)] public float spawnPointMarkerSize = 0.12f;
    [Tooltip("Show actual random gate mesh previews at spawn points (editor-only, for testing placement).")]
    public bool showRandomGateMeshPreview = true;
    [Tooltip("Changes which random gate type appears on each spawn point preview.")]
    public int previewRandomSeed = 7;
    [Tooltip("When you select a SpawnPoint Transform, show gate-size previews at that point.")]
    public bool previewOnSelectedSpawnPoint = true;

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
        GameObject spawned = Instantiate(gatePrefab, point.position, point.rotation);
        spawned.transform.localScale *= spawnedGateScaleMultiplier;
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

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (spawnPoints == null || spawnPoints.Count == 0) return;

        if (showSpawnPointMarkers)
        {
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                Transform p = spawnPoints[i];
                if (p == null) continue;

                if (showRandomGateMeshPreview)
                {
                    GameObject previewPrefab = GetRandomPreviewPrefabForIndex(i);
                    DrawPrefabMeshPreview(previewPrefab, p, new Color(1f, 0.95f, 0.7f, 0.22f), new Color(1f, 0.8f, 0.35f, 0.95f));
                }
                else
                {
                    Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
                    Gizmos.DrawSphere(p.position, spawnPointMarkerSize);
                }

                Handles.color = new Color(1f, 0.9f, 0.55f, 1f);
                Handles.Label(p.position + Vector3.up * 0.18f, $"SpawnPoint{i + 1}");
            }
        }

        if (!previewOnSelectedSpawnPoint) return;

        Transform selected = Selection.activeTransform;
        if (selected == null) return;

        bool isSpawnPoint = false;
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            if (spawnPoints[i] == selected)
            {
                isSpawnPoint = true;
                break;
            }
        }

        if (!isSpawnPoint) return;

        DrawPrefabPreview(andGatePrefab, selected, new Color(0.15f, 0.85f, 1f, 0.95f), "AND");
        DrawPrefabPreview(orGatePrefab, selected, new Color(0.35f, 1f, 0.35f, 0.95f), "OR");
        DrawPrefabPreview(notGatePrefab, selected, new Color(1f, 0.8f, 0.2f, 0.95f), "NOT");
    }

    void DrawPrefabPreview(GameObject prefab, Transform point, Color color, string label)
    {
        if (prefab == null || point == null) return;
        if (!TryGetPrefabLocalBounds(prefab, out Bounds bounds)) return;

        Vector3 scaledCenter = bounds.center * spawnedGateScaleMultiplier;
        Vector3 scaledSize = bounds.size * spawnedGateScaleMultiplier;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(point.position, point.rotation, Vector3.one);
        Gizmos.color = color;
        Gizmos.DrawWireCube(scaledCenter, scaledSize);
        Gizmos.matrix = prev;

        Handles.color = color;
        Handles.Label(point.position + Vector3.up * 0.15f, label);
    }

    GameObject GetRandomPreviewPrefabForIndex(int index)
    {
        List<GameObject> prefabs = new List<GameObject>();
        if (andGatePrefab != null) prefabs.Add(andGatePrefab);
        if (orGatePrefab != null) prefabs.Add(orGatePrefab);
        if (notGatePrefab != null) prefabs.Add(notGatePrefab);
        if (prefabs.Count == 0) return null;

        int hash = Mathf.Abs((previewRandomSeed + 1) * 73856093 ^ (index + 1) * 19349663);
        return prefabs[hash % prefabs.Count];
    }

    void DrawPrefabMeshPreview(GameObject prefab, Transform point, Color fillColor, Color wireColor)
    {
        if (prefab == null || point == null) return;

        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        if (meshFilters == null || meshFilters.Length == 0)
        {
            Gizmos.color = wireColor;
            Gizmos.DrawSphere(point.position, spawnPointMarkerSize);
            return;
        }

        Matrix4x4 root = Matrix4x4.TRS(point.position, point.rotation, Vector3.one * spawnedGateScaleMultiplier);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;

            Matrix4x4 matrix = root * mf.transform.localToWorldMatrix;
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = matrix;
            Gizmos.color = fillColor;
            Gizmos.DrawMesh(mf.sharedMesh);
            Gizmos.color = wireColor;
            Gizmos.DrawWireMesh(mf.sharedMesh);
            Gizmos.matrix = prev;
        }
    }

    bool TryGetPrefabLocalBounds(GameObject prefab, out Bounds bounds)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bool initialized = false;
        bounds = new Bounds(Vector3.zero, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            if (!initialized)
            {
                bounds = r.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return initialized;
    }
#endif
}