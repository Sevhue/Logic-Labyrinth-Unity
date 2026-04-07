using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class DraggableGate : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Gate Settings")]
    public GateType gateType;
    public TextMeshProUGUI gateText;

    private RectTransform rectTransform;
    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private Vector2 originalPosition;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        // Find canvas in parent
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>(); // FIXED: Updated deprecated method
        }
    }

    public void Initialize(GateType type)
    {
        gateType = type;

        // Set gate text and color based on type
        if (gateText != null)
        {
            switch (gateType)
            {
                case GateType.AND:
                    gateText.text = "AND";
                    break;
                case GateType.OR:
                    gateText.text = "OR";
                    break;
                case GateType.NOT:
                    gateText.text = "NOT";
                    break;
            }
        }

        // Store original position for reset
        originalPosition = rectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        // Bring to front
        transform.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform != null && canvas != null)
        {
            rectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }

        // Snap to grid or check connections here (future implementation)
        Debug.Log($"Dropped {gateType} gate at position: {rectTransform.anchoredPosition}");
    }

    // Reset gate to original position
    public void ResetPosition()
    {
        rectTransform.anchoredPosition = originalPosition;
    }

    // Get gate type for circuit evaluation
    public GateType GetGateType()
    {
        return gateType;
    }
}