using System.Collections;
using StarterAssets;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Runtime bootstrap that lets players consume a store-bought adrenaline charge from the hotbar.
/// Select the ADR slot, press F, then a short drink animation plays and movement speed is boosted.
///
/// Pose workflow:
/// - Move/rotate AdrenalineEquipped in Play mode.
/// - Pose is auto-captured while equipped and auto-saved when Play mode stops.
/// - Optional manual save key (P) still exists.
/// </summary>
public class AdrenalineConsumableController : MonoBehaviour
{
    private const string PrefHandPosX = "LL_ADR_HAND_POS_X";
    private const string PrefHandPosY = "LL_ADR_HAND_POS_Y";
    private const string PrefHandPosZ = "LL_ADR_HAND_POS_Z";
    private const string PrefHandRotX = "LL_ADR_HAND_ROT_X";
    private const string PrefHandRotY = "LL_ADR_HAND_ROT_Y";
    private const string PrefHandRotZ = "LL_ADR_HAND_ROT_Z";
    private const string PrefHandScaleX = "LL_ADR_HAND_SCALE_X";
    private const string PrefHandScaleY = "LL_ADR_HAND_SCALE_Y";
    private const string PrefHandScaleZ = "LL_ADR_HAND_SCALE_Z";

    private const string PrefCamPosX = "LL_ADR_CAM_POS_X";
    private const string PrefCamPosY = "LL_ADR_CAM_POS_Y";
    private const string PrefCamPosZ = "LL_ADR_CAM_POS_Z";
    private const string PrefCamRotX = "LL_ADR_CAM_ROT_X";
    private const string PrefCamRotY = "LL_ADR_CAM_ROT_Y";
    private const string PrefCamRotZ = "LL_ADR_CAM_ROT_Z";
    private static readonly Vector3 DefaultHandScale = new Vector3(0.2f, 0.2f, 0.2f);

    private const string ObjMeshPath = "Assets/Store/Purchase/Adrenaline/source/model/base.obj";
    private static readonly string[] DiffuseTexPaths =
    {
        "Assets/Store/Purchase/Adrenaline/textures/texture_diffuse.png",
        "Assets/Store/Purchase/Adrenaline/source/model/texture_diffuse.png"
    };
    private static readonly string[] NormalTexPaths =
    {
        "Assets/Store/Purchase/Adrenaline/textures/texture_normal.png",
        "Assets/Store/Purchase/Adrenaline/source/model/texture_normal.png"
    };
    private static readonly string[] MetallicTexPaths =
    {
        "Assets/Store/Purchase/Adrenaline/textures/texture_metallic.png",
        "Assets/Store/Purchase/Adrenaline/source/model/texture_metallic.png"
    };

    [Header("Input")]
    public KeyCode consumeKey = KeyCode.F;
    public KeyCode savePoseKey = KeyCode.P;

    [Header("Boost")]
    public float boostDuration = 12f;
    public float speedMultiplier = 1.5f;

    [Header("View Model")]
    public string resourcesPrefabPath = "Adrenaline/AdrenalinePrefab";
    public Vector3 handLocalPosition = new Vector3(0f, 0.06f, 0.04f);
    public Vector3 handLocalEuler = new Vector3(-90f, 0f, 0f);
    public Vector3 handLocalScale = new Vector3(0.2f, 0.2f, 0.2f);
    public Vector3 cameraFallbackLocalPosition = new Vector3(0.22f, -0.30f, 0.45f);
    public Vector3 cameraFallbackLocalEuler = new Vector3(8f, -18f, 10f);

    [Header("Pose Save")]
    public bool autoSavePoseOnDisable = true;

    private Coroutine boostCoroutine;
    private bool boostActive;

    private FirstPersonController fpsController;
    private float baseFpsMove;
    private float baseFpsSprint;

    private PlayerController legacyController;
    private float baseLegacyWalk;
    private float baseLegacyRun;

