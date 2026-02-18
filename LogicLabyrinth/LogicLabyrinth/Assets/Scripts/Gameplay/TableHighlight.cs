using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Adds a pulsing glowing outline effect around the table — top edges AND legs.
/// Uses LineRenderers that trace the actual mesh bounds in local space,
/// so the outline matches the table's shape and rotation.
/// Automatically removes itself when the player opens the puzzle table.
/// </summary>
public class TableHighlight : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color glowColor = new Color(1f, 0.85f, 0.4f, 1f);     // Bright gold
    public Color dimColor  = new Color(0.6f, 0.45f, 0.15f, 0.4f); // Dim gold
    public float pulseSpeed = 2f;
    public float lineWidth = 0.03f;
    public float heightOffset = 0.02f; // Tiny offset to prevent z-fighting

    [Header("Leg Settings")]
    [Tooltip("How much to inset the bottom rectangle from the top (table legs are narrower)")]
    public float legInset = 0.15f;
    public float legLineWidth = 0.025f;

    // Internal
    private List<LineRenderer> allLines = new List<LineRenderer>();
    private GameObject outlineRoot;
    private Material outlineMaterial;
    private bool isActive = false;
    private float pulseTimer = 0f;

    /// <summary>
    /// Begin the pulsing outline effect.
    /// </summary>
    public void StartHighlight()
    {
        if (isActive) return;
        isActive = true;

        // Create container
        outlineRoot = new GameObject("TableOutline");
        outlineRoot.transform.SetParent(transform, false);
        outlineRoot.transform.localPosition = Vector3.zero;
        outlineRoot.transform.localRotation = Quaternion.identity;
        outlineRoot.transform.localScale = Vector3.one;

        // Create unlit emissive material
        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineMaterial.color = glowColor;

        // ── Get local-space bounds from the mesh ──
        Bounds localBounds = GetLocalBounds();
        float topY = localBounds.max.y + heightOffset;
        float botY = localBounds.min.y + heightOffset;
        float minX = localBounds.min.x;
        float maxX = localBounds.max.x;
        float minZ = localBounds.min.z;
        float maxZ = localBounds.max.z;

        // ── TOP EDGE (table surface outline) ──
        Vector3[] topCorners = new Vector3[]
        {
            new Vector3(minX, topY, minZ), // front-left
            new Vector3(maxX, topY, minZ), // front-right
            new Vector3(maxX, topY, maxZ), // back-right
            new Vector3(minX, topY, maxZ), // back-left
        };
        CreateLoopLine("TopOutline", topCorners, lineWidth);

        // ── BOTTOM EDGE (feet outline, slightly inset) ──
        float inX = legInset;
        float inZ = legInset;
        Vector3[] botCorners = new Vector3[]
        {
            new Vector3(minX + inX, botY, minZ + inZ),
            new Vector3(maxX - inX, botY, minZ + inZ),
            new Vector3(maxX - inX, botY, maxZ - inZ),
            new Vector3(minX + inX, botY, maxZ - inZ),
        };
        CreateLoopLine("BottomOutline", botCorners, legLineWidth);

        // ── 4 VERTICAL LEGS connecting top to bottom ──
        for (int i = 0; i < 4; i++)
        {
            // Top corner to corresponding bottom corner
            Vector3 top = topCorners[i];
            Vector3 bot = botCorners[i];
            CreateSegmentLine($"Leg{i}", top, bot, legLineWidth);
        }

        Debug.Log($"[TableHighlight] Outline started. Bounds: {localBounds.center}, size: {localBounds.size}");
    }

    /// <summary>
    /// Create a looping LineRenderer (closed rectangle).
    /// Points are in LOCAL space — the LineRenderer is a child of the table.
    /// </summary>
    private void CreateLoopLine(string name, Vector3[] localPoints, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(outlineRoot.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = outlineMaterial;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = false; // Local space — follows the table's transform
        lr.loop = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = glowColor;
        lr.endColor = glowColor;
        lr.numCornerVertices = 3;
        lr.sortingOrder = 5;

        lr.positionCount = localPoints.Length;
        for (int i = 0; i < localPoints.Length; i++)
            lr.SetPosition(i, localPoints[i]);

        allLines.Add(lr);
    }

    /// <summary>
    /// Create a single line segment (for a table leg).
    /// </summary>
    private void CreateSegmentLine(string name, Vector3 localStart, Vector3 localEnd, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(outlineRoot.transform, false);

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = outlineMaterial;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = glowColor;
        lr.endColor = glowColor;
        lr.sortingOrder = 5;

        lr.positionCount = 2;
        lr.SetPosition(0, localStart);
        lr.SetPosition(1, localEnd);

        allLines.Add(lr);
    }

    /// <summary>
    /// Stop and remove the highlight effect.
    /// </summary>
    public void StopHighlight()
    {
        isActive = false;

        if (outlineRoot != null)
        {
            Destroy(outlineRoot);
            outlineRoot = null;
        }

        if (outlineMaterial != null)
        {
            Destroy(outlineMaterial);
            outlineMaterial = null;
        }

        allLines.Clear();

        Debug.Log("[TableHighlight] Outline stopped.");
        Destroy(this);
    }

    void Update()
    {
        if (!isActive || allLines.Count == 0) return;

        // Pulse color
        pulseTimer += Time.deltaTime * pulseSpeed;
        float t = (Mathf.Sin(pulseTimer) + 1f) * 0.5f; // 0 → 1
        Color currentColor = Color.Lerp(dimColor, glowColor, t);

        foreach (var lr in allLines)
        {
            if (lr == null) continue;
            lr.startColor = currentColor;
            lr.endColor = currentColor;

            // Subtle width pulse
            float baseWidth = lr.gameObject.name.Contains("Leg") || lr.gameObject.name.Contains("Bottom")
                ? legLineWidth : lineWidth;
            float w = Mathf.Lerp(baseWidth * 0.75f, baseWidth * 1.25f, t);
            lr.startWidth = w;
            lr.endWidth = w;
        }

        if (outlineMaterial != null)
            outlineMaterial.color = currentColor;

        // Auto-remove when the player opens the puzzle table
        if (PuzzleTableController.IsOpen)
        {
            StopHighlight();
        }
    }

    /// <summary>
    /// Get the mesh's local-space bounds (not world-space).
    /// </summary>
    private Bounds GetLocalBounds()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
            return mf.sharedMesh.bounds;

        // Fallback: use collider bounds converted to local
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
            return new Bounds(box.center, box.size);

        if (col != null)
        {
            Bounds worldBounds = col.bounds;
            Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
            Vector3 localSize = new Vector3(
                worldBounds.size.x / transform.lossyScale.x,
                worldBounds.size.y / transform.lossyScale.y,
                worldBounds.size.z / transform.lossyScale.z
            );
            return new Bounds(localCenter, localSize);
        }

        return new Bounds(Vector3.zero, new Vector3(2f, 1f, 2f));
    }
}
