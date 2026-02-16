using UnityEngine;

/// <summary>
/// Attach this to the PlayerCapsule (same object as CharacterController).
/// It instantiates and sets up the character FBX model at runtime.
/// Hides the head/neck for first-person view, ensures hands are visible.
/// </summary>
public class CharacterModelSetup : MonoBehaviour
{
    [Header("Character Model")]
    [Tooltip("Drag the Character.fbx (or its prefab) here")]
    public GameObject characterPrefab;

    [Header("Scale Settings")]
    [Tooltip("Target height for the character in Unity units (meters)")]
    public float targetHeight = 1.85f;

    [Header("First Person Settings")]
    [Tooltip("Hide the head and neck bones for first-person camera")]
    public bool hideHeadForFirstPerson = true;

    [Header("Runtime References (auto-set)")]
    [SerializeField] private GameObject characterInstance;
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform neckBone;

    private MeshRenderer capsuleRenderer;

    void Start()
    {
        SetupCharacterModel();
    }

    void SetupCharacterModel()
    {
        if (characterPrefab == null)
        {
            Debug.LogWarning("CharacterModelSetup: No character prefab assigned!");
            return;
        }

        // Disable the default capsule mesh
        DisableCapsuleMesh();

        // Destroy any existing character instance
        if (characterInstance != null)
        {
            Destroy(characterInstance);
        }

        // Instantiate the character model
        characterInstance = Instantiate(characterPrefab, transform);
        characterInstance.name = "CharacterModel";

        // Reset local transform
        characterInstance.transform.localPosition = Vector3.zero;
        characterInstance.transform.localRotation = Quaternion.identity;
        characterInstance.transform.localScale = Vector3.one;

        // Calculate the correct scale based on the mesh bounds
        float modelHeight = CalculateModelHeight(characterInstance);
        if (modelHeight > 0.001f)
        {
            float scaleFactor = targetHeight / modelHeight;
            characterInstance.transform.localScale = Vector3.one * scaleFactor;
            Debug.Log($"CharacterModelSetup: Model height={modelHeight:F3}, scale={scaleFactor:F2}, target={targetHeight}");
        }
        else
        {
            Debug.LogWarning("CharacterModelSetup: Could not determine model height, using default scale");
            characterInstance.transform.localScale = Vector3.one;
        }

        // Position: feet at the bottom of CharacterController
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            float controllerBottom = cc.center.y - cc.height / 2f;
            characterInstance.transform.localPosition = new Vector3(0, controllerBottom, 0);
            Debug.Log($"CharacterModelSetup: Positioned feet at controller bottom y={controllerBottom:F2}");
        }

        // Fix armature offset (some FBX files have non-zero armature root position)
        FixArmatureOffset(characterInstance);

        // Set up SkinnedMeshRenderers
        foreach (var smr in characterInstance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.updateWhenOffscreen = true;
        }

        // Hide head/neck for first person
        if (hideHeadForFirstPerson)
        {
            HideHeadBones(characterInstance);
        }

        // Disable any cameras or lights that came with the FBX
        DisableExtraComponents(characterInstance);

        // Widen the player camera FOV for hand visibility
        Camera playerCam = GetComponentInChildren<Camera>();
        if (playerCam != null)
        {
            if (playerCam.fieldOfView < 60f)
            {
                playerCam.fieldOfView = 70f;
                Debug.Log("CharacterModelSetup: Increased camera FOV to 70 for hand visibility");
            }
            playerCam.nearClipPlane = 0.1f;
        }

        Debug.Log("CharacterModelSetup: Character model setup complete!");
    }

    void DisableCapsuleMesh()
    {
        // Find and disable the Capsule child's renderer
        Transform capsuleChild = transform.Find("Capsule");
        if (capsuleChild != null)
        {
            MeshRenderer mr = capsuleChild.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.enabled = false;
                Debug.Log("CharacterModelSetup: Disabled capsule mesh renderer");
            }
        }
    }

    float CalculateModelHeight(GameObject model)
    {
        // Method 1: Use SkinnedMeshRenderer bounds
        SkinnedMeshRenderer[] smRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        if (smRenderers.Length > 0)
        {
            // Force recalculate bounds
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            foreach (var smr in smRenderers)
            {
                smr.updateWhenOffscreen = true;

                // Use the shared mesh bounds as reference
                if (smr.sharedMesh != null)
                {
                    Bounds meshBounds = smr.sharedMesh.bounds;
                    float localMin = meshBounds.min.y;
                    float localMax = meshBounds.max.y;

                    if (localMin < minY) minY = localMin;
                    if (localMax > maxY) maxY = localMax;
                }
            }

            if (minY < maxY)
            {
                float height = maxY - minY;
                Debug.Log($"CharacterModelSetup: Mesh bounds height = {height:F3} (min={minY:F3}, max={maxY:F3})");
                return height;
            }
        }

        // Method 2: Use MeshRenderer bounds
        MeshRenderer[] mRenderers = model.GetComponentsInChildren<MeshRenderer>();
        if (mRenderers.Length > 0)
        {
            Bounds combinedBounds = mRenderers[0].bounds;
            for (int i = 1; i < mRenderers.Length; i++)
            {
                combinedBounds.Encapsulate(mRenderers[i].bounds);
            }
            return combinedBounds.size.y;
        }

        return 0f;
    }

    void FixArmatureOffset(GameObject model)
    {
        // Find the armature root and reset its local position to zero
        // Many FBX exports from Blender have a non-zero armature origin
        Transform armature = model.transform.Find("Hero_armature");
        if (armature == null)
        {
            // Try common naming conventions
            foreach (Transform child in model.transform)
            {
                if (child.name.ToLower().Contains("armature") || child.name.ToLower().Contains("skeleton"))
                {
                    armature = child;
                    break;
                }
            }
        }

        if (armature != null && armature.localPosition != Vector3.zero)
        {
            Debug.Log($"CharacterModelSetup: Resetting armature offset from {armature.localPosition} to zero");
            armature.localPosition = Vector3.zero;
        }
    }

    void HideHeadBones(GameObject model)
    {
        // Find head and neck bones in the Mixamo skeleton
        Transform[] allTransforms = model.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in allTransforms)
        {
            string lowerName = t.name.ToLower();

            // Hide head bone (scale to near-zero so skinned mesh vertices collapse)
            if (lowerName.Contains("head") && !lowerName.Contains("headtop") && !lowerName.Contains("_end"))
            {
                headBone = t;
                t.localScale = Vector3.one * 0.001f;
                Debug.Log($"CharacterModelSetup: Hidden head bone: {t.name}");
            }

            // Also hide the helmet if present
            if (lowerName.Contains("helmet"))
            {
                t.gameObject.SetActive(false);
                Debug.Log($"CharacterModelSetup: Disabled helmet: {t.name}");
            }
        }
    }

    void DisableExtraComponents(GameObject model)
    {
        // Disable any cameras that came with the FBX
        foreach (Camera cam in model.GetComponentsInChildren<Camera>(true))
        {
            cam.enabled = false;
            Debug.Log($"CharacterModelSetup: Disabled extra camera: {cam.name}");
        }

        // Disable any lights that came with the FBX
        foreach (Light light in model.GetComponentsInChildren<Light>(true))
        {
            light.enabled = false;
            Debug.Log($"CharacterModelSetup: Disabled extra light: {light.name}");
        }
    }

    // Call this to re-setup if needed
    public void RefreshModel()
    {
        SetupCharacterModel();
    }
}
