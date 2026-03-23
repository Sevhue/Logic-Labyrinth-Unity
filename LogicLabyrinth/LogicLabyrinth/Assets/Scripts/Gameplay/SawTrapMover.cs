using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Moves a saw back and forth across its parent SawBase.
/// Attach to the moving saw object (usually named "Saw").
/// </summary>
public class SawTrapMover : MonoBehaviour
{
    [SerializeField] private bool autoUseLongestSawBaseAxis = true;
    [SerializeField] private bool moveAlongSawBaseForward = false;
    [SerializeField] private float travelDuration = 3.8f;
    [SerializeField] private float edgePauseDuration = 0.65f;
    [SerializeField] private float edgePadding = 0.12f;

    [SerializeField, Tooltip("Randomize movement phase so saws are not in sync")] private bool randomizeStartPhase = true;
    private float phaseOffset;

    private Transform sawBase;
    private Vector3 moveAxisWorld;
    private float minAlong;
    private float maxAlong;
    private Vector3 perpendicularOffset;
    private float baseY;

    private void Awake()
    {
        sawBase = FindSawBaseAncestor(transform);
        ConfigurePath();

        if (randomizeStartPhase)
        {
            // Use a deterministic hash so saws always offset the same way per scene
            int hash = gameObject.GetInstanceID();
            float r = Mathf.Abs(Mathf.Sin(hash * 0.618f));
            phaseOffset = r * 100f; // Large enough to randomize within the cycle
        }
        else
        {
            phaseOffset = 0f;
        }
    }

    private void Update()
    {
        if (sawBase == null)
            return;
        if (maxAlong - minAlong <= 0.02f)
            return;

        float travel = Mathf.Max(0.25f, travelDuration);
        float hold = Mathf.Max(0f, edgePauseDuration);
        float total = (travel * 2f) + (hold * 2f);
        float t = Mathf.Repeat(Time.time + phaseOffset, total);

        float along;
        if (t < travel)
        {
            float k = t / travel;
            along = Mathf.Lerp(minAlong, maxAlong, k);
        }
        else if (t < travel + hold)
        {
            along = maxAlong;
        }
        else if (t < (travel * 2f) + hold)
        {
            float k = (t - travel - hold) / travel;
            along = Mathf.Lerp(maxAlong, minAlong, k);
        }
        else
        {
            along = minAlong;
        }

        Vector3 p = perpendicularOffset + (moveAxisWorld * along);
        p.y = baseY;
        transform.position = p;
    }

    private void ConfigurePath()
    {
        if (sawBase == null)
            return;

        Bounds b;
        if (!TryGetSawBaseBounds(sawBase, out b))
            return;

        Vector3 axis;
        if (autoUseLongestSawBaseAxis)
        {
            float forwardSpan = GetProjectedHorizontalSpan(b, sawBase.forward);
            float rightSpan = GetProjectedHorizontalSpan(b, sawBase.right);
            axis = forwardSpan >= rightSpan ? sawBase.forward : sawBase.right;
        }
        else
        {
            axis = moveAlongSawBaseForward ? sawBase.forward : sawBase.right;
        }

        axis.y = 0f;
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.right;

        moveAxisWorld = axis.normalized;

        float axisLength = GetProjectedHorizontalSpan(b, moveAxisWorld);
        float half = Mathf.Max(0.1f, (axisLength * 0.5f) - Mathf.Max(0f, edgePadding));

        float centerAlong = Vector3.Dot(b.center, moveAxisWorld);
        minAlong = centerAlong - half;
        maxAlong = centerAlong + half;

        float currentAlong = Vector3.Dot(transform.position, moveAxisWorld);
        float clampedAlong = Mathf.Clamp(currentAlong, minAlong, maxAlong);
        float currentPerpY = transform.position.y;
        Vector3 currentPerp = transform.position - (moveAxisWorld * clampedAlong);
        currentPerp.y = 0f;

        perpendicularOffset = currentPerp;
        baseY = transform.position.y;

        if (currentPerpY != baseY)
            baseY = currentPerpY;
    }

