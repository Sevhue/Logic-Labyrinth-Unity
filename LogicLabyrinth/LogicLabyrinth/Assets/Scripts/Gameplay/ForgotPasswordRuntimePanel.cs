using UnityEngine;
using UnityEngine.UI;

public class ForgotPasswordRuntimePanel : MonoBehaviour
{
    private static readonly Color PanelBgColor = new Color(0.10f, 0.07f, 0.03f, 0.92f);
    private static readonly Color PanelBorderColor = new Color(0.72f, 0.58f, 0.30f, 0.95f);
    private static readonly Color TitleColor = new Color(0.84f, 0.75f, 0.50f, 1f);
    private static readonly Color BodyTextColor = new Color(0.95f, 0.90f, 0.75f, 1f);
    private static readonly Color InputBgColor = new Color(0.18f, 0.12f, 0.05f, 0.96f);
    private static readonly Color InputTextColor = new Color(0.96f, 0.91f, 0.78f, 1f);
    private static readonly Color PlaceholderColor = new Color(0.74f, 0.65f, 0.45f, 0.8f);
    private static readonly Color ButtonBgColor = new Color(0.27f, 0.19f, 0.08f, 1f);
    private static readonly Color ButtonTextColor = new Color(0.90f, 0.82f, 0.58f, 1f);

    private enum Step
    {
        Username,
        Security,
        Reset,
        Done
    }

    private static ForgotPasswordRuntimePanel instance;

    private Canvas canvas;
    private GameObject rootPanel;
    private Text titleText;
    private Text questionText;
    private Text messageText;

    private InputField usernameField;
    private InputField answerField;
    private InputField passwordField;
    private InputField confirmField;

    private Button primaryButton;
    private Button backButton;

    private Step step = Step.Username;
    private string currentUsername = "";
    private string currentQuestion = "";
    private string typedAnswer = "";
    private bool waiting;

