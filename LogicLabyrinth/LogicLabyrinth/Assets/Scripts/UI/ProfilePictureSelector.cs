using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the ProfilePicture prefab panel.
/// Dynamically builds a high-quality grid of profile pictures from Resources/ProfilePictures/.
/// The two "Default" slots from the prefab:
///   - Default #1 (top-left, smaller) = kept as the default avatar indicator
///   - Default #2 (right side, larger) = preview of the currently selected picture
/// The old tiny "Image" slots from the prefab are hidden and replaced with a dynamic GridLayoutGroup.
/// </summary>
public class ProfilePictureSelector : MonoBehaviour
{
    // ── State ──
    private string pendingSelection = null;
    private string currentSaved = null;
    private List<ImageSlot> slots = new List<ImageSlot>();
    private Button confirmButton;
    private Button closeButton;
    private bool isInitialized = false;

    // ── Special slots ──
    private Image previewImage;         // Right side preview of selected picture

    // ── Dynamic UI ──
    private GameObject gridContainer;

    // ── Colors ──
    private static readonly Color normalBorder   = new Color(0.50f, 0.40f, 0.22f, 0.8f);
    private static readonly Color selectedBorder  = new Color(1f, 0.84f, 0f, 1f);
    private static readonly Color hoverTint       = new Color(0.95f, 0.92f, 0.82f, 1f);
    private static readonly Color panelBG         = new Color(0.22f, 0.18f, 0.10f, 0.95f);
    private static readonly Color darkBG          = new Color(0.15f, 0.12f, 0.07f, 0.9f);

    private struct ImageSlot
    {
        public GameObject go;
        public Image image;
        public string pictureName;
        public Outline outline;
    }

    /// <summary>
    /// Initialize the panel: load pictures into slots, wire buttons.
    /// </summary>
    public void Initialize(string currentProfilePicture)
    {
        currentSaved = currentProfilePicture;
        pendingSelection = currentProfilePicture;

        if (!isInitialized)
        {
            BuildUI();
            isInitialized = true;
        }
        else
        {
            RefreshHighlight();
            UpdatePreview();
        }
    }

    // ══════════════════════════════════════════════
    //  BUILD UI
    // ══════════════════════════════════════════════

