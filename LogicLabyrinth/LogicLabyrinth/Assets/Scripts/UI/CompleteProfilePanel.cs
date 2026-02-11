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
        createAccountButton.onClick.AddListener(OnCreateAccountClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);

        // Auto-fill with Google info if available
        AutoFillGoogleInfo();
    }

    private void AutoFillGoogleInfo()
    {
        var currentPlayer = AccountManager.Instance.GetCurrentPlayer();
        if (currentPlayer != null && !string.IsNullOrEmpty(currentPlayer.displayName))
        {
            // You can auto-fill username or show welcome message
            Debug.Log($"Completing profile for Google user: {currentPlayer.displayName}");
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
        // Optional: Delete the temporary Google account if canceled
        AccountManager.Instance.Logout();
        UIManager.Instance.ShowMainMenu();
    }

    private void SaveProfileData()
    {
        var player = AccountManager.Instance.GetCurrentPlayer();
        if (player != null)
        {
            // Save the profile data from the form
            player.username = usernameField.text;

            if (ageDropdown != null)
                // You might want to store age as string or int

                if (genderDropdown != null)
                    // Store gender selection

                    AccountManager.Instance.SavePlayerProgress();
        }
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