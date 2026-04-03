using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Draggable gate item from the palette.
/// Player drags this onto a GateDropSlot.
/// </summary>
public class GateDragItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [HideInInspector] public GateType gateType;
    [HideInInspector] public PuzzleTableController controller;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Canvas rootCanvas;
    private Transform originalParent;
    private Vector2 originalPosition;
    private bool droppedInSlot;

    // The visual copy that follows the mouse
    private GameObject dragVisual;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null)
            rootCanvas = rootCanvas.rootCanvas;
    }

    public void Initialize(GateType type, PuzzleTableController ctrl)
    {
        gateType = type;
        controller = ctrl;

        // Set visual
        TextMeshProUGUI label = GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
        {
            label.text = type.ToString();
            label.color = GetGateColor(type);
        }

        Image bg = GetComponent<Image>();
        if (bg != null)
        {
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Check if this gate type is available in inventory
        if (controller != null && !controller.CanUseGate(gateType))
        {
            eventData.pointerDrag = null; // Cancel drag
            return;
        }

        droppedInSlot = false;
        originalParent = transform.parent;
        originalPosition = rectTransform.anchoredPosition;

        // Create a visual copy that follows the mouse
        dragVisual = CreateDragVisual();

        // Make this item semi-transparent
        canvasGroup.alpha = 0.5f;
        canvasGroup.blocksRaycasts = false;

        // Deduct from inventory immediately (will be returned if drop fails)
        if (controller != null)
            controller.UseGateFromInventory(gateType);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragVisual != null && rootCanvas != null)
        {
            RectTransform dragRect = dragVisual.GetComponent<RectTransform>();
            // Move the visual copy to follow the mouse
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.GetComponent<RectTransform>(),
                eventData.position,
                eventData.pressEventCamera,
                out localPoint);
            dragRect.anchoredPosition = localPoint;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Destroy the visual copy
        if (dragVisual != null)
            Destroy(dragVisual);

        // Restore this item
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // If not dropped in a valid slot, return gate to inventory
        if (!droppedInSlot)
        {
            if (controller != null)
                controller.ReturnGateToInventory(gateType);
        }

        // Update palette display
        if (controller != null)
            controller.RefreshPalette();
    }

    /// <summary>
    /// Called by GateDropSlot when this item is successfully dropped.
    /// </summary>
    public void OnDroppedInSlot(GateDropSlot slot)
    {
        droppedInSlot = true;
    }

    private GameObject CreateDragVisual()
    {
        if (rootCanvas == null) return null;

        GameObject visual = new GameObject("DragVisual");
        visual.transform.SetParent(rootCanvas.transform, false);
        visual.transform.SetAsLastSibling(); // On top of everything

        RectTransform vRect = visual.AddComponent<RectTransform>();
        vRect.sizeDelta = new Vector2(80, 40);

        Image vBg = visual.AddComponent<Image>();
        vBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Add outline
        Outline outline = visual.AddComponent<Outline>();
        outline.effectColor = GetGateColor(gateType);
        outline.effectDistance = new Vector2(2, -2);

        // Add label
        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(visual.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = gateType.ToString();
        label.fontSize = 20;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = GetGateColor(gateType);

        // Make sure it doesn't block raycasts (so the drop target can receive the event)
        CanvasGroup cg = visual.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        return visual;
    }

    private Color GetGateColor(GateType type)
    {
        switch (type)
        {
            case GateType.AND: return new Color(0.3f, 0.7f, 1f, 1f);
            case GateType.OR:  return new Color(1f, 0.7f, 0.2f, 1f);
            case GateType.NOT: return new Color(1f, 0.3f, 0.4f, 1f);
            default:           return Color.white;
        }
    }
}
