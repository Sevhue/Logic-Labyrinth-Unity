using UnityEngine;

public class SimpleGateCollector : MonoBehaviour
{
    public Camera playerCamera;
    public float interactDistance = 3f;

    private Interactable currentInteractable;

    void Update()
    {
        HandleInteraction();

        if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
        {
            TryCollectGate();
        }
    }

    void HandleInteraction()
    {
        Ray ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;

        LevelUIManager levelUI = FindAnyObjectByType<LevelUIManager>();
        UIManager mainUI = FindAnyObjectByType<UIManager>();

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                currentInteractable = interactable;

                // Show prompt using LevelUIManager first, then fallback to UIManager
                if (levelUI != null)
                {
                    levelUI.ShowInteractPrompt(interactable.GetInteractionText());
                }
                else if (mainUI != null)
                {
                    mainUI.ShowInteractPrompt(true, interactable.GetInteractionText());
                }
                return; // Found an interactable, stop here
            }
        }

        // No interactable found - hide the prompt
        currentInteractable = null;

        if (levelUI != null)
        {
            levelUI.HideInteractPrompt();
        }
        else if (mainUI != null)
        {
            mainUI.ShowInteractPrompt(false);
        }
    }

    void TryCollectGate()
    {
        if (currentInteractable != null)
        {
            currentInteractable.Interact();
            currentInteractable = null; // Clear after interaction
        }
    }
}