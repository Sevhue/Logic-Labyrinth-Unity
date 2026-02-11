using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PasswordVisibilityToggle : MonoBehaviour
{
    [Header("References")]
    public TMP_InputField passwordField;
    public Button toggleButton;
    public TextMeshProUGUI eyeText; // Optional: if using text instead of image

    private bool isPasswordVisible = false;

    void Start()
    {
        // Auto-find references if not set
        if (passwordField == null)
            passwordField = GetComponentInParent<TMP_InputField>();

        if (toggleButton == null)
            toggleButton = GetComponent<Button>();

        // Add click listener
        toggleButton.onClick.AddListener(TogglePasswordVisibility);

        // Set initial state
        UpdatePasswordVisibility();
    }

    public void TogglePasswordVisibility()
    {
        isPasswordVisible = !isPasswordVisible;
        UpdatePasswordVisibility();
    }

    private void UpdatePasswordVisibility()
    {
        if (passwordField != null)
        {
            // Toggle between password and standard text
            passwordField.contentType = isPasswordVisible ?
                TMP_InputField.ContentType.Standard :
                TMP_InputField.ContentType.Password;

            // Refresh the input field to apply changes
            passwordField.ForceLabelUpdate();
        }

        // Update eye icon/text
        if (eyeText != null)
        {
            eyeText.text = isPasswordVisible ? "🙈" : "👁️";
        }
    }
}