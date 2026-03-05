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
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = isError ? Color.red : Color.green;
            messageText.gameObject.SetActive(true);
        }
        Debug.Log(message);
    }

    void ClearFields()
    {
        usernameField.text = "";
        passwordField.text = "";
        ShowMessage("", false);
    }
}