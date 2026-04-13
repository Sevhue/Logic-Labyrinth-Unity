using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shoots a spear (SpearAmmo) forward from a SpearBase, then resets and repeats.
/// </summary>
public class SpearShooter : MonoBehaviour
{
    [SerializeField] private float shootDistance = 6.0f;
    [SerializeField] private float shootDuration = 0.7f;
    [SerializeField] private float resetDelay = 1.2f;
    [SerializeField] private float startDelay = 0.0f;
    [SerializeField] private bool randomizeStart = true;

    private Transform ammo;
    private Vector3 ammoStartLocal;
    private Vector3 ammoEndLocal;
    private float timer;
    private enum State { Waiting, Shooting, Resetting }
    private State state = State.Waiting;

    private void Awake()
    {
        ammo = transform.Find("SpearAmmo");
        if (ammo == null)
        {
            Debug.LogWarning($"[SpearShooter] No SpearAmmo child found under {name}", this);
            enabled = false;
            return;
        }

        Collider ammoCollider = ammo.GetComponent<Collider>();
        if (ammoCollider != null)
        {
            ammoCollider.enabled = true;
            if (ammoCollider is MeshCollider meshCollider)
                meshCollider.convex = true;
            ammoCollider.isTrigger = true;
        }

        if (ammo.GetComponent<SpearAmmoHit>() == null)
            ammo.gameObject.AddComponent<SpearAmmoHit>();

        ammoStartLocal = ammo.localPosition;

        // Move perfectly straight: X increases, Y unchanged, Z unchanged
        Vector3 shootDir = new Vector3(1f, 0f, 0f).normalized;
        Vector3 ammoStartWorld = ammo.position;
        Vector3 ammoEndWorld = ammoStartWorld + shootDir * shootDistance;
        ammoEndLocal = ammo.parent.InverseTransformPoint(ammoEndWorld);
        if (randomizeStart)
            timer = Random.Range(0f, resetDelay + shootDuration);
        else
            timer = startDelay;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Waiting:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    state = State.Shooting;
                    timer = shootDuration;
                    ammo.gameObject.SetActive(true);
                }
                break;
            case State.Shooting:
                float t = 1f - (timer / shootDuration);
                ammo.localPosition = Vector3.Lerp(ammoStartLocal, ammoEndLocal, t);
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    ammo.gameObject.SetActive(false);
                    state = State.Resetting;
                    timer = resetDelay;
                }
                break;
            case State.Resetting:
                timer -= Time.deltaTime;
                if (timer <= 0f)
                {
                    ammo.localPosition = ammoStartLocal;
                    ammo.gameObject.SetActive(true);
                    state = State.Waiting;
                    timer = 0f;
                }
                break;
        }
    }
}

/// <summary>
/// Auto-attaches SpearShooter to all SpearBase objects in the scene.
/// </summary>
public static class SpearShooterBootstrap
{
    private static bool initialized;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (initialized) return;
        initialized = true;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAttachAll();
    }
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryAttachAll();
    private static void TryAttachAll()
    {
        var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int added = 0;
        foreach (var tr in all)
        {
            if (tr == null) continue;
            if (!tr.name.ToLowerInvariant().StartsWith("spearbase")) continue;
            if (tr.GetComponent<SpearShooter>() == null)
            {
                tr.gameObject.AddComponent<SpearShooter>();
                added++;
            }
        }
        if (added > 0)
            Debug.Log($"[SpearShooter] Added to {added} SpearBase objects.");
    }
}
