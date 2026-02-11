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
        
        loginButton.onClick.AddListener(OnLoginClicked);
        backButton.onClick.AddListener(OnBackClicked);
        forgotPasswordButton.onClick.AddListener(OnForgotPasswordClicked); 

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }


    public void OnLoginClicked()
    {
        string username = usernameField.text;
        string password = passwordField.text;

        ShowMessage("", false);

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowMessage("Please enter both username and password!", true);
            return;
        }

       
        AccountManager.Instance.Login(username, password, (success) => {
            if (success)
            {
                ShowMessage($"Welcome back, {username}!", false);
                Invoke("GoToMainMenu", 1.5f);
            }
            else
            {
                ShowMessage("Login failed! Check username/password or create an account.", true);
            }
        });
    }

    public void OnBackClicked()
    {
        UIManager.Instance.ShowMainMenu();
        ClearFields();
    }

    
    public void OnForgotPasswordClicked()
    {
        UIManager.Instance.ShowForgotPasswordPanel();
        ClearFields();
    }

    void GoToMainMenu()
    {
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