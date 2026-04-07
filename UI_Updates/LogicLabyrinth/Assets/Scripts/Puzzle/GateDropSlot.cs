using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop target for gate slots in the puzzle UI.
/// Attach to each "Box" in the UITable prefab.
/// Accepts dragged gates and shows them visually.
/// </summary>
public class GateDropSlot : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Settings")]
    public int slotIndex;                    // Which box this is (0-based)
    public GateType correctGateType;         // The answer for this slot

    [Header("Visual")]
    public Color emptyColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
    public Color filledColor = new Color(0.15f, 0.35f, 0.15f, 0.8f);
    public Color hoverColor = new Color(0.3f, 0.3f, 0.5f, 0.7f);
    public Color wrongColor = new Color(0.5f, 0.15f, 0.15f, 0.8f);

    // Runtime state
    private GateType? placedGate = null;     // null = empty
    private Image slotImage;
    private TextMeshProUGUI slotLabel;
    private Outline slotOutline;
    private PuzzleTableController controller;

    public bool IsEmpty => placedGate == null;
    public GateType? PlacedGate => placedGate;

    public void Initialize(int index, GateType answer, PuzzleTableController ctrl)
    {
        slotIndex = index;
        correctGateType = answer;
        controller = ctrl;

        slotImage = GetComponent<Image>();
        slotOutline = GetComponent<Outline>();

        // Create or find the label text inside the box
        slotLabel = GetComponentInChildren<TextMeshProUGUI>();
        if (slotLabel == null)
        {
            GameObject labelGO = new GameObject("GateLabel");
            labelGO.transform.SetParent(transform, false);

            RectTransform labelRect = labelGO.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            slotLabel = labelGO.AddComponent<TextMeshProUGUI>();
            slotLabel.alignment = TextAlignmentOptions.Center;
            slotLabel.fontSize = 18;
            slotLabel.fontStyle = FontStyles.Bold;
            slotLabel.color = Color.white;
        }

        UpdateVisual();
    }

    public void OnDrop(PointerEventData eventData)
    {
        GateDragItem dragItem = eventData.pointerDrag?.GetComponent<GateDragItem>();
        if (dragItem == null) return;

        // If slot already has a gate, return the old one to inventory first
        if (!IsEmpty)
        {
            ReturnGateToInventory();
        }

        // Place the new gate
        placedGate = dragItem.gateType;
        dragItem.OnDroppedInSlot(this);
        UpdateVisual();

        Debug.Log($"[GateDropSlot] Box{slotIndex + 1}: Placed {placedGate}");
    }

    /// <summary>
    /// Right-click or click to remove a placed gate.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsEmpty)
        {
            ReturnGateToInventory();
            UpdateVisual();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsEmpty && slotImage != null)
        {
            slotImage.color = hoverColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UpdateVisual(); // Restore correct color
    }

    public void ReturnGateToInventory()
    {
        if (placedGate == null) return;

        // Return gate to inventory
        if (controller != null)
        {
            controller.ReturnGateToInventory(placedGate.Value);
        }

        Debug.Log($"[GateDropSlot] Box{slotIndex + 1}: Removed {placedGate}");
        placedGate = null;
        UpdateVisual();
    }

    public bool IsCorrect()
    {
        return placedGate.HasValue && placedGate.Value == correctGateType;
    }

    public void ShowWrongFeedback()
    {
        if (slotImage != null)
            slotImage.color = wrongColor;
    }

    public void ShowCorrectFeedback()
    {
        if (slotImage != null)
            slotImage.color = new Color(0.1f, 0.5f, 0.2f, 0.9f); // Green
    }

    public void ClearSlot()
    {
        placedGate = null;
        UpdateVisual();
    }

    private void UpdateVisual()
    {
        if (slotImage != null)
        {
            slotImage.color = IsEmpty ? emptyColor : filledColor;
        }

        if (slotLabel != null)
        {
            if (IsEmpty)
            {
                slotLabel.text = "?";
                slotLabel.color = new Color(1f, 1f, 1f, 0.4f);
            }
            else
            {
                slotLabel.text = placedGate.Value.ToString();
                slotLabel.color = GetGateColor(placedGate.Value);
            }
        }

        // Highlight outline when filled
        if (slotOutline != null)
        {
            slotOutline.effectColor = IsEmpty
                ? new Color(0.5f, 0.5f, 0.5f, 0.5f)
                : GetGateColor(placedGate.Value);
        }
    }

    private Color GetGateColor(GateType type)
    {
        switch (type)
        {
            case GateType.AND: return new Color(0.3f, 0.7f, 1f, 1f);   // Blue
            case GateType.OR:  return new Color(1f, 0.7f, 0.2f, 1f);   // Orange
            case GateType.NOT: return new Color(1f, 0.3f, 0.4f, 1f);   // Red
            default:           return Color.white;
        }
    }
}
