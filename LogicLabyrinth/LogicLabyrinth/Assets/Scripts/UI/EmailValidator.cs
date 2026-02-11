using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EmailValidator : MonoBehaviour
{
    [Header("References")]
    public TMP_InputField emailField;
    public Image validationIcon;
    public TextMeshProUGUI errorText;

    [Header("Validation Icons")]
    public Sprite validIcon; // ✅ icon
    public Sprite invalidIcon; // ❌ icon

    private void Start()
    {
        if (emailField != null)
        {
            emailField.onValueChanged.AddListener(ValidateEmail);
            emailField.onEndEdit.AddListener(FinalEmailValidation);
        }
    }

    public void ValidateEmail(string email)
    {
        bool isValid = IsValidEmail(email);

        if (validationIcon != null)
        {
            validationIcon.sprite = isValid ? validIcon : invalidIcon;
        }

        if (errorText != null)
        {
            errorText.text = isValid ? "" : "Must be a valid @gmail.com address";
        }
    }

    private void FinalEmailValidation(string email)
    {
        bool isValid = IsValidEmail(email);

        if (!isValid && errorText != null)
        {
            errorText.text = "Please enter a valid @gmail.com address";
        }
    }

    public static bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        // Check for @gmail.com specifically
        bool hasGmail = email.ToLower().EndsWith("@gmail.com");

        // Basic email format validation
        bool hasAtSymbol = email.Contains("@");
        bool hasDotAfterAt = email.IndexOf('@') < email.LastIndexOf('.');
        bool hasValidLength = email.Length >= 10; // a@gmail.com = 10 chars

        return hasGmail && hasAtSymbol && hasDotAfterAt && hasValidLength;
    }

    public bool IsEmailValid()
    {
        return IsValidEmail(emailField.text);
    }
}