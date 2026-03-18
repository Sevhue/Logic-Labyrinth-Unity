using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Moves a trap mesh up/down to create a repeating spike hazard motion.
/// </summary>
public class SpikeTrapOscillator : MonoBehaviour
{
    [SerializeField] private float travelDistance = 0.45f;
    [SerializeField] private float upDuration = 1.0f;
    [SerializeField] private float holdUpDuration = 2.0f;
    [SerializeField] private float downDuration = 1.0f;
    [SerializeField] private bool randomizeBySpikeTrapGroup = true;
    [SerializeField] private bool useLocalSpace = true;
    [SerializeField] private Vector3 axis = Vector3.up;
    [SerializeField] private float startTimeOffsetSeconds;

    private static readonly Dictionary<int, float> trapGroupOffsets = new Dictionary<int, float>();

    private Vector3 startLocalPosition;
    private Vector3 startWorldPosition;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        startWorldPosition = transform.position;

        if (randomizeBySpikeTrapGroup)
            startTimeOffsetSeconds = ResolveSpikeTrapGroupOffset(transform);
    }

    private void Update()
    {
        float rise = Mathf.Max(0.05f, upDuration);
        float hold = Mathf.Max(0f, holdUpDuration);
        float fall = Mathf.Max(0.05f, downDuration);
        float totalCycle = rise + hold + fall;

        float cycleTime = Mathf.Repeat(Time.time + startTimeOffsetSeconds, totalCycle);
        float normalized;

        if (cycleTime < rise)
        {
            // Move up
            normalized = cycleTime / rise;
        }
        else if (cycleTime < rise + hold)
        {
            // Stay at top
            normalized = 1f;
        }
        else
        {
            // Move down
            normalized = 1f - ((cycleTime - rise - hold) / fall);
        }

        Vector3 dir = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector3.up;
        Vector3 delta = dir * (travelDistance * normalized);

        if (useLocalSpace)
        {
            transform.localPosition = startLocalPosition + delta;
        }
        else
        {
            transform.position = startWorldPosition + delta;
        }
    }

    private static float ResolveSpikeTrapGroupOffset(Transform spikeTransform)
    {
        Transform trapRoot = FindSpikeTrapRoot(spikeTransform);
        if (trapRoot == null)
            return 0f;

        int key = trapRoot.GetInstanceID();
        if (trapGroupOffsets.TryGetValue(key, out float existing))
            return existing;

        // 4s default cycle (up 1s + hold 2s + down 1s), randomized per SpikeTrap group.
        float offset = Random.Range(0f, 4f);
        trapGroupOffsets[key] = offset;
        return offset;
    }

    private static Transform FindSpikeTrapRoot(Transform from)
    {
        Transform p = from;
        while (p != null)
        {
            string name = p.name.ToLowerInvariant();
            if (name.StartsWith("spiketrap"))
                return p;
            p = p.parent;
        }

        return null;
    }
}

/// <summary>
/// Damages the player while touching a spike hazard.
/// </summary>
public class SpikeTrapHazard : MonoBehaviour
{
    [SerializeField] private float damageAmount = 15f;
    [SerializeField] private float hitCooldown = 0.6f;
    [SerializeField] private bool useProximityFallback = true;
    [SerializeField] private float bodyProbeRadius = 0.42f;
    [SerializeField] private float bodyProbeVerticalOffset = 0.10f;
    [SerializeField] private float tipProbeRadius = 0.28f;
    [SerializeField] private float tipProbeVerticalOffset = 0.45f;
    [SerializeField] private float proximityScanInterval = 0.03f;

    private readonly Dictionary<int, float> nextDamageTimeByPlayer = new Dictionary<int, float>();
    private readonly Collider[] proximityHits = new Collider[20];
    private float nextProximityScanTime;

    public void TryDamage(GameObject playerRoot)
    {
        if (playerRoot == null) return;

        StarterAssets.FirstPersonController fpc = playerRoot.GetComponentInParent<StarterAssets.FirstPersonController>();
        if (fpc == null) return;

        int playerId = fpc.GetInstanceID();
        float now = Time.time;

        if (nextDamageTimeByPlayer.TryGetValue(playerId, out float nextAllowedTime) && now < nextAllowedTime)
            return;

        nextDamageTimeByPlayer[playerId] = now + Mathf.Max(0.05f, hitCooldown);
        fpc.ApplyDamage(damageAmount);
    }

    private void TryDamage(Collider other)
    {
        if (other == null) return;
        TryDamage(other.gameObject);
    }