    private static Font GetUiFont()
    {
        // Newer Unity versions removed Arial.ttf as a built-in runtime font.
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null)
            f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    public static void Show()
    {
        if (instance == null)
        {
            var go = new GameObject("ForgotPasswordRuntimePanel");
            instance = go.AddComponent<ForgotPasswordRuntimePanel>();
            DontDestroyOnLoad(go);
            instance.BuildUI();
        }

        if (instance.canvas == null)
            instance.BuildUI();

        instance.gameObject.SetActive(true);
        instance.canvas.gameObject.SetActive(true);
        instance.ResetFlow();
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("ForgotPasswordCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        rootPanel = CreatePanel(canvas.transform, "Panel", new Vector2(700f, 500f), PanelBgColor);

        titleText = CreateText(rootPanel.transform, "Title", "RESET PASSWORD", 34, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(620f, 56f), new Vector2(0f, 190f), TitleColor);

        questionText = CreateText(rootPanel.transform, "Question", "Enter username", 20, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Vector2(620f, 40f), new Vector2(0f, 120f), BodyTextColor);

        usernameField = CreateInput(rootPanel.transform, "UsernameInput", "Username", new Vector2(620f, 48f), new Vector2(0f, 65f), false);
        answerField = CreateInput(rootPanel.transform, "AnswerInput", "Security answer (exact)", new Vector2(620f, 48f), new Vector2(0f, 15f), false);
        passwordField = CreateInput(rootPanel.transform, "PasswordInput", "New password", new Vector2(620f, 48f), new Vector2(0f, -35f), true);
        confirmField = CreateInput(rootPanel.transform, "ConfirmInput", "Confirm password", new Vector2(620f, 48f), new Vector2(0f, -85f), true);

        messageText = CreateText(rootPanel.transform, "Message", "", 18, FontStyle.Normal, TextAnchor.MiddleCenter,
            new Vector2(620f, 48f), new Vector2(0f, -155f), BodyTextColor);

        primaryButton = CreateButton(rootPanel.transform, "PrimaryButton", "Find Account", new Vector2(230f, 48f), new Vector2(135f, -220f));
        backButton = CreateButton(rootPanel.transform, "BackButton", "Back", new Vector2(165f, 48f), new Vector2(-100f, -220f));

        primaryButton.onClick.AddListener(OnPrimaryClicked);
        backButton.onClick.AddListener(OnBackClicked);

        ApplyResponsiveLayout();
    }

    private void ApplyResponsiveLayout()
    {
        if (rootPanel == null) return;

        RectTransform panelRT = rootPanel.GetComponent<RectTransform>();
        // Keep a smaller modal footprint so it matches the login card proportions.
        float panelWidth = Mathf.Clamp(Screen.width * 0.39f, 442f, 544f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.44f, 340f, 408f);
        panelRT.sizeDelta = new Vector2(panelWidth, panelHeight);

        float halfH = panelHeight * 0.5f;
        float halfW = panelWidth * 0.5f;
        float contentWidth = panelWidth - 56f;

        SetRect(titleText.rectTransform, new Vector2(contentWidth, 52f), new Vector2(0f, halfH - 38f));
        SetRect(questionText.rectTransform, new Vector2(contentWidth, 36f), new Vector2(0f, halfH - 92f));

        SetRect(usernameField.GetComponent<RectTransform>(), new Vector2(contentWidth, 44f), new Vector2(0f, halfH - 144f));
        SetRect(answerField.GetComponent<RectTransform>(), new Vector2(contentWidth, 44f), new Vector2(0f, halfH - 144f));
        SetRect(passwordField.GetComponent<RectTransform>(), new Vector2(contentWidth, 44f), new Vector2(0f, halfH - 196f));
        SetRect(confirmField.GetComponent<RectTransform>(), new Vector2(contentWidth, 44f), new Vector2(0f, halfH - 248f));

        SetRect(messageText.rectTransform, new Vector2(contentWidth, 46f), new Vector2(0f, -halfH + 72f));

        SetRect(backButton.GetComponent<RectTransform>(), new Vector2(128f, 36f), new Vector2(-76f, -halfH + 24f));
        SetRect(primaryButton.GetComponent<RectTransform>(), new Vector2(162f, 36f), new Vector2(86f, -halfH + 24f));
    }

    private static void SetRect(RectTransform rt, Vector2 size, Vector2 anchoredPos)
    {
        if (rt == null) return;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }

    private void ResetFlow()
    {
        ApplyResponsiveLayout();

        step = Step.Username;
        waiting = false;
        currentUsername = "";
        currentQuestion = "";
        typedAnswer = "";

        usernameField.text = "";
        answerField.text = "";
        passwordField.text = "";
        confirmField.text = "";

        SetMessage("", false);
        RefreshUI();
    }

    private void RefreshUI()
    {
        bool usernameStep = step == Step.Username;
        bool securityStep = step == Step.Security;
        bool resetStep = step == Step.Reset;
        bool doneStep = step == Step.Done;

        titleText.text = doneStep ? "PASSWORD UPDATED" : "RESET PASSWORD";

        if (usernameStep) questionText.text = "Enter your username";
        if (securityStep) questionText.text = string.IsNullOrWhiteSpace(currentQuestion)
            ? "Answer your security question"
            : currentQuestion;
        if (resetStep) questionText.text = "Set a new password";
        if (doneStep) questionText.text = "You can now login with your new password.";

        usernameField.gameObject.SetActive(usernameStep);
        answerField.gameObject.SetActive(securityStep);
        passwordField.gameObject.SetActive(resetStep);
        confirmField.gameObject.SetActive(resetStep);

        backButton.gameObject.SetActive(!doneStep);

        if (usernameStep) SetPrimaryButtonText("Find Account");
        if (securityStep) SetPrimaryButtonText("Verify Answer");
        if (resetStep) SetPrimaryButtonText("Reset Password");
        if (doneStep) SetPrimaryButtonText("Go To Login");

        primaryButton.interactable = !waiting;
    }

    private void OnPrimaryClicked()
    {
        if (AccountManager.Instance == null)
        {
            SetMessage("Account system is not ready.", true);
            return;
        }

        if (step == Step.Username)
        {
            string username = usernameField.text == null ? "" : usernameField.text.Trim();
            if (string.IsNullOrEmpty(username))
            {
                SetMessage("Enter a username.", true);
                return;
            }

            waiting = true;
            RefreshUI();
            SetMessage("Checking username...", false);

            AccountManager.Instance.GetSecurityQuestionForUserAsync(username, secQ =>
            {
                waiting = false;
                if (string.IsNullOrEmpty(secQ))
                {
                    string err = AccountManager.Instance.GetLastUsernameLookupError();
                    if (!string.IsNullOrEmpty(err) && err.ToLowerInvariant().Contains("permission denied"))
                        SetMessage("Firebase rules blocked username lookup.", true);
                    else
                        SetMessage("Username not found.", true);
                    RefreshUI();
                    return;
                }

                currentUsername = username;
                currentQuestion = secQ;
                step = Step.Security;
                SetMessage("Username found. Enter your exact security answer.", false);
                RefreshUI();
            });
            return;
        }

        if (step == Step.Security)
        {
            typedAnswer = answerField.text == null ? "" : answerField.text;
            if (string.IsNullOrEmpty(typedAnswer))
            {
                SetMessage("Enter your security answer.", true);
                return;
            }

            waiting = true;
            RefreshUI();

            AccountManager.Instance.VerifySecurityAnswerAsync(currentUsername, typedAnswer, ok =>
            {
                waiting = false;
                if (!ok)
                {
                    SetMessage("Incorrect security answer.", true);
                    RefreshUI();
                    return;
                }

                step = Step.Reset;
                SetMessage("Answer verified. Set your new password.", false);
                RefreshUI();
            });
            return;
        }

        if (step == Step.Reset)
        {
            string pass = passwordField.text ?? "";
            string confirm = confirmField.text ?? "";

            string validationError = ValidatePassword(pass, confirm);
            if (!string.IsNullOrEmpty(validationError))
            {
                SetMessage(validationError, true);
                return;
            }

            waiting = true;
            RefreshUI();
            SetMessage("Updating password...", false);

            AccountManager.Instance.ResetPasswordAsync(currentUsername, pass, typedAnswer, (success, message) =>
            {
                waiting = false;
                if (!success)
                {
                    SetMessage(message, true);
                    RefreshUI();
                    return;
                }

                step = Step.Done;
                SetMessage(message, false);
                RefreshUI();
            });
            return;
        }

        if (step == Step.Done)
        {
            ClosePanel();
            if (UIManager.Instance != null)
                UIManager.Instance.ShowLoginPanel();
        }
    }

    private void OnBackClicked()
    {
        if (step == Step.Reset)
            step = Step.Security;
        else if (step == Step.Security)
            step = Step.Username;
        else
            ClosePanel();

        SetMessage("", false);
        RefreshUI();
    }

    private void ClosePanel()
    {
        if (canvas != null) canvas.gameObject.SetActive(false);
        gameObject.SetActive(false);

        if (UIManager.Instance != null)
            UIManager.Instance.ShowLoginPanel();
    }

    private void SetPrimaryButtonText(string text)
    {
        var txt = primaryButton.GetComponentInChildren<Text>();
        if (txt != null) txt.text = text;
    }

    private void SetMessage(string msg, bool isError)
    {
        messageText.text = msg;
        messageText.color = isError ? new Color(1f, 0.55f, 0.50f, 1f) : new Color(0.77f, 0.95f, 0.72f, 1f);
    }

    private static string ValidatePassword(string pass, string confirm)
    {
        if (pass.Length < 8) return "Password must be at least 8 characters.";
        if (confirm != pass) return "Passwords do not match.";

        bool hasUpper = false, hasLower = false, hasDigit = false;
        for (int i = 0; i < pass.Length; i++)
        {
            char c = pass[i];
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
        }

        if (!hasUpper) return "Password needs at least 1 uppercase letter.";
        if (!hasLower) return "Password needs at least 1 lowercase letter.";
        if (!hasDigit) return "Password needs at least 1 number.";
        return "";
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = color;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = PanelBorderColor;
        outline.effectDistance = new Vector2(2f, 2f);
        return go;
    }

    private static Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle style,
        TextAnchor anchor, Vector2 size, Vector2 pos, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var text = go.GetComponent<Text>();
        text.text = value;
        text.font = GetUiFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        return text;
    }

