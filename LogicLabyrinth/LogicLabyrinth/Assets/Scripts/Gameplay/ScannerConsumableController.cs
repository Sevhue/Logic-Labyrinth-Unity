using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ScannerConsumableController : MonoBehaviour
{
    private const string PrefConsumeTipShown = "LL_SCN_CONSUME_TIP_SHOWN";
    private static ScannerConsumableController instance;

    [Header("Input")]
    public KeyCode scanKey = KeyCode.F;

    [Header("Reveal")]
    public float revealDuration = 10f;
    public float markerHeightOffset = 0.45f;
    public Color markerColor = new Color(0.25f, 0.95f, 1f, 1f);

    private Coroutine activeRevealRoutine;
    private Interactable activeTarget;
    private GUIStyle screenMarkerStyle;
    private bool consumeTipShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("ScannerConsumableController");
        instance = go.AddComponent<ScannerConsumableController>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        consumeTipShown = PlayerPrefs.GetInt(PrefConsumeTipShown, 0) == 1;
    }

    private void Update()
    {
        if (!IsGameplayLevelScene() || IsTypingIntoInputField())
            return;

        if (PauseMenuController.IsPaused || PuzzleTableController.IsOpen || CutsceneController.IsPlaying)
            return;

        if (IsScannerSelected())
            TryShowFirstEquipTip();

        if (!WasScanPressed())
            return;

        if (!IsScannerSelected())
        {
            ShowHint("Select SCN slot first.");
            return;
        }

        TryUseScanner();
    }

    private void TryUseScanner()
    {
        if (activeRevealRoutine != null)
        {
            ShowHint("Scanner is cooling down.");
            return;
        }

        Interactable target = FindNearestGate();
        if (target == null)
        {
            ShowHint("No gate found to reveal.");
            return;
        }

        if (AccountManager.Instance == null || !AccountManager.Instance.ConsumeScanner(1))
        {
            ShowHint("No scanner charges left.");
            if (GameInventoryUI.Instance != null)
                GameInventoryUI.Instance.RefreshFromInventory();
            return;
        }

        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();

        int remaining = AccountManager.Instance != null ? AccountManager.Instance.GetScannerCount() : 0;
        ShowHint($"Scanner revealed {target.gateType} gate. {remaining} left.");
        activeRevealRoutine = StartCoroutine(RevealGateRoutine(target));
    }

    private IEnumerator RevealGateRoutine(Interactable target)
    {
        activeTarget = target;
        float elapsed = 0f;

        while (elapsed < revealDuration)
        {
            if (target == null)
                break;

            elapsed += Time.deltaTime;
            UpdateScannerOverlay(revealDuration - elapsed);

            yield return null;
        }

        ClearScannerOverlay();
        activeTarget = null;
        activeRevealRoutine = null;
    }

    private GameObject CreateMarkerForTarget(Interactable target)
    {
        if (target == null)
            return null;

        Vector3 basePosition;
        float topY;
        ResolveGateAnchor(target, out basePosition, out topY);
        float beaconHeight = Mathf.Clamp((topY - basePosition.y) + 1.2f, 1.2f, 2.8f);

        GameObject marker = new GameObject("ScannerRevealMarker");
        marker.name = "ScannerRevealMarker";
        marker.transform.SetParent(target.transform, true);
        marker.transform.position = basePosition;
        marker.transform.localScale = Vector3.one;

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Beam";
        beam.transform.SetParent(marker.transform, false);
        beam.transform.localPosition = new Vector3(0f, beaconHeight * 0.5f, 0f);
        beam.transform.localRotation = Quaternion.identity;
        beam.transform.localScale = new Vector3(0.08f, beaconHeight * 0.5f, 0.08f);

        GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Orb";
        orb.transform.SetParent(marker.transform, false);
        orb.transform.localPosition = new Vector3(0f, beaconHeight + markerHeightOffset, 0f);
        orb.transform.localRotation = Quaternion.identity;
        orb.transform.localScale = Vector3.one * 0.28f;

        Collider beamCollider = beam.GetComponent<Collider>();
        if (beamCollider != null)
            Destroy(beamCollider);

        Collider orbCollider = orb.GetComponent<Collider>();
        if (orbCollider != null)
            Destroy(orbCollider);

        Renderer[] markerRenderers = marker.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < markerRenderers.Length; i++)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            Material material = new Material(shader);
            material.color = markerColor;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", markerColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", markerColor);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", markerColor * 4f);
            }
            markerRenderers[i].material = material;
        }

        Light light = marker.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 5f;
        light.intensity = 4f;
        light.color = markerColor;

        return marker;
    }

    private Interactable FindNearestGate()
    {
        Vector3 origin = GetScanOrigin();
        Interactable[] gates = FindObjectsByType<Interactable>(FindObjectsSortMode.None);
        Interactable best = null;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < gates.Length; i++)
        {
            Interactable gate = gates[i];
            if (gate == null || !gate.gameObject.activeInHierarchy)
                continue;

            if (gate == activeTarget)
                continue;

            float distanceSq = (gate.transform.position - origin).sqrMagnitude;
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                best = gate;
            }
        }

        return best;
    }

    private Vector3 GetScanOrigin()
    {
        Camera cam = Camera.main;
        if (cam != null)
            return cam.transform.position;

        GameObject player = GameObject.Find("FirstPersonPlayer");
        if (player != null)
            return player.transform.position;

        return transform.position;
    }

    private void OnGUI()
    {
        if (activeTarget == null)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 basePosition;
        float topY;
        ResolveGateAnchor(activeTarget, out basePosition, out topY);
        Vector3 screenPos = cam.WorldToScreenPoint(new Vector3(basePosition.x, topY + 0.6f, basePosition.z));
        if (screenPos.z <= 0f)
            return;

        EnsureScreenMarkerStyle();

        float width = 170f;
        float height = 30f;
        Rect rect = new Rect(screenPos.x - (width * 0.5f), Screen.height - screenPos.y - height, width, height);
        GUI.Label(rect, $"SCANNED {activeTarget.gateType}", screenMarkerStyle);
    }

    private void EnsureScreenMarkerStyle()
    {
        if (screenMarkerStyle != null)
            return;

        screenMarkerStyle = new GUIStyle(GUI.skin.box);
        screenMarkerStyle.alignment = TextAnchor.MiddleCenter;
        screenMarkerStyle.fontSize = 16;
        screenMarkerStyle.fontStyle = FontStyle.Bold;
        screenMarkerStyle.normal.textColor = Color.black;

        Texture2D bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(markerColor.r, markerColor.g, markerColor.b, 0.92f));
        bg.Apply();
        screenMarkerStyle.normal.background = bg;
    }

    private static void ResolveGateAnchor(Interactable target, out Vector3 basePosition, out float topY)
    {
        basePosition = target != null ? target.transform.position : Vector3.zero;
        topY = basePosition.y + 0.8f;

        if (target == null)
            return;

        Collider gateCollider = target.GetComponentInChildren<Collider>();
        if (gateCollider != null)
        {
            Bounds b = gateCollider.bounds;
            basePosition = new Vector3(b.center.x, b.min.y, b.center.z);
            topY = b.max.y;
            return;
        }

        Renderer gateRenderer = target.GetComponentInChildren<Renderer>();
        if (gateRenderer != null)
        {
            Bounds b = gateRenderer.bounds;
            basePosition = new Vector3(b.center.x, b.min.y, b.center.z);
            topY = b.max.y;
        }
    }

    private bool IsScannerSelected()
    {
        return AccountManager.Instance != null &&
               AccountManager.Instance.GetScannerCount() > 0 &&
               GameInventoryUI.Instance != null &&
               GameInventoryUI.Instance.GetSelectedItem() == GameInventoryUI.ItemType.Scanner;
    }

    private bool WasScanPressed()
    {
        bool pressed = Input.GetKeyDown(scanKey);
#if ENABLE_INPUT_SYSTEM
        if (!pressed && Keyboard.current != null)
            pressed = Keyboard.current.fKey.wasPressedThisFrame;
#endif
        return pressed;
    }

    private static bool IsGameplayLevelScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level");
    }

    private static bool IsTypingIntoInputField()
    {
        if (EventSystem.current == null) return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;

        return selected.GetComponent<TMPro.TMP_InputField>() != null ||
               selected.GetComponentInParent<TMPro.TMP_InputField>() != null;
    }

    private void ShowHint(string message)
    {
        LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
        if (levelUI != null)
        {
            levelUI.ShowCollectionMessage(message, markerColor);
            return;
        }

        Debug.Log($"[Scanner] {message}");
    }

    private void TryShowFirstEquipTip()
    {
        if (consumeTipShown)
            return;

        TipOverlayUI.ShowTip("Press F to consume the Scanner.", 7f, 40f);
        consumeTipShown = true;
        PlayerPrefs.SetInt(PrefConsumeTipShown, 1);
        PlayerPrefs.Save();
    }

    private void UpdateScannerOverlay(float secondsRemaining)
    {
        if (GameInventoryUI.Instance == null)
            return;

        int shownSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        GameInventoryUI.Instance.SetScannerOverlayText(shownSeconds > 0 ? shownSeconds.ToString() : "1", markerColor);
    }

    private void ClearScannerOverlay()
    {
        if (GameInventoryUI.Instance == null)
            return;

        GameInventoryUI.Instance.ClearScannerOverlayText();
    }
}