    private void TryDamage(Collision other)
    {
        if (other == null || other.collider == null) return;
        TryDamage(other.collider.gameObject);
    }

    private void Update()
    {
        if (!useProximityFallback) return;
        if (Time.time < nextProximityScanTime) return;

        nextProximityScanTime = Time.time + Mathf.Max(0.01f, proximityScanInterval);

        // Probe both body and tip zones so even tiny contact on the spike point still hurts.
        ScanAndDamage(transform.position + (Vector3.up * bodyProbeVerticalOffset), bodyProbeRadius);
        ScanAndDamage(transform.position + (Vector3.up * tipProbeVerticalOffset), tipProbeRadius);
    }

    private void ScanAndDamage(Vector3 center, float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(center, Mathf.Max(0.05f, radius), proximityHits, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider c = proximityHits[i];
            if (c == null) continue;
            TryDamage(c.gameObject);
            proximityHits[i] = null;
        }
    }

    private void OnTriggerEnter(Collider other) => TryDamage(other);
    private void OnTriggerStay(Collider other) => TryDamage(other);
    private void OnCollisionEnter(Collision other) => TryDamage(other);
    private void OnCollisionStay(Collision other) => TryDamage(other);

    private void OnDrawGizmosSelected()
    {
        if (!useProximityFallback) return;

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.30f);
        Gizmos.DrawSphere(transform.position + (Vector3.up * bodyProbeVerticalOffset), Mathf.Max(0.05f, bodyProbeRadius));

        Gizmos.color = new Color(1f, 0.45f, 0.2f, 0.35f);
        Gizmos.DrawSphere(transform.position + (Vector3.up * tipProbeVerticalOffset), Mathf.Max(0.05f, tipProbeRadius));
    }
}

/// <summary>
/// Automatically attaches oscillation to trap-like objects in Level 5/6 scenes.
/// This avoids manual per-object setup and keeps existing scene layout intact.
/// </summary>
public static class SpikeTrapOscillatorBootstrap
{
    private static bool initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (initialized) return;
        initialized = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAttachToCurrentScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryAttachToCurrentScene();
    }

    private static void TryAttachToCurrentScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Level5" && scene.name != "Level6") return;

        int oscillatorAdded = 0;
        int hazardAdded = 0;

        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        bool hasSpikeParentGrouping = HasAnySpikeParent(all);
        for (int i = 0; i < all.Length; i++)
        {
            Transform tr = all[i];
            if (tr == null) continue;

            // Prefer explicit SpikeParent hierarchy when present.
            // Fallback keeps compatibility with older scenes.
            if (hasSpikeParentGrouping)
            {
                if (!IsSpikePiece(tr) || !HasSpikeParentAncestor(tr)) continue;
            }
            else
            {
                if (!IsSpikePiece(tr) || !HasTrapAncestor(tr)) continue;
            }

            if (tr.GetComponent<MeshFilter>() == null) continue;

            if (tr.GetComponent<SpikeTrapOscillator>() == null)
            {
                tr.gameObject.AddComponent<SpikeTrapOscillator>();
                oscillatorAdded++;
            }

            if (tr.GetComponent<SpikeTrapHazard>() == null)
            {
                tr.gameObject.AddComponent<SpikeTrapHazard>();
                hazardAdded++;
            }
        }

        if (oscillatorAdded > 0 || hazardAdded > 0)
            Debug.Log($"[SpikeTrapOscillator] Scene '{scene.name}': added {oscillatorAdded} oscillators and {hazardAdded} hazards to spike pieces only.");
    }

    private static bool HasAnySpikeParent(Transform[] all)
    {
        for (int i = 0; i < all.Length; i++)
        {
            Transform tr = all[i];
            if (tr == null) continue;
            if (tr.name.ToLowerInvariant() == "spikeparent") return true;
        }
        return false;
    }

    private static bool IsSpikePiece(Transform tr)
    {
        string n = tr.name.ToLowerInvariant();
        if (n == "spike") return true;
        if (n.StartsWith("spike_") || n.StartsWith("spike-")) return true;
        if (n.Contains("spike") && !n.Contains("trap")) return true;
        return false;
    }

    private static bool HasTrapAncestor(Transform tr)
    {
        Transform p = tr.parent;
        while (p != null)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("trap")) return true;
            p = p.parent;
        }
        return false;
    }

    private static bool HasSpikeParentAncestor(Transform tr)
    {
        Transform p = tr.parent;
        while (p != null)
        {
            if (p.name.ToLowerInvariant() == "spikeparent") return true;
            p = p.parent;
        }
        return false;
    }
}
