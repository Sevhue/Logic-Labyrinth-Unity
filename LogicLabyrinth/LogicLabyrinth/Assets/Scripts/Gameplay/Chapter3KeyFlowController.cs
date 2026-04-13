using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Chapter3-only key flow:
/// - randomly disables one of four key pickups (25% each) as a decoy
/// - requires collecting the remaining three keys before Success_Door can unlock
/// - keeps KEY visible in hotbar after first key pickup
/// </summary>
public class Chapter3KeyFlowController : MonoBehaviour
{
    public static int CurrentCollectedKeyCount { get; private set; }

    private readonly List<CollectibleKey> trackedKeys = new List<CollectibleKey>();
    private bool initialized;
    private bool lastHasInventoryKey;
    private bool lastCanUnlockDoor;
    private const int RequiredKeyCount = 3;

    private void Start()
    {
        InitializeIfNeeded();
    }

    private void Update()
    {
        if (!initialized)
            InitializeIfNeeded();

        if (!initialized)
            return;

        int collected = 0;
        for (int i = 0; i < trackedKeys.Count; i++)
        {
            CollectibleKey key = trackedKeys[i];
            if (key != null && key.IsCollected)
                collected++;
        }

        bool hasInventoryKey = collected > 0;
        bool canUnlockDoor = collected >= RequiredKeyCount;
        CurrentCollectedKeyCount = collected;

        // Keep KEY shown in hotbar after first Chapter3 key pickup.
        TutorialDoor.PlayerHasKey = hasInventoryKey;

        // Door unlock state depends strictly on 3 collected keys.
        SuccessDoor.PlayerHasSuccessKey = canUnlockDoor;

        if (hasInventoryKey != lastHasInventoryKey || canUnlockDoor != lastCanUnlockDoor)
        {
            if (GameInventoryUI.Instance != null)
                GameInventoryUI.Instance.RefreshFromInventory();

            lastHasInventoryKey = hasInventoryKey;
            lastCanUnlockDoor = canUnlockDoor;
        }
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Chapter3")
            return;

        trackedKeys.Clear();
        CollectibleKey[] allKeys = FindObjectsByType<CollectibleKey>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < allKeys.Length; i++)
        {
            CollectibleKey key = allKeys[i];
            if (key == null) continue;
            if (key.gameObject == null) continue;
            if (!key.gameObject.name.ToLowerInvariant().StartsWith("key")) continue;

            // Force shiny/glowing key style in Chapter3.
            key.keyType = CollectibleKey.KeyType.Success;
            trackedKeys.Add(key);
        }

        // Keep deterministic ordering for stable random choice across named keys.
        trackedKeys.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        if (trackedKeys.Count >= 4)
        {
            int decoyIndex = Random.Range(0, trackedKeys.Count);
            CollectibleKey decoy = trackedKeys[decoyIndex];
            if (decoy != null)
                decoy.gameObject.SetActive(false);

            // Remove decoy from tracked set so exactly 3 remain collectible.
            trackedKeys.RemoveAt(decoyIndex);
        }

        // Make remaining Chapter3 keys clearly collectible and visibly glowing.
        for (int i = 0; i < trackedKeys.Count; i++)
        {
            CollectibleKey key = trackedKeys[i];
            if (key == null) continue;

            key.keyType = CollectibleKey.KeyType.Success;
            key.successGlowDuration = 999f;
            key.shineIntensity = Mathf.Max(5f, key.shineIntensity);
            key.shineRange = Mathf.Max(6f, key.shineRange);

            SphereCollider sc = key.GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                sc.radius = Mathf.Max(sc.radius, 0.45f);
            }

            // Re-trigger OnEnable so success-key glow starts after runtime keyType change.
            if (key.gameObject.activeSelf)
            {
                key.gameObject.SetActive(false);
                key.gameObject.SetActive(true);
            }
        }

        // Hard reset key flags for this level flow.
        CurrentCollectedKeyCount = 0;
        TutorialDoor.PlayerHasKey = false;
        SuccessDoor.PlayerHasSuccessKey = false;
        lastHasInventoryKey = false;
        lastCanUnlockDoor = false;

        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();

        initialized = true;
        Debug.Log($"[Chapter3KeyFlow] Initialized. Active keys: {trackedKeys.Count}. Required: {RequiredKeyCount}.");
    }

    private void OnDisable()
    {
        CurrentCollectedKeyCount = 0;
    }
}

public static class Chapter3KeyFlowBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryEnsureController();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryEnsureController();
    }

    private static void TryEnsureController()
    {
        if (SceneManager.GetActiveScene().name != "Chapter3")
            return;

        Chapter3KeyFlowController existing = Object.FindAnyObjectByType<Chapter3KeyFlowController>();
        if (existing != null)
            return;

        GameObject go = new GameObject("Chapter3KeyFlowController");
        go.AddComponent<Chapter3KeyFlowController>();
    }
}
