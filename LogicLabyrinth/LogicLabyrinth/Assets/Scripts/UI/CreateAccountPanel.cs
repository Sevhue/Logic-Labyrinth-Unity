using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class CreateAccountPanel : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public TMP_InputField confirmPasswordField;
    public TMP_Dropdown securityQuestionDropdown;
    public TMP_InputField securityAnswerField;
    public TMP_Dropdown ageDropdown;
    public TMP_Dropdown genderDropdown;
    public Toggle termsToggle;
    public Button createButton;
    public Button backButton;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI passwordText;

    [Header("Validation Popup")]
    public GameObject validationPopup; 
    public TextMeshProUGUI validationMessageText;

    void Start()
    {
        createButton.onClick.AddListener(OnCreateClicked);
        backButton.onClick.AddListener(OnBackClicked);

        
        InitializeSecurityQuestions();
    }

    private void InitializeSecurityQuestions()
    {
        if (securityQuestionDropdown != null)
        {
            securityQuestionDropdown.ClearOptions();

            List<string> questions = new List<string>
            {
                "Select Security Question",
                "What was your first pet's name?",
                "What elementary school did you attend?",
                "What city were you born in?",
                "What is your mother's maiden name?",
                "What was your childhood nickname?",
                "What is your favorite movie?",
                "What is the name of your first teacher?"
            };

            securityQuestionDropdown.AddOptions(questions);
        }
    }

    
    private void ShowValidationPopup(string message)
    {
        if (validationPopup != null && validationMessageText != null)
        {
            validationMessageText.text = message;
            validationPopup.SetActive(true);
            Debug.Log($"VALIDATION POPUP: {message}");
        }
        else
        {
            Debug.LogError("ValidationPopup references not set!");
            
            ShowMessage(message, true);
        }
    }

    
    public void CloseValidationPopup()
    {
        if (validationPopup != null)
        {
            validationPopup.SetActive(false);
        }
    }

    void OnCreateClicked()
    {
        Debug.Log("=== CREATE ACCOUNT CLICKED ===");

        string username = usernameField.text.Trim();
        string password = passwordField.text;
        string confirmPassword = confirmPasswordField.text;

        // 1. CLEAR PREVIOUS MESSAGES
        ShowMessage("", false);
        ShowPasswordMessage("", false);
        CloseValidationPopup();

        // 2. INPUT VALIDATIONS (The logic you were worried about)
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            ShowValidationPopup("Please fill in all fields");
            return;
        }

        string usernameError = GetUsernameError(username);
        if (!string.IsNullOrEmpty(usernameError))
        {
            ShowValidationPopup(usernameError);
            return;
        }

        if (password != confirmPassword)
        {
            ShowValidationPopup("Passwords don't match");
            return;
        }

        if (securityQuestionDropdown != null && securityQuestionDropdown.value == 0)
        {
            ShowValidationPopup("Please select a security question");
            return;
        }

        if (termsToggle != null && !termsToggle.isOn)
        {
            ShowValidationPopup("You must agree to the terms and conditions");
            return;
        }

        Debug.Log("ALL VALIDATIONS PASSED - Calling AccountManager");

        // 3. FIREBASE CALL (Wrapped in Action callback to fix CS0029 & CS7036)
        AccountManager.Instance.CreateAccountWithSecurity(
            username,
            password,
            securityQuestionDropdown.options[securityQuestionDropdown.value].text,
            securityAnswerField.text.Trim(),
            (success) => { // Dito papasok ang resulta galing Firebase

                if (success)
                {
                    Debug.Log("SUCCESS: Account created!");
                    ShowMessage("Account created successfully!", false);

                    // AUTO-LOGIN CALL
                    AccountManager.Instance.Login(username, password, (loginSuccess) => {
                        if (loginSuccess)
                        {
                            Debug.Log("SUCCESS: Auto-login worked");
                            Invoke("GoToMainMenu", 2f);
                        }
                        else
                        {
                            Debug.Log("WARNING: Auto-login failed");
                            Invoke("GoToLogin", 2f);
                        }
                    });
                }
                else
                {
                    Debug.Log("FAILED: Account creation failed");
                    ShowValidationPopup("Username already exists or Database error");
                }
            }
        );
    }


    private string GetUsernameError(string username)
    {
        if (username.Length < 3)
            return "Username must be at least 3 characters";

        if (username.Length > 20)
            return "Username must be less than 20 characters";

        
        foreach (char c in username)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
            {
                return "Username can only contain letters, numbers, _ , - , .";
            }
        }

        return "";
    }

    
    private string GetPasswordError(string password)
    {
        if (password.Length < 4)
            return "Password must be at least 4 characters";

        return "";
    }

    void OnBackClicked()
    {
        UIManager.Instance.ShowMainMenu();
        ClearFields();
    }

    void GoToMainMenu()
    {
        UIManager.Instance.ShowMainMenu();
        ClearFields();
    }

    void GoToLogin()
    {
        UIManager.Instance.ShowLoginPanel();
        ClearFields();
    }

    void ShowMessage(string message, bool isError)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = isError ? Color.red : Color.green;
            messageText.gameObject.SetActive(true);
            Debug.Log($"MESSAGE: {message}");
        }
    }

    void ShowPasswordMessage(string message, bool isError)
    {
        if (passwordText != null)
        {
            passwordText.text = message;
            passwordText.color = isError ? Color.red : Color.green;
            passwordText.gameObject.SetActive(true);
            Debug.Log($"PASSWORD MESSAGE: {message}");
        }
    }

    void ClearFields()
    {
        usernameField.text = "";
        passwordField.text = "";
        confirmPasswordField.text = "";
        securityAnswerField.text = "";

        if (securityQuestionDropdown != null) securityQuestionDropdown.value = 0;
        if (ageDropdown != null) ageDropdown.value = 0;
        if (genderDropdown != null) genderDropdown.value = 0;
        if (termsToggle != null) termsToggle.isOn = false;

        ShowMessage("", false);
        ShowPasswordMessage("", false);
        CloseValidationPopup(); 
    }

    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            OnCreateClicked();
        }
    }
}