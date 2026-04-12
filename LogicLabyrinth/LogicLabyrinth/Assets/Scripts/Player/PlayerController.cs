using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    [Header("Camera Settings")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 80f;

    [Header("Interaction")]
    public float interactionRange = 3f;
    public LayerMask interactableLayer;

    private CharacterController controller;
    private Camera playerCamera;
    private float xRotation = 0f;
    private Vector3 velocity;
    private bool isGrounded;

    // Input values
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool jumpPressed;
    private Interactable currentInteractable;
    private bool cursorFreelookDisabled;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();

        if (controller == null)
        {
            Debug.LogError("No CharacterController found on Player!");
        }
        else
        {
            Debug.Log("CharacterController found and working");
        }

        // Start with cursor unlocked
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("PlayerController Started");
    }

    void Update()
    {
        // TAB toggles gameplay cursor lock on/off.
        // Useful for quickly interacting with on-screen overlays without opening pause.
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            cursorFreelookDisabled = !cursorFreelookDisabled;
        }

        // Don't do anything while a puzzle UI, swap UI, or pause menu is open
        if (PuzzleTableController.IsOpen || SwapGateUI.IsOpen || PauseMenuController.IsPaused) return;

        // Block ALL input during full-screen cutscenes (Cutscene1/2)
        if (CutsceneController.IsPlaying) return;

        // Auto-lock cursor in level scenes (but not during camera-only cutscene phase)
        string currentScene = SceneManager.GetActiveScene().name;
        bool isGameplayScene = currentScene.StartsWith("Level") || currentScene == "Chapter3" || currentScene == "Chapter4";
        if (isGameplayScene && !CutsceneController.CameraOnlyMode)
        {
            if (cursorFreelookDisabled)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Debug.Log("Cursor locked for gameplay");
            }
        }

        // Only handle movement if cursor is locked (in gameplay)
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // During camera-only cutscene (Cutscene3/4): allow look but block movement/interaction
            if (!CutsceneController.CameraOnlyMode)
                HandleMovement();

            HandleLook();

            if (!CutsceneController.CameraOnlyMode)
                HandleInteraction();
        }
    }

    // Input System methods
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
        Debug.Log($"Move Input Received: {moveInput}");
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
        Debug.Log($"Look Input Received: {lookInput}");
    }

    public void OnJump(InputValue value)
    {
        jumpPressed = value.isPressed;
        Debug.Log($"Jump Input Received: {jumpPressed}");
    }

    public void OnInteract(InputValue value)
    {
        if (value.isPressed && currentInteractable != null)
        {
            Debug.Log($"INTERACTING WITH: {currentInteractable.gameObject.name}");
            currentInteractable.Interact();
        }
    }

    void HandleMovement()
    {
        // Ground check
        isGrounded = controller.isGrounded;

        // Reset vertical velocity when grounded
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        // Calculate movement direction
        Vector3 moveDirection = transform.right * moveInput.x + transform.forward * moveInput.y;

        // Apply movement speed
        float currentSpeed = walkSpeed;
        if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
        {
            currentSpeed = runSpeed;
        }

        // Move the character
        controller.Move(moveDirection * currentSpeed * Time.deltaTime);

        // Handle jumping
        if (jumpPressed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            Debug.Log("Jumping! Velocity: " + velocity.y);
            jumpPressed = false;
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    void HandleLook()
    {
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) return;
        }

        // Apply mouse look
        float mouseX = lookInput.x * mouseSensitivity * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -maxLookAngle, maxLookAngle);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void HandleInteraction()
    {
        if (playerCamera == null) return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionRange, interactableLayer))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            if (interactable != null)
            {
                currentInteractable = interactable;

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowInteractPrompt(true, interactable.GetInteractionText());
                }
                return;
            }
        }

        // No interactable found
        currentInteractable = null;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowInteractPrompt(false);
        }
    }

}