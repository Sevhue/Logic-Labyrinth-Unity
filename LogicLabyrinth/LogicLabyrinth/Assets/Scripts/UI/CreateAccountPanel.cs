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
        // Replace entire onClick to clear any Inspector-wired persistent listeners (e.g. ExecuteFinalSignUp)
        createButton.onClick = new Button.ButtonClickedEvent();
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

        string username = usernameField != null ? usernameField.text.Trim() : "";
        string password = passwordField != null ? passwordField.text : "";
        string confirmPassword = confirmPasswordField != null ? confirmPasswordField.text : "";

        // 1. CLEAR PREVIOUS MESSAGES
        ShowMessage("", false);
        ShowPasswordMessage("", false);
        CloseValidationPopup();

        // 2. INPUT VALIDATIONS — use shared SignUpValidator
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
        {
            ShowValidationPopup("Please fill in all fields!");
            return;
        }

        // Username validation (length, allowed chars, profanity)
        string usernameError = SignUpValidator.ValidateUsername(username);
        if (!string.IsNullOrEmpty(usernameError))
        {
            ShowValidationPopup(usernameError);
            return;
        }

        // Password validation (8-20 chars, letters+numbers, no specials)
        string passwordError = SignUpValidator.ValidatePassword(password);
        if (!string.IsNullOrEmpty(passwordError))
        {
            ShowValidationPopup(passwordError);
            return;
        }

        if (password != confirmPassword)
        {
            ShowValidationPopup("Passwords don't match!");
            return;
        }

        if (securityQuestionDropdown != null && securityQuestionDropdown.value == 0)
        {
            ShowValidationPopup("Please select a security question!");
            return;
        }

        if (securityAnswerField != null && string.IsNullOrEmpty(securityAnswerField.text.Trim()))
        {
            ShowValidationPopup("Please fill in your security answer!");
            return;
        }

        if (termsToggle != null && !termsToggle.isOn)
        {
            ShowValidationPopup("You must agree to the Terms and Conditions!");
            return;
        }

        Debug.Log("ALL VALIDATIONS PASSED - Calling AccountManager");

        // Collect all fields
        string secQuestion = (securityQuestionDropdown != null && securityQuestionDropdown.value > 0)
            ? securityQuestionDropdown.options[securityQuestionDropdown.value].text
            : "";
        string secAnswer = securityAnswerField != null ? securityAnswerField.text.Trim() : "";
        string gender = (genderDropdown != null && genderDropdown.value > 0)
            ? genderDropdown.options[genderDropdown.value].text
            : "";
        string age = (ageDropdown != null && ageDropdown.value > 0)
            ? ageDropdown.options[ageDropdown.value].text
            : "";

        Debug.Log($"[CreateAccountPanel] Sending: user={username}, secQ={secQuestion}, secA={secAnswer}, gender={gender}, age={age}");

        // Disable button to prevent double-clicks
        if (createButton != null) createButton.interactable = false;

        // 3. FIREBASE CALL — creates Auth user + saves all data
        AccountManager.Instance.CreateAccountWithSecurity(
            username,
            password,
            secQuestion,
            secAnswer,
            gender,
            age,
            (success, message) => {

                // Re-enable button
                if (createButton != null) createButton.interactable = true;

                if (success)
                {
                    Debug.Log("SUCCESS: Account created!");

                    // Check if profile is still missing info (e.g. displayName)
                    if (NewPlayerSetupUI.IsProfileIncomplete())
                    {
                        Debug.Log("[CreateAccountPanel] Profile incomplete — showing setup UI.");
                        NewPlayerSetupUI.Show(() =>
                        {
                            if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                        });
                    }
                    else
                    {
                        // Go straight to LoggedIn panel
                        if (UIManager.Instance != null) UIManager.Instance.ShowMainMenu();
                    }
                }
                else
                {
                    Debug.Log("FAILED: " + message);
                    ShowValidationPopup(message);
                }
            }
        );
    }


    // Validation is now handled by the shared SignUpValidator class.
    // See Assets/Scripts/Utils/SignUpValidator.cs

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