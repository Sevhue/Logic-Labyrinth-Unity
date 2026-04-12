using UnityEngine;

/// <summary>
/// Ensures core gameplay managers exist even when starting play directly in a gameplay scene
/// (for example Chapter3) instead of entering from the Main menu flow.
/// </summary>
public static class GameplayRuntimeBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCoreManagers()
    {
        EnsureSingleton<LevelManager>("LevelManager");
        EnsureSingleton<PauseMenuController>("PauseMenuController");
    }

    private static void EnsureSingleton<T>(string name) where T : MonoBehaviour
    {
        if (Object.FindAnyObjectByType<T>() != null)
            return;

        GameObject go = new GameObject(name);
        go.AddComponent<T>();
        Object.DontDestroyOnLoad(go);
    }
}