    private static InputField CreateInput(Transform parent, string name, string placeholder, Vector2 size, Vector2 pos, bool password)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        Image bg = go.GetComponent<Image>();
        bg.color = InputBgColor;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = PanelBorderColor;
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        var text = CreateText(go.transform, "Text", "", 20, FontStyle.Normal, TextAnchor.MiddleLeft,
            new Vector2(size.x - 20f, size.y - 8f), Vector2.zero, InputTextColor);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = new Vector2(10f, 4f);
        text.rectTransform.offsetMax = new Vector2(-10f, -4f);

        var holder = CreateText(go.transform, "Placeholder", placeholder, 18, FontStyle.Italic, TextAnchor.MiddleLeft,
            new Vector2(size.x - 20f, size.y - 8f), Vector2.zero, PlaceholderColor);
        holder.rectTransform.anchorMin = new Vector2(0f, 0f);
        holder.rectTransform.anchorMax = new Vector2(1f, 1f);
        holder.rectTransform.offsetMin = new Vector2(10f, 4f);
        holder.rectTransform.offsetMax = new Vector2(-10f, -4f);

        var input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = holder;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
        input.selectionColor = new Color(0.72f, 0.58f, 0.30f, 0.45f);
        input.caretColor = InputTextColor;
        return input;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        Image bg = go.GetComponent<Image>();
        bg.color = ButtonBgColor;

        var outline = go.AddComponent<Outline>();
        outline.effectColor = PanelBorderColor;
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonBgColor;
        colors.highlightedColor = new Color(0.33f, 0.24f, 0.11f, 1f);
        colors.pressedColor = new Color(0.18f, 0.12f, 0.05f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.16f, 0.12f, 0.08f, 0.7f);
        button.colors = colors;

        CreateText(go.transform, "Label", label, 18, FontStyle.Bold, TextAnchor.MiddleCenter, size, Vector2.zero, ButtonTextColor);

        return button;
    }
}