    private static AdrenalineConsumableController instance;
    private GameObject equippedModel;
    private bool isDrinkingAnimation;
    private bool attachedToCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("AdrenalineConsumableController");
        instance = go.AddComponent<AdrenalineConsumableController>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        LoadSavedPose();
    }

    private void Update()
    {
        if (!IsGameplayLevelScene() || IsTypingIntoInputField())
            return;

        if (PauseMenuController.IsPaused || PuzzleTableController.IsOpen || CutsceneController.IsPlaying)
            return;

        bool adrSelected = IsAdrenalineSelected();
        if (adrSelected && !isDrinkingAnimation)
            EnsureEquippedModel();
        else if (!adrSelected && !isDrinkingAnimation)
            RemoveEquippedModel();

        if (equippedModel != null && !isDrinkingAnimation)
            CaptureCurrentPoseFromModel();

        if (WasSavePosePressed())
            SavePoseToPrefs(true);

        if (!WasConsumePressed())
            return;

        if (!adrSelected)
        {
            ShowCenterHint("Select ADR slot first.");
            return;
        }

        TryConsumeAdrenaline();
    }

    private void OnDisable()
    {
        if (autoSavePoseOnDisable)
            SavePoseToPrefs(false);

        if (boostActive)
            RestoreBaseSpeed();

        RemoveEquippedModel();
    }

    private void TryConsumeAdrenaline()
    {
        if (AccountManager.Instance == null)
        {
            ShowCenterHint("No account session for adrenaline.");
            return;
        }

        if (!AccountManager.Instance.ConsumeAdrenaline(1))
        {
            ShowCenterHint("No adrenaline left.");
            if (GameInventoryUI.Instance != null)
                GameInventoryUI.Instance.RefreshFromInventory();
            return;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayDrinkSound();

        if (!boostActive)
            CacheAndApplyBoost();

        if (boostCoroutine != null)
            StopCoroutine(boostCoroutine);
        boostCoroutine = StartCoroutine(BoostTimer());

        if (GameInventoryUI.Instance != null)
            GameInventoryUI.Instance.RefreshFromInventory();

        StartCoroutine(PlayDrinkViewModel());

        int remaining = AccountManager.Instance.GetAdrenalineCount();
        ShowCenterHint($"Adrenaline active ({remaining} left)");
    }

    private void CacheAndApplyBoost()
    {
        fpsController = FindFirstObjectByType<FirstPersonController>();
        legacyController = FindFirstObjectByType<PlayerController>();

        if (fpsController != null)
        {
            baseFpsMove = fpsController.MoveSpeed;
            baseFpsSprint = fpsController.SprintSpeed;
            fpsController.MoveSpeed = baseFpsMove * speedMultiplier;
            fpsController.SprintSpeed = baseFpsSprint * speedMultiplier;
        }

        if (legacyController != null)
        {
            baseLegacyWalk = legacyController.walkSpeed;
            baseLegacyRun = legacyController.runSpeed;
            legacyController.walkSpeed = baseLegacyWalk * speedMultiplier;
            legacyController.runSpeed = baseLegacyRun * speedMultiplier;
        }

        boostActive = true;
    }

    private IEnumerator BoostTimer()
    {
        yield return new WaitForSeconds(boostDuration);
        RestoreBaseSpeed();
        ShowCenterHint("Adrenaline faded");
    }

    private void RestoreBaseSpeed()
    {
        if (fpsController != null)
        {
            fpsController.MoveSpeed = baseFpsMove;
            fpsController.SprintSpeed = baseFpsSprint;
        }

        if (legacyController != null)
        {
            legacyController.walkSpeed = baseLegacyWalk;
            legacyController.runSpeed = baseLegacyRun;
        }

        boostActive = false;
        boostCoroutine = null;
    }

    private IEnumerator PlayDrinkViewModel()
    {
        Camera cam = GetActiveCamera();
        if (cam == null) yield break;

        isDrinkingAnimation = true;
        EnsureEquippedModel();

        if (equippedModel == null)
        {
            isDrinkingAnimation = false;
            yield break;
        }

        Transform model = equippedModel.transform;
        Vector3 startPos = model.localPosition;
        Quaternion startRot = model.localRotation;

        Vector3 sipPos;
        Quaternion sipRot;
        if (attachedToCamera)
        {
            sipPos = startPos + new Vector3(-0.07f, 0.14f, -0.19f);
            sipRot = startRot * Quaternion.Euler(-55f, 8f, -65f);
        }
        else
        {
            sipPos = startPos + new Vector3(0.01f, 0.02f, 0.03f);
            sipRot = startRot * Quaternion.Euler(-35f, -10f, -45f);
        }

        const float inDuration = 0.28f;
        const float holdDuration = 0.22f;
        const float outDuration = 0.24f;

        float t = 0f;
        while (t < inDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / inDuration);
            model.localPosition = Vector3.Lerp(startPos, sipPos, p);
            model.localRotation = Quaternion.Slerp(startRot, sipRot, p);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        t = 0f;
        while (t < outDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / outDuration);
            model.localPosition = Vector3.Lerp(sipPos, startPos, p);
            model.localRotation = Quaternion.Slerp(sipRot, startRot, p);
            yield return null;
        }

        isDrinkingAnimation = false;

        if (equippedModel != null)
            CaptureCurrentPoseFromModel();

        if (AccountManager.Instance == null || AccountManager.Instance.GetAdrenalineCount() <= 0 || !IsAdrenalineSelected())
            RemoveEquippedModel();
    }

    private bool IsAdrenalineSelected()
    {
        return GameInventoryUI.Instance != null &&
               GameInventoryUI.Instance.GetSelectedItem() == GameInventoryUI.ItemType.Adrenaline &&
               AccountManager.Instance != null &&
               AccountManager.Instance.GetAdrenalineCount() > 0;
    }

    private bool WasConsumePressed()
    {
        bool pressed = Input.GetKeyDown(consumeKey);
#if ENABLE_INPUT_SYSTEM
        if (!pressed && Keyboard.current != null)
            pressed = Keyboard.current.fKey.wasPressedThisFrame;
#endif
        return pressed;
    }

    private static bool IsGameplayLevelScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level");
    }

    private static bool IsTypingIntoInputField()
    {
        if (EventSystem.current == null) return false;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;

        if (selected.GetComponent<TMP_InputField>() != null) return true;
        if (selected.GetComponentInParent<TMP_InputField>() != null) return true;

        return false;
    }

    private bool WasSavePosePressed()
    {
        bool pressed = Input.GetKeyDown(savePoseKey);
#if ENABLE_INPUT_SYSTEM
        if (!pressed && Keyboard.current != null)
            pressed = Keyboard.current.pKey.wasPressedThisFrame;
#endif
        return pressed;
    }

    private void EnsureEquippedModel()
    {
        if (equippedModel != null)
            return;

        Transform anchor = FindRightHandAnchor();
        if (anchor == null)
            return;

        equippedModel = BuildAdrenalineModel();
        equippedModel.name = "AdrenalineEquipped";
        equippedModel.transform.SetParent(anchor, false);

        if (attachedToCamera)
        {
            equippedModel.transform.localPosition = cameraFallbackLocalPosition;
            equippedModel.transform.localRotation = Quaternion.Euler(cameraFallbackLocalEuler);
        }
        else
        {
            equippedModel.transform.localPosition = handLocalPosition;
            equippedModel.transform.localRotation = Quaternion.Euler(handLocalEuler);
        }

        equippedModel.transform.localScale = handLocalScale;
    }

    private void RemoveEquippedModel()
    {
        if (equippedModel != null)
        {
            Destroy(equippedModel);
            equippedModel = null;
        }
    }

    private void CaptureCurrentPoseFromModel()
    {
        if (equippedModel == null) return;

        Transform t = equippedModel.transform;
        Vector3 p = t.localPosition;
        Vector3 r = t.localRotation.eulerAngles;
        Vector3 s = t.localScale;

        handLocalScale = s;

        if (attachedToCamera)
        {
            cameraFallbackLocalPosition = p;
            cameraFallbackLocalEuler = r;
        }
        else
        {
            handLocalPosition = p;
            handLocalEuler = r;
        }
    }

    private void SavePoseToPrefs(bool showHint)
    {
        CaptureCurrentPoseFromModel();

        SaveVector3(PrefHandPosX, PrefHandPosY, PrefHandPosZ, handLocalPosition);
        SaveVector3(PrefHandRotX, PrefHandRotY, PrefHandRotZ, handLocalEuler);
        SaveVector3(PrefHandScaleX, PrefHandScaleY, PrefHandScaleZ, handLocalScale);
        SaveVector3(PrefCamPosX, PrefCamPosY, PrefCamPosZ, cameraFallbackLocalPosition);
        SaveVector3(PrefCamRotX, PrefCamRotY, PrefCamRotZ, cameraFallbackLocalEuler);
        PlayerPrefs.Save();

        if (showHint)
            ShowCenterHint(attachedToCamera ? "Saved ADR camera pose." : "Saved ADR hand pose.");
    }

    private void LoadSavedPose()
    {
        if (HasVector3(PrefHandPosX, PrefHandPosY, PrefHandPosZ))
            handLocalPosition = LoadVector3(PrefHandPosX, PrefHandPosY, PrefHandPosZ);

        if (HasVector3(PrefHandRotX, PrefHandRotY, PrefHandRotZ))
            handLocalEuler = LoadVector3(PrefHandRotX, PrefHandRotY, PrefHandRotZ);

        if (HasVector3(PrefHandScaleX, PrefHandScaleY, PrefHandScaleZ))
            handLocalScale = LoadVector3(PrefHandScaleX, PrefHandScaleY, PrefHandScaleZ);

        // Guard against stale/invalid prefs that make ADR appear huge or microscopic on some machines.
        if (!IsReasonableScale(handLocalScale))
            handLocalScale = DefaultHandScale;

        if (HasVector3(PrefCamPosX, PrefCamPosY, PrefCamPosZ))
            cameraFallbackLocalPosition = LoadVector3(PrefCamPosX, PrefCamPosY, PrefCamPosZ);

        if (HasVector3(PrefCamRotX, PrefCamRotY, PrefCamRotZ))
            cameraFallbackLocalEuler = LoadVector3(PrefCamRotX, PrefCamRotY, PrefCamRotZ);
    }

    private static bool IsReasonableScale(Vector3 scale)
    {
        if (!float.IsFinite(scale.x) || !float.IsFinite(scale.y) || !float.IsFinite(scale.z))
            return false;

        // ADR handheld model should stay within a practical visual range.
        return scale.x >= 0.03f && scale.y >= 0.03f && scale.z >= 0.03f
            && scale.x <= 0.8f && scale.y <= 0.8f && scale.z <= 0.8f;
    }

    private static bool HasVector3(string x, string y, string z)
    {
        return PlayerPrefs.HasKey(x) && PlayerPrefs.HasKey(y) && PlayerPrefs.HasKey(z);
    }

    private static Vector3 LoadVector3(string x, string y, string z)
    {
        return new Vector3(PlayerPrefs.GetFloat(x), PlayerPrefs.GetFloat(y), PlayerPrefs.GetFloat(z));
    }

    private static void SaveVector3(string x, string y, string z, Vector3 value)
    {
        PlayerPrefs.SetFloat(x, value.x);
        PlayerPrefs.SetFloat(y, value.y);
        PlayerPrefs.SetFloat(z, value.z);
    }

    private Camera GetActiveCamera()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled)
            return cam;

        Camera[] all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].isActiveAndEnabled)
                return all[i];
        }

        return null;
    }

    private Transform FindRightHandAnchor()
    {
        attachedToCamera = false;

        string[] handBoneNames = { "mixamorig:RightHand", "RightHand", "Hand_R" };

        if (FirstPersonArmAnimator.Instance != null)
        {
            for (int i = 0; i < handBoneNames.Length; i++)
            {
                Transform hand = FindDeepChild(FirstPersonArmAnimator.Instance.transform, handBoneNames[i]);
                if (hand != null)
                    return hand;
            }
        }

        GameObject player = GameObject.Find("FirstPersonPlayer");
        if (player != null)
        {
            for (int i = 0; i < handBoneNames.Length; i++)
            {
                Transform hand = FindDeepChild(player.transform, handBoneNames[i]);
                if (hand != null)
                    return hand;
            }
        }

        Camera cam = GetActiveCamera();
        if (cam != null)
        {
            Transform anchor = cam.transform.Find("AdrenalineHandAnchor");
            if (anchor == null)
            {
                GameObject anchorGO = new GameObject("AdrenalineHandAnchor");
                anchorGO.transform.SetParent(cam.transform, false);
                anchor = anchorGO.transform;
            }

            attachedToCamera = true;
            return anchor;
        }

        return null;
    }

    private Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null) return null;

        Transform direct = parent.Find(childName);
        if (direct != null) return direct;

        foreach (Transform child in parent)
        {
            Transform nested = FindDeepChild(child, childName);
            if (nested != null) return nested;
        }

        return null;
    }

    private GameObject BuildAdrenalineModel()
    {
        GameObject prefab = Resources.Load<GameObject>(resourcesPrefabPath);
        if (prefab != null)
            return Instantiate(prefab);

#if UNITY_EDITOR
        GameObject fromObj = TryBuildFromObjAsset();
        if (fromObj != null)
            return fromObj;
#endif

        // Last-resort fallback so the animation remains visible even without assets wired.
        GameObject root = new GameObject("AdrenalineBottleRuntime");

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = Vector3.zero;
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.04f, 0.12f, 0.04f);

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.transform.SetParent(root.transform, false);
        cap.transform.localPosition = new Vector3(0f, 0.14f, 0f);
        cap.transform.localRotation = Quaternion.identity;
        cap.transform.localScale = new Vector3(0.028f, 0.025f, 0.028f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.9f, 0.18f, 0.2f, 1f);

        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null) bodyRenderer.material = mat;

        Renderer capRenderer = cap.GetComponent<Renderer>();
        if (capRenderer != null) capRenderer.material = mat;

        Collider bodyCol = body.GetComponent<Collider>();
        if (bodyCol != null) Destroy(bodyCol);

        Collider capCol = cap.GetComponent<Collider>();
        if (capCol != null) Destroy(capCol);

        return root;
    }

