using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StarterAssets;

/// <summary>
/// Shows a full-screen parchment tutorial card the FIRST time each gate type is collected.
/// Press E to dismiss. After closing, a TIP appears telling players about the Gate Journal (J).
///
/// Place images at: Assets/Resources/GateTutorials/New_NOTGate.jpg, New_ANDGate.jpg, New_ORGate.jpg
/// </summary>
public class GateTutorialCard : MonoBehaviour
{
    // ── Singleton ──
    private static GateTutorialCard _instance;

    // ── State ──
    /// <summary>Set of gate types (NOT/AND/OR) for which the tutorial card has already been shown.</summary>
    public static HashSet<string> SeenGateTypes { get; private set; } = new HashSet<string>();

    private GameObject root;
    private bool isOpen = false;

    // ── Player control refs ──
    private FirstPersonController fpc;
    private StarterAssetsInputs inputs;

    // ── Resource paths ──
    private static readonly Dictionary<string, string> GateImagePaths = new Dictionary<string, string>
    {
        { "NOT", "GateTutorials/New_NOTGate" },
        { "AND", "GateTutorials/New_ANDGate" },
        { "OR",  "GateTutorials/New_ORGate"  },
    };

    // ── Public API ──

    public static bool IsOpen => _instance != null && _instance.isOpen;

    /// <summary>
    /// Call after a gate is collected. Shows the tutorial card if this is the
    /// first time collecting this gate type. Safe to call every collection — 
    /// it no-ops on repeat pickups.
    /// </summary>
    public static void ShowCard(string gateType)
    {
        string key = gateType.ToUpper();
        if (!GateImagePaths.ContainsKey(key)) return;
        if (SeenGateTypes.Contains(key)) return;

        SeenGateTypes.Add(key);
        EnsureInstance();
        if (_instance != null)
            _instance.StartCoroutine(_instance.ShowCardRoutine(key));
    }

    /// <summary>Clears the seen-gates record. Call when starting a new game.</summary>
    public static void ResetSeenGates()
    {
        SeenGateTypes.Clear();
    }

    // ── Lifecycle ──

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.E))
            CloseCard();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ── Private helpers ──

    private static void EnsureInstance()
    {
        if (_instance != null) return;
        _instance = FindAnyObjectByType<GateTutorialCard>();
        if (_instance != null) return;

        GameObject go = new GameObject("GateTutorialCard");
        _instance = go.AddComponent<GateTutorialCard>();
        DontDestroyOnLoad(go);
    }

    private IEnumerator ShowCardRoutine(string key)
    {
        // Wait one frame so the pickup animation/notification settles
        yield return null;

        Texture2D tex = Resources.Load<Texture2D>(GateImagePaths[key]);
        if (tex == null)
        {
            Debug.LogWarning($"[GateTutorialCard] Image not found at Resources/{GateImagePaths[key]}. " +
                             "Make sure the image is in Assets/Resources/GateTutorials/");
            yield break;
        }

        BuildUI(tex);
        SetPlayerControls(false);
        isOpen = true;
    }

    private void BuildUI(Texture2D tex)
    {
        if (root != null) Destroy(root);

        root = new GameObject("GateTutorialCardRoot");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1400; // Above TipOverlay (1250) and GateJournal (1350)

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // ── Dark dim overlay (full screen) ──
        GameObject dimGO = new GameObject("Dim");
        dimGO.transform.SetParent(root.transform, false);
        RectTransform dimRT = dimGO.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;
        Image dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.65f);
        dimImg.raycastTarget = true; // Block clicks through

        // ── Parchment card (centered, wider presentation) ──
        GameObject cardGO = new GameObject("Card");
        cardGO.transform.SetParent(root.transform, false);
        RectTransform cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.12f, 0.20f);
        cardRT.anchorMax = new Vector2(0.88f, 0.84f);
        cardRT.offsetMin = Vector2.zero;
        cardRT.offsetMax = Vector2.zero;

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        Image cardImg = cardGO.AddComponent<Image>();
        cardImg.sprite = sprite;
        cardImg.preserveAspect = true;
        cardImg.raycastTarget = false;

        // ── "PRESS E TO CLOSE" hint at the bottom ──
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Cinzel-VariableFont_wght SDF");
        if (font == null) font = Resources.Load<TMP_FontAsset>("Cinzel-VariableFont_wght SDF");

        GameObject closeLabelGO = new GameObject("CloseLabel");
        closeLabelGO.transform.SetParent(root.transform, false);
        RectTransform closeLabelRT = closeLabelGO.AddComponent<RectTransform>();
        closeLabelRT.anchorMin = new Vector2(0.20f, 0.04f);
        closeLabelRT.anchorMax = new Vector2(0.80f, 0.13f);
        closeLabelRT.offsetMin = Vector2.zero;
        closeLabelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI closeTMP = closeLabelGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text = "PRESS  E  TO  CLOSE";
        closeTMP.fontSize = 26f;
        closeTMP.fontStyle = FontStyles.Bold;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color = new Color(1f, 0.92f, 0.68f, 1f);
        closeTMP.raycastTarget = false;
        if (font != null) closeTMP.font = font;
    }

    private void CloseCard()
    {
        isOpen = false;
        if (root != null) Destroy(root);
        root = null;
        SetPlayerControls(true);

        // Hint about the journal after the player closes the card
        TipOverlayUI.ShowTip("Press J to review your Gate Journal.", 7f, 40f);
    }

    private void SetPlayerControls(bool enable)
    {
        if (fpc == null) fpc = FindAnyObjectByType<FirstPersonController>();
        if (inputs == null) inputs = FindAnyObjectByType<StarterAssetsInputs>();

        if (fpc != null) fpc.enabled = enable;
        if (inputs != null) inputs.enabled = enable;

        Cursor.lockState = enable ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enable;
    }
}
