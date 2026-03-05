using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CompleteProfilePanel : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField usernameField;
    public TMP_Dropdown ageDropdown;
    public TMP_Dropdown genderDropdown;
    public Button createAccountButton;
    public Button cancelButton;
    public TextMeshProUGUI messageText;

    void Start()
    {
        if (createAccountButton != null)
            createAccountButton.onClick.AddListener(OnCreateAccountClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        // Auto-fill with Google info if available
        AutoFillGoogleInfo();
    }

    private void AutoFillGoogleInfo()
    {
        var currentPlayer = AccountManager.Instance != null ? AccountManager.Instance.GetCurrentPlayer() : null;
        if (currentPlayer != null && !string.IsNullOrEmpty(currentPlayer.displayName))
        {
            // You can auto-fill username or show welcome message
            Debug.Log($"Completing profile for Google user: {currentPlayer.displayName}");

            if (usernameField != null && string.IsNullOrWhiteSpace(usernameField.text))
                usernameField.text = currentPlayer.displayName;
        }
    }

    void OnCreateAccountClicked()
    {
        string username = usernameField.text;

        // Basic validation
        if (string.IsNullOrEmpty(username))
        {
            ShowMessage("Please enter a username!", true);
            return;
        }

        if (ageDropdown != null && !IsValidDropdownSelection(ageDropdown, "Select Age"))
        {
            ShowMessage("Please select your age range!", true);
            return;
        }

        if (genderDropdown != null && !IsValidDropdownSelection(genderDropdown, "Select Gender"))
        {
            ShowMessage("Please select your gender!", true);
            return;
        }

        // Profile is complete - save and go to main menu
        SaveProfileData();
        ShowMessage("Profile completed successfully!", false);

        // Go to main menu after delay
        Invoke("GoToMainMenu", 2f);
    }

    void OnCancelClicked()
    {
        // Non-destructive cancel: keep current session and return to menu.
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainMenu();
    }

    private void SaveProfileData()
    {
        if (AccountManager.Instance == null)
        {
            ShowMessage("Profile service unavailable.", true);
            return;
        }

        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player == null)
        {
            ShowMessage("No active profile found.", true);
            return;
        }

        // Save the profile data from the form
        if (usernameField != null)
            player.username = usernameField.text.Trim();

        if (ageDropdown != null && ageDropdown.value > 0)
            player.age = ageDropdown.options[ageDropdown.value].text;

        if (genderDropdown != null && genderDropdown.value > 0)
            player.gender = genderDropdown.options[genderDropdown.value].text;

        AccountManager.Instance.SavePlayerProgress(success =>
        {
            if (!success)
                ShowMessage("Profile saved locally. Cloud sync will retry later.", false);
        });
    }

    private bool IsValidDropdownSelection(TMP_Dropdown dropdown, string defaultText)
    {
        if (dropdown == null || dropdown.options.Count == 0)
            return true;

        if (dropdown.value == 0)
            return false;

        string selectedText = dropdown.options[dropdown.value].text;
        return selectedText != defaultText;
    }

    void GoToMainMenu()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowMainMenu();
    }

    void ShowMessage(string message, bool isError)
    {
        if (messageText != null)
        {
            messageText.text = message;
            messageText.color = isError ? Color.red : Color.green;
            messageText.gameObject.SetActive(true);
        }
    }
}