#if UNITY_EDITOR
    private GameObject TryBuildFromObjAsset()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ObjMeshPath);
        if (mesh == null)
            return null;

        GameObject go = new GameObject("AdrenalineObjRuntime");
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);

        Texture2D diffuse = LoadTextureFromAny(DiffuseTexPaths);
        if (diffuse != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", diffuse);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", diffuse);
        }

        Texture2D normal = LoadTextureFromAny(NormalTexPaths);
        if (normal != null)
        {
            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
        }

        Texture2D metallic = LoadTextureFromAny(MetallicTexPaths);
        if (metallic != null)
        {
            if (mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);
        }
        else if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", 0.05f);
        }

        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);

        mr.sharedMaterial = mat;
        go.transform.localScale = Vector3.one * 0.28f;
        return go;
    }

    private static Texture2D LoadTextureFromAny(string[] paths)
    {
        if (paths == null) return null;
        for (int i = 0; i < paths.Length; i++)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            if (tex != null) return tex;
        }
        return null;
    }
#endif

    private void ShowCenterHint(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        StartCoroutine(ShowCenterHintCoroutine(message));
    }

    private IEnumerator ShowCenterHintCoroutine(string message)
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.Log($"[Adrenaline] {message}");
            yield break;
        }

        GameObject go = new GameObject("AdrenalineHint");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -120f);
        rt.sizeDelta = new Vector2(520f, 80f);

        TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
        txt.text = message;
        txt.alignment = TextAlignmentOptions.Center;
        txt.fontSize = 28f;
        txt.color = new Color(1f, 0.92f, 0.75f, 1f);
        txt.fontStyle = FontStyles.Bold;

        CanvasGroup cg = go.AddComponent<CanvasGroup>();
        float life = 1.2f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float p = elapsed / life;
            cg.alpha = p < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (p - 0.75f) / 0.25f);
            yield return null;
        }

        Destroy(go);
    }
}