    private void BuildUI()
    {
        // Load all textures
        Texture2D[] rawTextures = Resources.LoadAll<Texture2D>("ProfilePictures");
        if (rawTextures.Length == 0)
        {
            Debug.LogWarning("[ProfilePictureSelector] No textures found in Resources/ProfilePictures/!");
            return;
        }

        // Sort: "default" first, then alphabetical
        List<Texture2D> textures = rawTextures.OrderBy(t =>
        {
            string n = t.name.ToLowerInvariant();
            if (n == "default") return "000_default";
            return t.name;
        }).ToList();

        Transform boarder = transform.Find("Boarder");
        if (boarder == null)
        {
            Debug.LogError("[ProfilePictureSelector] 'Boarder' child not found!");
            return;
        }

        // ── Resize the Boarder to be bigger ──
        RectTransform boarderRT = boarder.GetComponent<RectTransform>();
        boarderRT.sizeDelta = new Vector2(860f, 520f);

        // ── Hide all existing children except Text (TMP), ConfirmButton ──
        List<Transform> defaultSlots = new List<Transform>();
        foreach (Transform child in boarder)
        {
            string cName = child.name;
            if (cName == "Default")
            {
                defaultSlots.Add(child);
                child.gameObject.SetActive(false); // hide old default slots
            }
            else if (cName == "Image")
            {
                child.gameObject.SetActive(false); // hide old tiny image slots
            }
        }

        // ── Create a dark background panel for the grid area (left side) ──
        GameObject gridPanel = CreateUIObject("GridPanel", boarder);
        RectTransform gridPanelRT = gridPanel.GetComponent<RectTransform>();
        gridPanelRT.anchorMin = new Vector2(0f, 0f);
        gridPanelRT.anchorMax = new Vector2(0.65f, 0.82f);
        gridPanelRT.offsetMin = new Vector2(15f, 15f);
        gridPanelRT.offsetMax = new Vector2(0f, 0f);

        Image gridPanelBG = gridPanel.AddComponent<Image>();
        gridPanelBG.color = darkBG;
        gridPanelBG.raycastTarget = true;

        // ── Add a ScrollRect + Viewport for scrolling if many images ──
        GameObject viewport = CreateUIObject("Viewport", gridPanel.transform);
        RectTransform viewportRT = viewport.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = new Vector2(5f, 5f);
        viewportRT.offsetMax = new Vector2(-5f, -5f);
        Image vpMask = viewport.AddComponent<Image>();
        vpMask.color = Color.white; // alpha must be 1 for Mask to work
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false; // hides the white image but mask still clips

        // ── Grid content ──
        GameObject gridContent = CreateUIObject("GridContent", viewport.transform);
        RectTransform gridContentRT = gridContent.GetComponent<RectTransform>();
        gridContentRT.anchorMin = new Vector2(0f, 1f);
        gridContentRT.anchorMax = new Vector2(1f, 1f);
        gridContentRT.pivot = new Vector2(0f, 1f);
        gridContentRT.anchoredPosition = Vector2.zero;

        GridLayoutGroup grid = gridContent.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(100f, 100f);
        grid.spacing = new Vector2(10f, 10f);
        grid.padding = new RectOffset(10, 10, 10, 10);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperLeft;

        ContentSizeFitter csf = gridContent.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Add ScrollRect
        ScrollRect scroll = gridPanel.AddComponent<ScrollRect>();
        scroll.content = gridContentRT;
        scroll.viewport = viewportRT;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 20f;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        gridContainer = gridContent;

        // ── Create image slots in the grid ──
        slots.Clear();
        foreach (Texture2D tex in textures)
        {
            CreateImageSlot(gridContent.transform, tex);
        }

        // ── Right side: Preview area ──
        GameObject previewPanel = CreateUIObject("PreviewPanel", boarder);
        RectTransform previewPanelRT = previewPanel.GetComponent<RectTransform>();
        previewPanelRT.anchorMin = new Vector2(0.67f, 0.25f);
        previewPanelRT.anchorMax = new Vector2(0.98f, 0.82f);
        previewPanelRT.offsetMin = Vector2.zero;
        previewPanelRT.offsetMax = Vector2.zero;

        Image previewPanelBG = previewPanel.AddComponent<Image>();
        previewPanelBG.color = darkBG;

        // Preview image
        GameObject previewImgGO = CreateUIObject("PreviewImage", previewPanel.transform);
        RectTransform previewImgRT = previewImgGO.GetComponent<RectTransform>();
        previewImgRT.anchorMin = new Vector2(0.05f, 0.05f);
        previewImgRT.anchorMax = new Vector2(0.95f, 0.95f);
        previewImgRT.offsetMin = Vector2.zero;
        previewImgRT.offsetMax = Vector2.zero;

        previewImage = previewImgGO.AddComponent<Image>();
        previewImage.preserveAspect = true;
        previewImage.color = Color.white;
        previewImage.raycastTarget = false;

        // ── Reposition the title text ──
        Transform titleText = boarder.Find("Text (TMP)");
        if (titleText != null)
        {
            RectTransform titleRT = titleText.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 0.85f);
            titleRT.anchorMax = new Vector2(0.65f, 1f);
            titleRT.offsetMin = new Vector2(15f, 0f);
            titleRT.offsetMax = new Vector2(0f, -5f);

            TextMeshProUGUI titleTMP = titleText.GetComponent<TextMeshProUGUI>();
            if (titleTMP != null)
            {
                titleTMP.text = "AVATAR";
                titleTMP.fontSize = 32;
                titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        // ── Reposition the Confirm/Save button ──
        Transform confirmTransform = boarder.Find("ConfirmButton");
        if (confirmTransform != null)
        {
            RectTransform confirmRT = confirmTransform.GetComponent<RectTransform>();
            confirmRT.anchorMin = new Vector2(0.67f, 0.05f);
            confirmRT.anchorMax = new Vector2(0.98f, 0.2f);
            confirmRT.offsetMin = Vector2.zero;
            confirmRT.offsetMax = Vector2.zero;

            confirmButton = confirmTransform.GetComponent<Button>();
            if (confirmButton == null)
                confirmButton = confirmTransform.gameObject.AddComponent<Button>();

            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);

            TextMeshProUGUI btnText = confirmTransform.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = "SAVE";
                btnText.fontSize = 24;
            }
        }

        // ── Close button (X) ──
        CreateCloseButton(boarder);

        RefreshHighlight();
        UpdatePreview();