    private static float GetProjectedHorizontalSpan(Bounds bounds, Vector3 axis)
    {
        Vector3 a = axis;
        a.y = 0f;
        if (a.sqrMagnitude < 0.0001f)
            return 0f;

        a.Normalize();
        Vector3 s = bounds.size;
        return Mathf.Abs(s.x * a.x) + Mathf.Abs(s.z * a.z);
    }

    private static Transform FindSawBaseAncestor(Transform from)
    {
        Transform p = from;
        while (p != null)
        {
            string n = p.name.ToLowerInvariant();
            if (n == "sawbase" || n.StartsWith("sawbase"))
                return p;
            p = p.parent;
        }

        return null;
    }

    private static bool TryGetSawBaseBounds(Transform baseRoot, out Bounds bounds)
    {
        Renderer rootRenderer = baseRoot.GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            bounds = rootRenderer.bounds;
            return true;
        }

        Renderer[] renderers = baseRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        bounds = new Bounds(baseRoot.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            // Exclude moving saw meshes from base extents.
            if (r.transform.name.ToLowerInvariant().StartsWith("saw"))
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (hasBounds)
            return true;

        bounds = new Bounds(baseRoot.position, new Vector3(2f, 0.2f, 0.6f));
        return true;
    }
}

/// <summary>
/// Damages player on contact with the moving saw.
/// </summary>
public class SawTrapHazard : MonoBehaviour
{
    [SerializeField] private float damageAmount = 20f;
    [SerializeField] private float hitCooldown = 0.45f;

    private readonly Dictionary<int, float> nextDamageTimeByPlayer = new Dictionary<int, float>();

    private void TryDamage(GameObject other)
    {
        if (other == null) return;

        StarterAssets.FirstPersonController fpc = other.GetComponentInParent<StarterAssets.FirstPersonController>();
        if (fpc == null) return;

        int id = fpc.GetInstanceID();
        float now = Time.time;

        if (nextDamageTimeByPlayer.TryGetValue(id, out float nextAllowed) && now < nextAllowed)
            return;

        nextDamageTimeByPlayer[id] = now + Mathf.Max(0.05f, hitCooldown);
        fpc.ApplyDamage(Mathf.Max(0f, damageAmount));
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other.gameObject);
    private void OnTriggerStay(Collider other) => TryDamage(other.gameObject);
    private void OnCollisionEnter(Collision other)
    {
        if (other != null && other.collider != null)
            TryDamage(other.collider.gameObject);
    }

    private void OnCollisionStay(Collision other)
    {
        if (other != null && other.collider != null)
            TryDamage(other.collider.gameObject);
    }
}

/// <summary>
/// Auto-attaches saw movement/damage scripts for objects named Saw under SawBase.
/// </summary>
public static class SawTrapBootstrap
{
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (initialized) return;
        initialized = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAttachInCurrentScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAttachInCurrentScene();
    }

    private static void TryAttachInCurrentScene()
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int moversAdded = 0;
        int hazardsAdded = 0;

        for (int i = 0; i < all.Length; i++)
        {
            Transform tr = all[i];
            if (tr == null) continue;

            string n = tr.name.ToLowerInvariant();
            if (n != "saw" && !n.StartsWith("saw(")) continue;

            if (!HasSawBaseAncestor(tr)) continue;

            if (tr.GetComponent<SawTrapMover>() == null)
            {
                tr.gameObject.AddComponent<SawTrapMover>();
                moversAdded++;
            }

            if (tr.GetComponent<SawTrapHazard>() == null)
            {
                tr.gameObject.AddComponent<SawTrapHazard>();
                hazardsAdded++;
            }
        }

        if (moversAdded > 0 || hazardsAdded > 0)
            Debug.Log($"[SawTrap] Added {moversAdded} movers and {hazardsAdded} hazards.");
    }

    private static bool HasSawBaseAncestor(Transform tr)
    {
        Transform p = tr.parent;
        while (p != null)
        {
            string n = p.name.ToLowerInvariant();
            if (n == "sawbase" || n.StartsWith("sawbase"))
                return true;
            p = p.parent;
        }

        return false;
    }
}
