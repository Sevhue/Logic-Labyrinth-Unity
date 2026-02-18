using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Attach to the key GameObject in the scene.
/// Handles bob/spin animation and the collection logic.
/// Prompt and E-key detection are handled by SimpleGateCollector.
/// </summary>
public class CollectibleKey : MonoBehaviour
{
    [Header("Animation")]
    public float bobSpeed = 1.5f;
    public float bobHeight = 0.15f;

    /// <summary>True after the player picks up the key.</summary>
    public bool IsCollected { get; private set; } = false;

    // Legacy static flag — no longer needed but kept for compat
    public static bool IsShowingPrompt => false;

    private Vector3 startLocalPos;

    void Start()
    {
        startLocalPos = transform.localPosition;

        // Ensure a collider exists so SphereCast can hit this object.
        // Must be a TRIGGER so it doesn't push the player's CharacterController.
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider>();
        bc.size = new Vector3(0.15f, 0.15f, 0.15f);
        bc.center = new Vector3(0f, 0.005f, 0f);
        bc.isTrigger = true;

        StartCoroutine(BobAndSpin());
    }

    /// <summary>
    /// Called by SimpleGateCollector when the player presses E while looking at the key.
    /// </summary>
    public void CollectKey()
    {
        if (IsCollected) return;
        IsCollected = true;

        Debug.Log("[CollectibleKey] Key collected!");
        TutorialDoor.PlayerHasKey = true;

        // Notify hotbar UI
        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.OnKeyCollected();

        if (FirstPersonArmAnimator.Instance != null)
            FirstPersonArmAnimator.Instance.PlayCollectAnimation();

        StartCoroutine(PickupAnimation());
    }

    private IEnumerator PickupAnimation()
    {
        // Show pickup message
        GameObject msgUI = CreatePickupMessage();

        // Shrink and float upward
        Vector3 startScale = transform.localScale;
        Vector3 startPos = transform.position;
        float duration = 0.6f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.position = startPos + Vector3.up * t * 1.5f;
            transform.localScale = startScale * (1f - t);
            yield return null;
        }

        // Fade out message
        yield return new WaitForSeconds(1.5f);
        if (msgUI != null)
        {
            CanvasGroup cg = msgUI.AddComponent<CanvasGroup>();
            float fadeTime = 0.5f;
            float fadeElapsed = 0f;
            while (fadeElapsed < fadeTime)
            {
                fadeElapsed += Time.deltaTime;
                cg.alpha = 1f - (fadeElapsed / fadeTime);
                yield return null;
            }
            Destroy(msgUI);
        }

        Destroy(gameObject);
    }

    private GameObject CreatePickupMessage()
    {
        GameObject msgUI = new GameObject("KeyPickupMessage");
        Canvas canvas = msgUI.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        CanvasScaler scaler = msgUI.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panelGO = new GameObject("Panel");
        panelGO.transform.SetParent(msgUI.transform, false);

        RectTransform panelRT = panelGO.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.3f, 0.55f);
        panelRT.anchorMax = new Vector2(0.7f, 0.65f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image bg = panelGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.02f, 0.85f);
        bg.raycastTarget = false;

        Outline outline = panelGO.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.4f, 0.9f);
        outline.effectDistance = new Vector2(2f, 2f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(10f, 5f);
        textRT.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Key collected!";
        tmp.fontSize = 26;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.9f, 0.5f, 1f);
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        return msgUI;
    }

    private IEnumerator BobAndSpin()
    {
        float timer = 0f;
        while (!IsCollected)
        {
            timer += Time.deltaTime;

            float yOffset = Mathf.Sin(timer * bobSpeed) * bobHeight;
            transform.localPosition = startLocalPos + Vector3.up * yOffset;
            transform.Rotate(Vector3.up, 30f * Time.deltaTime, Space.Self);

            yield return null;
        }
    }
}