        Debug.Log($"[ProfilePictureSelector] Built UI with {slots.Count} profile pictures.");
    }

    private void CreateImageSlot(Transform parent, Texture2D tex)
    {
        GameObject slotGO = CreateUIObject("Slot_" + tex.name, parent);

        // Background
        Image bgImg = slotGO.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.25f, 0.15f, 1f);

        // Child image for the actual picture
        GameObject picGO = CreateUIObject("Picture", slotGO.transform);
        RectTransform picRT = picGO.GetComponent<RectTransform>();
        picRT.anchorMin = new Vector2(0.05f, 0.05f);
        picRT.anchorMax = new Vector2(0.95f, 0.95f);
        picRT.offsetMin = Vector2.zero;
        picRT.offsetMax = Vector2.zero;

        Image picImg = picGO.AddComponent<Image>();
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        picImg.sprite = sprite;
        picImg.preserveAspect = true;
        picImg.color = Color.white;
        picImg.raycastTarget = false;

        // Outline for selection highlight
        Outline outline = slotGO.AddComponent<Outline>();
        outline.effectColor = normalBorder;
        outline.effectDistance = new Vector2(3f, 3f);

        // Button
        Button btn = slotGO.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        btn.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.3f, 0.25f, 0.15f, 1f);
        cb.highlightedColor = new Color(0.45f, 0.38f, 0.22f, 1f);
        cb.pressedColor = new Color(0.25f, 0.20f, 0.12f, 1f);
        cb.selectedColor = new Color(0.3f, 0.25f, 0.15f, 1f);
        btn.colors = cb;

        string capturedName = tex.name;
        btn.onClick.AddListener(() => OnSlotClicked(capturedName));

        slots.Add(new ImageSlot
        {
            go = slotGO,
            image = picImg,
            pictureName = tex.name,
            outline = outline
        });
    }

    private GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return go;
    }

    private void CreateCloseButton(Transform boarder)
    {
        Transform existing = boarder.Find("CloseButton");
        if (existing != null)
        {
            closeButton = existing.GetComponent<Button>();
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClicked);
            }
            return;
        }

        GameObject closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        closeBtnGO.transform.SetParent(boarder, false);
        closeBtnGO.layer = LayerMask.NameToLayer("UI");

        RectTransform closeRT = closeBtnGO.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-8f, -8f);
        closeRT.sizeDelta = new Vector2(44f, 44f);

        Image closeBG = closeBtnGO.GetComponent<Image>();
        closeBG.color = new Color(0.55f, 0.12f, 0.12f, 0.95f);

        closeButton = closeBtnGO.GetComponent<Button>();
        closeButton.targetGraphic = closeBG;
        ColorBlock ccb = closeButton.colors;
        ccb.normalColor = new Color(0.55f, 0.12f, 0.12f, 0.95f);
        ccb.highlightedColor = new Color(0.75f, 0.18f, 0.18f, 1f);
        ccb.pressedColor = new Color(0.4f, 0.08f, 0.08f, 1f);
        closeButton.colors = ccb;
        closeButton.onClick.AddListener(OnCloseClicked);

        // X label
        GameObject xTextGO = new GameObject("XText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        xTextGO.transform.SetParent(closeBtnGO.transform, false);
        xTextGO.layer = LayerMask.NameToLayer("UI");

        RectTransform xRT = xTextGO.GetComponent<RectTransform>();
        xRT.anchorMin = Vector2.zero;
        xRT.anchorMax = Vector2.one;
        xRT.offsetMin = Vector2.zero;
        xRT.offsetMax = Vector2.zero;

        TextMeshProUGUI xText = xTextGO.GetComponent<TextMeshProUGUI>();
        xText.text = "X";
        xText.fontSize = 26;
        xText.fontStyle = FontStyles.Bold;
        xText.color = Color.white;
        xText.alignment = TextAlignmentOptions.Center;
        xText.raycastTarget = false;
    }

    // ══════════════════════════════════════════════
    //  SELECTION
    // ══════════════════════════════════════════════

    private void OnSlotClicked(string pictureName)
    {
        pendingSelection = pictureName;
        Debug.Log($"[ProfilePictureSelector] Selected: {pictureName}");
        RefreshHighlight();
        UpdatePreview();
    }

    private void RefreshHighlight()
    {
        foreach (var slot in slots)
        {
            if (slot.outline != null)
            {
                bool isSelected = (slot.pictureName == pendingSelection);
                slot.outline.effectColor = isSelected ? selectedBorder : normalBorder;
                slot.outline.effectDistance = isSelected ? new Vector2(4f, 4f) : new Vector2(3f, 3f);
            }
        }
    }

    private void UpdatePreview()
    {
        if (previewImage == null) return;

        foreach (var slot in slots)
        {
            if (slot.pictureName == pendingSelection)
            {
                previewImage.sprite = slot.image.sprite;
                previewImage.preserveAspect = true;
                previewImage.color = Color.white;
                return;
            }
        }

        // Fallback
        if (ProfileManager.Instance != null)
        {
            Sprite sp = ProfileManager.Instance.GetSprite(pendingSelection);
            if (sp != null)
            {
                previewImage.sprite = sp;
                previewImage.preserveAspect = true;
                previewImage.color = Color.white;
            }
        }
    }

    // ══════════════════════════════════════════════
    //  CONFIRM / CLOSE
    // ══════════════════════════════════════════════

    private void OnConfirmClicked()
    {
        if (!string.IsNullOrEmpty(pendingSelection))
        {
            currentSaved = pendingSelection;

            if (ProfileManager.Instance != null)
            {
                ProfileManager.Instance.SelectProfilePicture(pendingSelection);
                ProfileManager.Instance.SavePendingChanges();
                Debug.Log($"[ProfilePictureSelector] Profile picture saved: {pendingSelection}");
            }
        }

        Close();
    }

    private void OnCloseClicked()
    {
        pendingSelection = currentSaved;

        if (ProfileManager.Instance != null)
            ProfileManager.Instance.RevertPendingChanges();

        Debug.Log("[ProfilePictureSelector] Closed without saving.");
        Close();
    }

    private void Close()
    {
        gameObject.SetActive(false);

        if (UIManager.Instance != null && UIManager.Instance.accountProfilePanel != null)
        {
            if (!UIManager.Instance.accountProfilePanel.activeSelf)
                UIManager.Instance.accountProfilePanel.SetActive(true);
        }
    }

    /// <summary>
    /// Returns the currently pending (preview) selection name.
    /// </summary>
    public string GetPendingSelection() => pendingSelection;
}
