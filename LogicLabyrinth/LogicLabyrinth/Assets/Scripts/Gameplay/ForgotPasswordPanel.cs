using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ForgotPasswordPanel : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField usernameField;
    public TextMeshProUGUI securityQuestionText;
    public TMP_InputField securityAnswerField;
    public TMP_InputField newPasswordField;
    public TMP_InputField confirmPasswordField;
    public Button submitButton;
    public Button backButton;
    public TextMeshProUGUI messageText;
    public TextMeshProUGUI passwordMessageText;

    [Header("Panel Sections")]
    public GameObject usernameSection;
    public GameObject securitySection;
    public GameObject passwordResetSection;

    private string currentUsername;

    void Start()
    {
        
        if (submitButton != null)
            submitButton.onClick.AddListener(OnSubmitClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        
        if (usernameField != null)
            usernameField.onValueChanged.AddListener(OnUsernameChanged);

        
        if (securityAnswerField != null)
            securityAnswerField.onValueChanged.AddListener(OnSecurityAnswerChanged);

       
        ShowUsernameSection();
    }

    void OnEnable()
    {
        
        ShowUsernameSection();
    }

    
    void OnUsernameChanged(string username)
    {
        if (AccountManager.Instance == null)
        {
            Debug.LogError("AccountManager is not available!");
            return;
        }

        if (string.IsNullOrEmpty(username))
        {
            if (securityQuestionText != null)
                securityQuestionText.text = "Enter username to see security question...";
            ShowMessage("", false);
            return;
        }

        
        if (securityQuestionText != null)
            securityQuestionText.text = "Checking username...";
        ShowMessage("Checking username...", false);

        
        CancelInvoke("CheckUsername");
        Invoke("CheckUsername", 0.8f);
    }

    void CheckUsername()
    {
        string username = usernameField.text.Trim();

        if (string.IsNullOrEmpty(username))
            return;

        string securityQuestion = AccountManager.Instance.GetSecurityQuestion(username);

        if (string.IsNullOrEmpty(securityQuestion))
        {
            if (securityQuestionText != null)
                securityQuestionText.text = "❌ Username not found";
            ShowMessage("Username not found! Please check and try again.", true);
            currentUsername = ""; 
        }
        else
        {
            if (securityQuestionText != null)
                securityQuestionText.text = securityQuestion;
            currentUsername = username;
            ShowMessage("✓ Username found! Please answer your security question.", false);

            
            if (securitySection != null && !securitySection.activeInHierarchy)
            {
                ShowSecuritySection();
            }
        }
    }

    
    void OnSecurityAnswerChanged(string answer)
    {
        if (!string.IsNullOrEmpty(answer) && answer.Length >= 2)
        {
           
            CancelInvoke("VerifyAndAdvance");
            Invoke("VerifyAndAdvance", 0.8f);
        }
    }

    void VerifyAndAdvance()
    {
        if (securitySection != null && securitySection.activeInHierarchy && !string.IsNullOrEmpty(securityAnswerField.text))
        {
            
            if (string.IsNullOrEmpty(currentUsername))
            {
                ShowMessage("Please enter a valid username first", true);
                return;
            }

            string securityAnswer = securityAnswerField.text.Trim();
            bool isAnswerCorrect = AccountManager.Instance.VerifySecurityAnswer(currentUsername, securityAnswer);

            if (isAnswerCorrect)
            {
                ShowMessage("✓ Security answer correct! Now set your new password.", false);
                ShowPasswordResetSection();
            }
            else
            {
                ShowMessage("❌ Security answer incorrect. Please try again.", true);
                
                if (securityAnswerField != null)
                {
                    securityAnswerField.text = "";
                    securityAnswerField.Select();
                    securityAnswerField.ActivateInputField();
                }
            }
        }
    }

    void OnSubmitClicked()
    {
        if (string.IsNullOrEmpty(currentUsername))
        {
            ShowMessage("Please enter a valid username first", true);
            ShowUsernameSection();
            return;
        }

        string securityAnswer = securityAnswerField.text.Trim();
        string newPassword = newPasswordField.text;
        string confirmPassword = confirmPasswordField.text;

        
        bool isAnswerCorrect = AccountManager.Instance.VerifySecurityAnswer(currentUsername, securityAnswer);
        if (!isAnswerCorrect)
        {
            ShowPasswordMessage("❌ Security answer incorrect. Please go back and verify.", true);
            ShowSecuritySection();
            return;
        }

        
        string passwordError = GetPasswordError(newPassword);
        if (!string.IsNullOrEmpty(passwordError))
        {
            ShowPasswordMessage(passwordError, true);
            return;
        }

        
        if (newPassword != confirmPassword)
        {
            ShowPasswordMessage("Passwords don't match", true);
            return;
        }

       
        bool success = AccountManager.Instance.ResetPassword(currentUsername, newPassword, securityAnswer);

        if (success)
        {
            ShowPasswordMessage("✓ Password reset successfully! You can now login with your new password", false);

           
            Invoke("ReturnToLogin", 2f);
        }
        else
        {
            ShowPasswordMessage("❌ Password reset failed. Please try again.", true);
        }
    }

    
    private string GetPasswordError(string password)
    {
        if (password.Length < 8)
            return "Password must be at least 8 characters!";

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]"))
            return "Password must contain at least 1 uppercase letter!";

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[a-z]"))
            return "Password must contain at least 1 lowercase letter!";

        if (!System.Text.RegularExpressions.Regex.IsMatch(password, @"[0-9]"))
            return "Password must contain at least 1 number!";

        return "";
    }

    void OnBackClicked()
    {
        if (passwordResetSection != null && passwordResetSection.activeInHierarchy)
        {
            ShowSecuritySection();
        }
        else if (securitySection != null && securitySection.activeInHierarchy)
        {
            ShowUsernameSection();
        }
        else
        {
            UIManager.Instance.ShowLoginPanel();
            ClearFields();
        }
    }

    void ShowUsernameSection()
    {
        if (usernameSection != null) usernameSection.SetActive(true);
        if (securitySection != null) securitySection.SetActive(false);
        if (passwordResetSection != null) passwordResetSection.SetActive(false);

        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = "";
        }
        if (passwordMessageText != null) passwordMessageText.gameObject.SetActive(false);

        
        if (usernameField != null)
        {
            usernameField.text = "";
            usernameField.Select();
            usernameField.ActivateInputField();
        }
        if (securityQuestionText != null)
            securityQuestionText.text = "Enter username to see security question...";
    }

    void ShowSecuritySection()
    {
        if (usernameSection != null) usernameSection.SetActive(false);
        if (securitySection != null) securitySection.SetActive(true);
        if (passwordResetSection != null) passwordResetSection.SetActive(false);

        if (messageText != null) messageText.gameObject.SetActive(true);
        if (passwordMessageText != null) passwordMessageText.gameObject.SetActive(false);

        
        if (securityAnswerField != null)
        {
            securityAnswerField.Select();
            securityAnswerField.ActivateInputField();
        }
    }

    void ShowPasswordResetSection()
    {
        if (usernameSection != null) usernameSection.SetActive(false);
        if (securitySection != null) securitySection.SetActive(false);
        if (passwordResetSection != null) passwordResetSection.SetActive(true);

        if (messageText != null) messageText.gameObject.SetActive(false); 
        if (passwordMessageText != null)
        {
            passwordMessageText.gameObject.SetActive(true);
            passwordMessageText.text = "";
        }

       
        if (newPasswordField != null)
        {
            newPasswordField.Select();
            newPasswordField.ActivateInputField();
        }
    }

    void ReturnToLogin()
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
            messageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
            Debug.Log("FORGOT PASSWORD: " + message);
        }
    }

    void ShowPasswordMessage(string message, bool isError)
    {
        if (passwordMessageText != null)
        {
            passwordMessageText.text = message;
            passwordMessageText.color = isError ? Color.red : Color.green;
            passwordMessageText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }
    }

    void ClearFields()
    {
        if (usernameField != null) usernameField.text = "";
        if (securityAnswerField != null) securityAnswerField.text = "";
        if (newPasswordField != null) newPasswordField.text = "";
        if (confirmPasswordField != null) confirmPasswordField.text = "";
        if (securityQuestionText != null) securityQuestionText.text = "Enter username to see security question...";
        currentUsername = "";

        ShowMessage("", false);
        ShowPasswordMessage("", false);
        ShowUsernameSection();
    }

    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (passwordResetSection != null && passwordResetSection.activeInHierarchy &&
                newPasswordField != null && !string.IsNullOrEmpty(newPasswordField.text) &&
                confirmPasswordField != null && !string.IsNullOrEmpty(confirmPasswordField.text))
            {
                OnSubmitClicked();
            }
        }
    }
}