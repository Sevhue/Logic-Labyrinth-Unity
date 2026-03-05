using UnityEngine;
using UnityEngine.UI;

public class ForgotPasswordRuntimePanel : MonoBehaviour
{
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
    private Button closeButton;

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

        rootPanel = CreatePanel(canvas.transform, "Panel", new Vector2(640f, 460f), new Color(0f, 0f, 0f, 0.9f));

        titleText = CreateText(rootPanel.transform, "Title", "RESET PASSWORD", 34, FontStyle.Bold, TextAnchor.MiddleCenter,
            new Vector2(560f, 56f), new Vector2(0f, 170f), Color.white);

        questionText = CreateText(rootPanel.transform, "Question", "Enter username", 20, FontStyle.Bold, TextAnchor.MiddleLeft,
            new Vector2(560f, 40f), new Vector2(0f, 105f), new Color(1f, 0.95f, 0.7f));

        usernameField = CreateInput(rootPanel.transform, "UsernameInput", "Username", new Vector2(560f, 44f), new Vector2(0f, 55f), false);
        answerField = CreateInput(rootPanel.transform, "AnswerInput", "Security answer (exact)", new Vector2(560f, 44f), new Vector2(0f, 10f), false);
        passwordField = CreateInput(rootPanel.transform, "PasswordInput", "New password", new Vector2(560f, 44f), new Vector2(0f, -35f), true);
        confirmField = CreateInput(rootPanel.transform, "ConfirmInput", "Confirm password", new Vector2(560f, 44f), new Vector2(0f, -80f), true);

        messageText = CreateText(rootPanel.transform, "Message", "", 18, FontStyle.Normal, TextAnchor.MiddleCenter,
            new Vector2(560f, 48f), new Vector2(0f, -140f), Color.white);

        primaryButton = CreateButton(rootPanel.transform, "PrimaryButton", "Find Account", new Vector2(220f, 46f), new Vector2(120f, -200f));
        backButton = CreateButton(rootPanel.transform, "BackButton", "Back", new Vector2(140f, 46f), new Vector2(-90f, -200f));
        closeButton = CreateButton(rootPanel.transform, "CloseButton", "Close", new Vector2(120f, 36f), new Vector2(250f, 205f));

        primaryButton.onClick.AddListener(OnPrimaryClicked);
        backButton.onClick.AddListener(OnBackClicked);
        closeButton.onClick.AddListener(ClosePanel);
    }

    private void ResetFlow()
    {
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
    }

    private void SetPrimaryButtonText(string text)
    {
        var txt = primaryButton.GetComponentInChildren<Text>();
        if (txt != null) txt.text = text;
    }

    private void SetMessage(string msg, bool isError)
    {
        messageText.text = msg;
        messageText.color = isError ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 1f, 0.7f);
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
        go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.95f);

        var text = CreateText(go.transform, "Text", "", 20, FontStyle.Normal, TextAnchor.MiddleLeft,
            new Vector2(size.x - 20f, size.y - 8f), Vector2.zero, Color.black);
        text.rectTransform.anchorMin = new Vector2(0f, 0f);
        text.rectTransform.anchorMax = new Vector2(1f, 1f);
        text.rectTransform.offsetMin = new Vector2(10f, 4f);
        text.rectTransform.offsetMax = new Vector2(-10f, -4f);

        var holder = CreateText(go.transform, "Placeholder", placeholder, 18, FontStyle.Italic, TextAnchor.MiddleLeft,
            new Vector2(size.x - 20f, size.y - 8f), Vector2.zero, new Color(0.4f, 0.4f, 0.4f));
        holder.rectTransform.anchorMin = new Vector2(0f, 0f);
        holder.rectTransform.anchorMax = new Vector2(1f, 1f);
        holder.rectTransform.offsetMin = new Vector2(10f, 4f);
        holder.rectTransform.offsetMax = new Vector2(-10f, -4f);

        var input = go.GetComponent<InputField>();
        input.textComponent = text;
        input.placeholder = holder;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
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

        go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.9f, 1f);
        CreateText(go.transform, "Label", label, 18, FontStyle.Bold, TextAnchor.MiddleCenter, size, Vector2.zero, Color.white);

        return go.GetComponent<Button>();
    }
}
