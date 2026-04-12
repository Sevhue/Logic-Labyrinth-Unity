using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Builds a medieval-themed "Complete Your Profile" popup at runtime.
/// Shown when a new user logs in (Google or email) and is missing
/// displayName, gender, or age in their PlayerData.
/// </summary>
public class NewPlayerSetupUI : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════════
    //  SINGLETON
    // ═══════════════════════════════════════════════════════════

    private static NewPlayerSetupUI instance;
    private static GameObject uiRoot;

    // ═══════════════════════════════════════════════════════════
    //  COLOUR PALETTE  (medieval wood / gold / parchment)
    // ═══════════════════════════════════════════════════════════

    static readonly Color WOOD_DARK    = new Color(0.18f, 0.12f, 0.06f, 0.97f);
    static readonly Color WOOD_MED     = new Color(0.30f, 0.22f, 0.10f, 0.95f);
    static readonly Color WOOD_LIGHT   = new Color(0.40f, 0.30f, 0.14f, 0.90f);
    static readonly Color GOLD         = new Color(0.82f, 0.72f, 0.30f, 1.00f);
    static readonly Color GOLD_DIM     = new Color(0.65f, 0.55f, 0.22f, 1.00f);
    static readonly Color PARCHMENT    = new Color(0.92f, 0.87f, 0.73f, 1.00f);
    static readonly Color INK          = new Color(0.15f, 0.10f, 0.05f, 1.00f);
    static readonly Color FIELD_BG     = new Color(0.14f, 0.10f, 0.05f, 0.85f);
    static readonly Color BORDER       = new Color(0.60f, 0.48f, 0.18f, 0.90f);
    static readonly Color BTN_NORMAL   = new Color(0.35f, 0.26f, 0.10f, 1.00f);
    static readonly Color BTN_HOVER    = new Color(0.48f, 0.36f, 0.14f, 1.00f);
    static readonly Color BTN_PRESSED  = new Color(0.25f, 0.18f, 0.08f, 1.00f);
    static readonly Color ERROR_RED    = new Color(0.85f, 0.25f, 0.20f, 1.00f);

    // UI references (created at runtime)
    private TMP_InputField nameField;
    private TMP_Dropdown   genderDropdown;
    private TMP_Dropdown   ageDropdown;
    private TextMeshProUGUI errorText;
    private Button         confirmButton;
    private TextMeshProUGUI nameFieldVisualCaret;

    // Callback to invoke after the user finishes setup
    private System.Action onSetupComplete;

    // ═══════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Returns true if the current player's profile is incomplete
    /// (missing displayName, gender, or age).
    /// </summary>
    public static bool IsProfileIncomplete()
    {
        if (AccountManager.Instance == null) return false;
        var p = AccountManager.Instance.GetCurrentPlayer();
        if (p == null) return false;

        // Only force this popup flow for Google-auth users.
        bool isGoogleUser = !string.IsNullOrWhiteSpace(p.googleId)
            || !string.IsNullOrWhiteSpace(p.googleEmail);

        if (!isGoogleUser)
            return false;

        return string.IsNullOrWhiteSpace(p.displayName)
            || string.IsNullOrWhiteSpace(p.gender)
            || string.IsNullOrWhiteSpace(p.age);
    }

    /// <summary>
    /// Shows the profile-setup popup. When the user submits, <paramref name="onComplete"/>
    /// is invoked so the caller can proceed (e.g. ShowMainMenu).
    /// </summary>
    public static void Show(System.Action onComplete = null)
    {
        if (uiRoot != null) return; // already showing

        uiRoot = new GameObject("NewPlayerSetupUI");
        instance = uiRoot.AddComponent<NewPlayerSetupUI>();
        instance.onSetupComplete = onComplete;
        instance.BuildUI();
    }

    /// <summary>
    /// Force-closes the popup (if open) without saving.
    /// </summary>
    public static void Hide()
    {
        if (uiRoot != null)
        {
            Destroy(uiRoot);
            uiRoot = null;
            instance = null;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  BUILD THE ENTIRE UI FROM CODE
    // ═══════════════════════════════════════════════════════════

    private void BuildUI()
    {
        // ── Root Canvas ──
        Canvas canvas = uiRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        uiRoot.AddComponent<GraphicRaycaster>();
    EnsureEventSystem();

        // ── Dimmed background ──
        GameObject dimBG = CreateChild(uiRoot, "DimBG");
        RectTransform dimRT = StretchFull(dimBG);
        Image dimImg = dimBG.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.65f);
        dimImg.raycastTarget = true;

        // ── Main panel (centred) ──
        GameObject panel = CreateChild(uiRoot, "Panel");
        RectTransform panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.25f, 0.15f);
        panelRT.anchorMax = new Vector2(0.75f, 0.85f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Wood background
        Image panelBG = panel.AddComponent<Image>();
        panelBG.color = WOOD_DARK;
        panelBG.raycastTarget = true;

        // Gold border
        Outline panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = BORDER;
        panelOutline.effectDistance = new Vector2(3f, 3f);

        // Inner border for depth
        Outline panelOutline2 = panel.AddComponent<Outline>();
        panelOutline2.effectColor = new Color(0.12f, 0.08f, 0.03f, 0.8f);
        panelOutline2.effectDistance = new Vector2(6f, 6f);

        // ── Inner parchment area ──
        GameObject inner = CreateChild(panel, "Inner");
        RectTransform innerRT = inner.AddComponent<RectTransform>();
        innerRT.anchorMin = new Vector2(0.04f, 0.04f);
        innerRT.anchorMax = new Vector2(0.96f, 0.96f);
        innerRT.offsetMin = Vector2.zero;
        innerRT.offsetMax = Vector2.zero;

        Image innerBG = inner.AddComponent<Image>();
        innerBG.color = WOOD_MED;
        innerBG.raycastTarget = false;

        Outline innerOutline = inner.AddComponent<Outline>();
        innerOutline.effectColor = GOLD_DIM;
        innerOutline.effectDistance = new Vector2(2f, 2f);

        // ── Content layout ──
        GameObject content = CreateChild(inner, "Content");
        RectTransform contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.08f, 0.05f);
        contentRT.anchorMax = new Vector2(0.92f, 0.95f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 14f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(20, 20, 10, 10);

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ── Decorative top bar ──
        CreateDecorBar(content);

        // ── Title ──
        CreateLabel(content, "COMPLETE THY PROFILE", 34, GOLD, FontStyles.SmallCaps | FontStyles.Bold);

        // Subtitle
        CreateLabel(content, "The dungeon master requires thy details\nbefore thou may proceed.", 18, GOLD_DIM, FontStyles.Italic);

        // Spacer
        CreateSpacer(content, 10);

        // ── Name field ──
        CreateLabel(content, "NAME", 16, GOLD, FontStyles.SmallCaps);
        nameField = CreateInputField(content, "Enter thy name...");

        // Pre-fill with displayName or username if available
        var player = AccountManager.Instance?.GetCurrentPlayer();
        if (player != null)
        {
            if (!string.IsNullOrWhiteSpace(player.displayName))
                nameField.text = player.displayName;
            else if (!string.IsNullOrWhiteSpace(player.username))
                nameField.text = player.username;
        }

        SetupNameFieldVisualCaret();

        // ── Gender dropdown ──
        CreateLabel(content, "GENDER", 16, GOLD, FontStyles.SmallCaps);
        genderDropdown = CreateDropdown(content, new List<string>
        {
            "Select Gender...", "Male", "Female", "Other", "Prefer not to say"
        });

        // Pre-select if already set
        if (player != null && !string.IsNullOrWhiteSpace(player.gender))
        {
            int idx = genderDropdown.options.FindIndex(o => o.text.Equals(player.gender, System.StringComparison.OrdinalIgnoreCase));
            if (idx > 0) genderDropdown.value = idx;
        }

        // ── Age dropdown ──
        CreateLabel(content, "AGE", 16, GOLD, FontStyles.SmallCaps);
        List<string> ageOptions = new List<string> { "Select Age..." };
        for (int i = 10; i <= 60; i++) ageOptions.Add(i.ToString());
        ageOptions.Add("60+");
        ageDropdown = CreateDropdown(content, ageOptions);

        // Pre-select if already set
        if (player != null && !string.IsNullOrWhiteSpace(player.age))
        {
            int idx = ageDropdown.options.FindIndex(o => o.text == player.age);
            if (idx > 0) ageDropdown.value = idx;
        }

        // Spacer
        CreateSpacer(content, 8);

        // ── Error text ──
        GameObject errorGO = CreateChild(content, "ErrorText");
        errorGO.AddComponent<RectTransform>();
        LayoutElement errorLE = errorGO.AddComponent<LayoutElement>();
        errorLE.preferredHeight = 24;
        errorText = errorGO.AddComponent<TextMeshProUGUI>();
        errorText.text = "";
        errorText.fontSize = 16;
        errorText.color = ERROR_RED;
        errorText.alignment = TextAlignmentOptions.Center;
        errorText.fontStyle = FontStyles.Bold;

        // ── Confirm button ──
        confirmButton = CreateButton(content, "ENTER THE DUNGEON", OnConfirm);

        // ── Decorative bottom bar ──
        CreateDecorBar(content);

        // Show a blinking caret immediately so players know the name field is editable.
        StartCoroutine(FocusNameFieldNextFrame());

        Debug.Log("[NewPlayerSetupUI] Medieval profile setup UI built.");
    }

    private IEnumerator FocusNameFieldNextFrame()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (nameField == null)
            yield break;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(nameField.gameObject);
        }

        nameField.ForceLabelUpdate();
        nameField.Select();
        nameField.ActivateInputField();
        nameField.caretPosition = nameField.text.Length;
        nameField.selectionAnchorPosition = nameField.text.Length;
        nameField.selectionFocusPosition = nameField.text.Length;
        UpdateNameFieldVisualCaret();
        StartCoroutine(BlinkNameFieldVisualCaret());
    }

    private void SetupNameFieldVisualCaret()
    {
        if (nameField == null || nameField.textViewport == null || nameField.textComponent == null)
            return;

        GameObject caretGO = CreateChild(nameField.textViewport.gameObject, "VisibleCaret");
        RectTransform caretRT = caretGO.AddComponent<RectTransform>();
        caretRT.anchorMin = new Vector2(0f, 0.5f);
        caretRT.anchorMax = new Vector2(0f, 0.5f);
        caretRT.pivot = new Vector2(0f, 0.5f);
        caretRT.sizeDelta = new Vector2(16f, 28f);

        nameFieldVisualCaret = caretGO.AddComponent<TextMeshProUGUI>();
        nameFieldVisualCaret.text = "|";
        nameFieldVisualCaret.fontSize = nameField.textComponent.fontSize;
        nameFieldVisualCaret.color = GOLD;
        nameFieldVisualCaret.alignment = TextAlignmentOptions.MidlineLeft;
        nameFieldVisualCaret.raycastTarget = false;

        nameField.onValueChanged.AddListener(_ => UpdateNameFieldVisualCaret());
        nameField.onSelect.AddListener(_ => UpdateNameFieldVisualCaret());
        nameField.onDeselect.AddListener(_ =>
        {
            if (nameFieldVisualCaret != null)
                nameFieldVisualCaret.enabled = false;
        });

        UpdateNameFieldVisualCaret();
    }

    private IEnumerator BlinkNameFieldVisualCaret()
    {
        while (nameFieldVisualCaret != null)
        {
            bool shouldShow = nameField != null && nameField.isFocused;
            nameFieldVisualCaret.enabled = shouldShow && !nameFieldVisualCaret.enabled;
            UpdateNameFieldVisualCaret();
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    private void UpdateNameFieldVisualCaret()
    {
        if (nameField == null || nameFieldVisualCaret == null || nameField.textComponent == null || nameField.textViewport == null)
            return;

        RectTransform caretRT = nameFieldVisualCaret.rectTransform;
        RectTransform viewportRT = nameField.textViewport;
        string currentText = nameField.text ?? string.Empty;
        float textWidth = nameField.textComponent.GetPreferredValues(currentText).x;
        float maxX = Mathf.Max(0f, viewportRT.rect.width - 10f);
        caretRT.anchoredPosition = new Vector2(Mathf.Min(textWidth + 2f, maxX), 0f);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject esGO = new GameObject("EventSystem_Runtime");
        esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        esGO.AddComponent<StandaloneInputModule>();
#endif
    }

    // ═══════════════════════════════════════════════════════════
    //  CONFIRM HANDLER
    // ═══════════════════════════════════════════════════════════

    private void OnConfirm()
    {
        string playerName = nameField.text.Trim();
        string gender = genderDropdown.value > 0 ? genderDropdown.options[genderDropdown.value].text : "";
        string age = ageDropdown.value > 0 ? ageDropdown.options[ageDropdown.value].text : "";

        // ── Validation ──
        if (string.IsNullOrWhiteSpace(playerName))
        {
            errorText.text = "Thou must provide a name!";
            return;
        }
        if (playerName.Length < 2 || playerName.Length > 24)
        {
            errorText.text = "Name must be 2-24 characters.";
            return;
        }
        if (string.IsNullOrWhiteSpace(gender))
        {
            errorText.text = "Please select thy gender.";
            return;
        }
        if (string.IsNullOrWhiteSpace(age))
        {
            errorText.text = "Please select thy age.";
            return;
        }

        errorText.text = "";

        // ── Save to PlayerData & Firebase ──
        var p = AccountManager.Instance?.GetCurrentPlayer();
        if (p != null)
        {
            p.displayName = playerName;
            p.gender = gender;
            p.age = age;

            AccountManager.Instance.SavePlayerProgress(success =>
            {
                if (success)
                    Debug.Log("[NewPlayerSetupUI] Profile saved to Firebase successfully.");
                else
                    Debug.LogWarning("[NewPlayerSetupUI] Firebase save failed, but local data is updated.");
            });
        }

        Debug.Log($"[NewPlayerSetupUI] Profile set: name='{playerName}', gender='{gender}', age='{age}'");

        // Close the popup
        System.Action callback = onSetupComplete;
        Hide();
        callback?.Invoke();
    }

    // ═══════════════════════════════════════════════════════════
    //  UI BUILDER HELPERS
    // ═══════════════════════════════════════════════════════════

    private GameObject CreateChild(GameObject parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private RectTransform StretchFull(GameObject go)
    {
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private void CreateLabel(GameObject parent, string text, float fontSize, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject go = CreateChild(parent, "Label");
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = fontSize + 12;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
    }

    private void CreateSpacer(GameObject parent, float height)
    {
        GameObject go = CreateChild(parent, "Spacer");
        go.AddComponent<RectTransform>();
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    private void CreateDecorBar(GameObject parent)
    {
        GameObject go = CreateChild(parent, "DecorBar");
        go.AddComponent<RectTransform>();

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 3;

        Image img = go.AddComponent<Image>();
        img.color = GOLD_DIM;
        img.raycastTarget = false;
    }

    private TMP_InputField CreateInputField(GameObject parent, string placeholder)
    {
        // Container
        GameObject container = CreateChild(parent, "InputField");
        RectTransform containerRT = container.AddComponent<RectTransform>();

        LayoutElement le = container.AddComponent<LayoutElement>();
        le.preferredHeight = 48;
        le.flexibleWidth = 1;

        Image bg = container.AddComponent<Image>();
        bg.color = FIELD_BG;
        bg.raycastTarget = true;

        Outline ol = container.AddComponent<Outline>();
        ol.effectColor = BORDER;
        ol.effectDistance = new Vector2(1.5f, 1.5f);

        // Text area
        GameObject textArea = CreateChild(container, "TextArea");
        RectTransform textAreaRT = textArea.AddComponent<RectTransform>();
        textAreaRT.anchorMin = Vector2.zero;
        textAreaRT.anchorMax = Vector2.one;
        textAreaRT.offsetMin = new Vector2(12f, 4f);
        textAreaRT.offsetMax = new Vector2(-12f, -4f);
        textArea.AddComponent<RectMask2D>();

        // Input text
        GameObject textGO = CreateChild(textArea, "Text");
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI inputTMP = textGO.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 20;
        inputTMP.color = PARCHMENT;
        inputTMP.alignment = TextAlignmentOptions.MidlineLeft;
        inputTMP.richText = false;

        // Placeholder
        GameObject phGO = CreateChild(textArea, "Placeholder");
        RectTransform phRT = phGO.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero;
        phRT.offsetMax = Vector2.zero;

        TextMeshProUGUI phTMP = phGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = placeholder;
        phTMP.fontSize = 20;
        phTMP.color = new Color(PARCHMENT.r, PARCHMENT.g, PARCHMENT.b, 0.4f);
        phTMP.alignment = TextAlignmentOptions.MidlineLeft;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.richText = false;

        // TMP_InputField
        TMP_InputField inputField = container.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRT;
        inputField.textComponent = inputTMP;
        inputField.placeholder = phTMP;
        inputField.fontAsset = inputTMP.font;
        inputField.pointSize = 20;
        inputField.characterLimit = 24;
        inputField.customCaretColor = true;
        inputField.caretColor = GOLD;
        inputField.caretWidth = 3;
        inputField.selectionColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.3f);

        return inputField;
    }

    private TMP_Dropdown CreateDropdown(GameObject parent, List<string> options)
    {
        // Container
        GameObject container = CreateChild(parent, "Dropdown");
        container.AddComponent<RectTransform>();

        LayoutElement le = container.AddComponent<LayoutElement>();
        le.preferredHeight = 48;
        le.flexibleWidth = 1;

        Image bg = container.AddComponent<Image>();
        bg.color = FIELD_BG;
        bg.raycastTarget = true;

        Outline ol = container.AddComponent<Outline>();
        ol.effectColor = BORDER;
        ol.effectDistance = new Vector2(1.5f, 1.5f);

        // Caption text
        GameObject captionGO = CreateChild(container, "Label");
        RectTransform captionRT = captionGO.AddComponent<RectTransform>();
        captionRT.anchorMin = Vector2.zero;
        captionRT.anchorMax = Vector2.one;
        captionRT.offsetMin = new Vector2(12f, 4f);
        captionRT.offsetMax = new Vector2(-36f, -4f);

        TextMeshProUGUI captionTMP = captionGO.AddComponent<TextMeshProUGUI>();
        captionTMP.fontSize = 20;
        captionTMP.color = PARCHMENT;
        captionTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // Arrow indicator
        GameObject arrowGO = CreateChild(container, "Arrow");
        RectTransform arrowRT = arrowGO.AddComponent<RectTransform>();
        arrowRT.anchorMin = new Vector2(1f, 0f);
        arrowRT.anchorMax = new Vector2(1f, 1f);
        arrowRT.offsetMin = new Vector2(-32f, 8f);
        arrowRT.offsetMax = new Vector2(-8f, -8f);

        TextMeshProUGUI arrowTMP = arrowGO.AddComponent<TextMeshProUGUI>();
        arrowTMP.text = "\u25BC"; // ▼
        arrowTMP.fontSize = 16;
        arrowTMP.color = GOLD;
        arrowTMP.alignment = TextAlignmentOptions.Center;
        arrowTMP.raycastTarget = false;

        // Dropdown template
        GameObject template = CreateChild(container, "Template");
        template.SetActive(false);
        RectTransform templateRT = template.AddComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot = new Vector2(0.5f, 1f);
        templateRT.sizeDelta = new Vector2(0f, 200f);

        Image templateBG = template.AddComponent<Image>();
        templateBG.color = WOOD_DARK;

        Outline templateOL = template.AddComponent<Outline>();
        templateOL.effectColor = BORDER;
        templateOL.effectDistance = new Vector2(1.5f, 1.5f);

        ScrollRect scrollRect = template.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;

        // Viewport
        GameObject viewport = CreateChild(template, "Viewport");
        RectTransform vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = Color.white;
        scrollRect.viewport = vpRT;

        // Content
        GameObject scrollContent = CreateChild(viewport, "Content");
        RectTransform scrollContentRT = scrollContent.AddComponent<RectTransform>();
        scrollContentRT.anchorMin = new Vector2(0f, 1f);
        scrollContentRT.anchorMax = new Vector2(1f, 1f);
        scrollContentRT.pivot = new Vector2(0.5f, 1f);
        scrollContentRT.sizeDelta = new Vector2(0f, 40f);
        scrollRect.content = scrollContentRT;

        // Item template
        GameObject item = CreateChild(scrollContent, "Item");
        RectTransform itemRT = item.AddComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0f, 0.5f);
        itemRT.anchorMax = new Vector2(1f, 0.5f);
        itemRT.sizeDelta = new Vector2(0f, 40f);

        Toggle itemToggle = item.AddComponent<Toggle>();

        // Item background
        GameObject itemBG = CreateChild(item, "Item Background");
        RectTransform itemBGRT = itemBG.AddComponent<RectTransform>();
        itemBGRT.anchorMin = Vector2.zero;
        itemBGRT.anchorMax = Vector2.one;
        itemBGRT.offsetMin = Vector2.zero;
        itemBGRT.offsetMax = Vector2.zero;

        Image itemBGImg = itemBG.AddComponent<Image>();
        itemBGImg.color = WOOD_LIGHT;

        // Item checkmark (highlight)
        GameObject checkmark = CreateChild(itemBG, "Item Checkmark");
        RectTransform checkRT = checkmark.AddComponent<RectTransform>();
        checkRT.anchorMin = Vector2.zero;
        checkRT.anchorMax = Vector2.one;
        checkRT.offsetMin = Vector2.zero;
        checkRT.offsetMax = Vector2.zero;

        Image checkImg = checkmark.AddComponent<Image>();
        checkImg.color = new Color(GOLD.r, GOLD.g, GOLD.b, 0.3f);

        itemToggle.targetGraphic = itemBGImg;
        itemToggle.graphic = checkImg;

        // Item label
        GameObject itemLabel = CreateChild(item, "Item Label");
        RectTransform itemLabelRT = itemLabel.AddComponent<RectTransform>();
        itemLabelRT.anchorMin = Vector2.zero;
        itemLabelRT.anchorMax = Vector2.one;
        itemLabelRT.offsetMin = new Vector2(12f, 2f);
        itemLabelRT.offsetMax = new Vector2(-12f, -2f);

        TextMeshProUGUI itemLabelTMP = itemLabel.AddComponent<TextMeshProUGUI>();
        itemLabelTMP.fontSize = 18;
        itemLabelTMP.color = PARCHMENT;
        itemLabelTMP.alignment = TextAlignmentOptions.MidlineLeft;

        // TMP_Dropdown
        TMP_Dropdown dropdown = container.AddComponent<TMP_Dropdown>();
        dropdown.template = templateRT;
        dropdown.captionText = captionTMP;
        dropdown.itemText = itemLabelTMP;
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        dropdown.value = 0;

        // Colour transitions
        ColorBlock cb = dropdown.colors;
        cb.normalColor = FIELD_BG;
        cb.highlightedColor = WOOD_LIGHT;
        cb.pressedColor = WOOD_DARK;
        cb.selectedColor = FIELD_BG;
        dropdown.colors = cb;

        return dropdown;
    }

    private Button CreateButton(GameObject parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject container = CreateChild(parent, "Button");
        container.AddComponent<RectTransform>();

        LayoutElement le = container.AddComponent<LayoutElement>();
        le.preferredHeight = 54;
        le.flexibleWidth = 1;

        Image bg = container.AddComponent<Image>();
        bg.color = BTN_NORMAL;
        bg.raycastTarget = true;

        Outline ol = container.AddComponent<Outline>();
        ol.effectColor = GOLD;
        ol.effectDistance = new Vector2(2f, 2f);

        // Inner glow
        Outline ol2 = container.AddComponent<Outline>();
        ol2.effectColor = new Color(0.1f, 0.08f, 0.03f, 0.6f);
        ol2.effectDistance = new Vector2(4f, 4f);

        Button btn = container.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = BTN_NORMAL;
        cb.highlightedColor = BTN_HOVER;
        cb.pressedColor = BTN_PRESSED;
        cb.selectedColor = BTN_NORMAL;
        cb.fadeDuration = 0.12f;
        btn.colors = cb;
        btn.targetGraphic = bg;
        btn.onClick.AddListener(onClick);

        // Label
        GameObject textGO = CreateChild(container, "Text");
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 24;
        tmp.color = GOLD;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.SmallCaps | FontStyles.Bold;
        tmp.raycastTarget = false;

        return btn;
    }
}
