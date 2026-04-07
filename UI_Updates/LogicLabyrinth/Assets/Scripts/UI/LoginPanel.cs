using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LoginPanel : MonoBehaviour
{
    [Header("Login References")]
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public Button loginButton;
    public Button backButton;
    public Button forgotPasswordButton; 
    public TextMeshProUGUI messageText;
    private GameObject runtimeErrorBanner;

    void Start()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(OnLoginClicked);
        else
            Debug.LogWarning("[LoginPanel] loginButton is not assigned.");

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (forgotPasswordButton != null)
            forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked); 

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }


    public void OnLoginClicked()
    {
        if (AccountManager.Instance == null)
        {
            ShowMessage("Login service unavailable. Please retry.", true);
            return;
        }

        if (loginButton != null && !loginButton.interactable)
            return;

        string username = usernameField.text;
        string password = passwordField.text;

        ShowMessage("", false);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Please enter both username and password!", true);
            return;
        }

        if (loginButton != null) loginButton.interactable = false;

        AccountManager.Instance.Login(username, password, (success, message) => {
            if (loginButton != null) loginButton.interactable = true;

            if (success)
            {
                ShowMessage(string.IsNullOrWhiteSpace(message) ? $"Welcome back, {username}!" : message, false);
                Invoke("GoToMainMenu", 1.5f);
            }
            else
            {
                ShowMessage(string.IsNullOrWhiteSpace(message)
                    ? "Login failed! Check username/password or create an account."
                    : message, true);
            }
        });
    }

    public void OnBackClicked()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainMenu();
        ClearFields();
    }

    
    public void OnForgotPasswordClicked()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowForgotPasswordPanel();
        ClearFields();
    }

    void GoToMainMenu()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainMenu();
        ClearFields();
    }

    void ShowMessage(string message, bool isError)
    {
        bool shownInPanel = false;

        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = isError ? Color.red : Color.green;
            messageText.gameObject.SetActive(true);
            shownInPanel = true;
        }

        // Failsafe: if login panel message is unavailable or invisible, show a global validation popup.
        if (isError && !string.IsNullOrWhiteSpace(message))
        {
            bool popupShown = ShowGlobalValidationPopup(message);
            if (!shownInPanel && !popupShown)
                ShowRuntimeErrorBanner(message);
        }

        Debug.Log(message);
    }

    private bool ShowGlobalValidationPopup(string message)
    {
        if (UIManager.Instance == null) return false;
        if (UIManager.Instance.validationPopup == null || UIManager.Instance.validationMessageText == null)
            return false;

        UIManager.Instance.validationMessageText.text = message;
        UIManager.Instance.validationMessageText.color = Color.red;
        UIManager.Instance.validationPopup.SetActive(true);
        return true;
    }

    private void ShowRuntimeErrorBanner(string message)
    {
        if (runtimeErrorBanner != null)
            Destroy(runtimeErrorBanner);

        runtimeErrorBanner = new GameObject("LoginErrorBanner");
        Canvas canvas = runtimeErrorBanner.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;

        CanvasScaler scaler = runtimeErrorBanner.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(runtimeErrorBanner.transform, false);
        RectTransform panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.2f, 0.78f);
        panelRT.anchorMax = new Vector2(0.8f, 0.90f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.20f, 0.03f, 0.03f, 0.95f);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panel.transform, false);
        RectTransform textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = new Vector2(14f, 10f);
        textRT.offsetMax = new Vector2(-14f, -10f);

        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = message;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 28;
        text.color = new Color(1f, 0.85f, 0.85f, 1f);
        text.enableWordWrapping = true;

        Destroy(runtimeErrorBanner, 4f);
    }

    void ClearFields()
    {
        usernameField.text = "";
        passwordField.text = "";
        ShowMessage("", false);
    }
}