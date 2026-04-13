using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LanternHandController : MonoBehaviour
{
    private const string PrefHandPosX = "LL_LAN_HAND_POS_X";
    private const string PrefHandPosY = "LL_LAN_HAND_POS_Y";
    private const string PrefHandPosZ = "LL_LAN_HAND_POS_Z";
    private const string PrefHandRotX = "LL_LAN_HAND_ROT_X";
    private const string PrefHandRotY = "LL_LAN_HAND_ROT_Y";
    private const string PrefHandRotZ = "LL_LAN_HAND_ROT_Z";
    private const string PrefHandScaleX = "LL_LAN_HAND_SCALE_X";
    private const string PrefHandScaleY = "LL_LAN_HAND_SCALE_Y";
    private const string PrefHandScaleZ = "LL_LAN_HAND_SCALE_Z";
    private const string PrefCamPosX = "LL_LAN_CAM_POS_X";
    private const string PrefCamPosY = "LL_LAN_CAM_POS_Y";
    private const string PrefCamPosZ = "LL_LAN_CAM_POS_Z";
    private const string PrefCamRotX = "LL_LAN_CAM_ROT_X";
    private const string PrefCamRotY = "LL_LAN_CAM_ROT_Y";
    private const string PrefCamRotZ = "LL_LAN_CAM_ROT_Z";

    private const string LanternObjPath = "Assets/Store/Purchase/Lantern/source/model/base.obj";
    private static readonly string[] DiffuseTexPaths =
    {
        "Assets/Store/Purchase/Lantern/textures/texture_diffuse.png",
        "Assets/Store/Purchase/Lantern/source/model/texture_diffuse.png"
    };
    private static readonly string[] NormalTexPaths =
    {
        "Assets/Store/Purchase/Lantern/textures/texture_normal.png",
        "Assets/Store/Purchase/Lantern/source/model/texture_normal.png"
    };
    private static readonly string[] MetallicTexPaths =
    {
        "Assets/Store/Purchase/Lantern/textures/texture_metallic.png",
        "Assets/Store/Purchase/Lantern/source/model/texture_metallic.png"
    };

    private static LanternHandController instance;
    private GameObject equippedLanternModel;
    private Light equippedLanternLight;
    private bool lanternLightBoosted = false;
    private bool attachedToCamera;

    [Header("Pose")]
    public KeyCode savePoseKey = KeyCode.P;
    public bool autoSavePoseOnDisable = true;

    [Header("View Model")]
    public Vector3 handLocalPosition = new Vector3(-0.01f, 0.03f, 0.01f);
    public Vector3 handLocalEuler = new Vector3(-80f, 0f, 0f);
    public Vector3 handLocalScale = new Vector3(0.11f, 0.11f, 0.11f);
    public Vector3 cameraFallbackLocalPosition = new Vector3(0.22f, -0.28f, 0.40f);
    public Vector3 cameraFallbackLocalEuler = new Vector3(5f, -20f, 8f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("LanternHandController");
        instance = go.AddComponent<LanternHandController>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        LoadSavedPose();
        SaveLoadedPoseToPrefs();
    }

    private void Update()
    {
        bool shouldShow = ShouldShowLanternInHand();

        if (shouldShow)
            EnsureLanternModel();
        else
            RemoveLanternModel();

        if (equippedLanternModel != null)
            ApplyLoadedPoseToModel();
    }

    private void OnDisable()
    {
        RemoveLanternModel();
    }

    private bool ShouldShowLanternInHand()
    {
        if (!IsGameplayLevelScene()) return false;
        if (AccountManager.Instance == null) return false;
        if (!AccountManager.Instance.HasStoreItem("Lantern")) return false;

        // Match candle behavior: lantern is only shown while LAN slot is selected.
        if (GameInventoryUI.Instance == null || GameInventoryUI.Instance.GetSelectedItem() != GameInventoryUI.ItemType.Lantern)
            return false;

        if (CollectibleCandle.IsEquipped)
            return false;

        if (GameInventoryUI.Instance != null && GameInventoryUI.Instance.GetSelectedItem() == GameInventoryUI.ItemType.Adrenaline)
            return false;

        return true;
    }

    private bool IsGameplayLevelScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName)
               && (sceneName.StartsWith("Level") || sceneName.StartsWith("Chapter") || sceneName == "SampleScene");
    }

    private void EnsureLanternModel()
    {
        if (equippedLanternModel != null)
        {
            if (!lanternLightBoosted)
            {
                DungeonLightingManager readyDlm = Object.FindFirstObjectByType<DungeonLightingManager>();
                if (readyDlm != null)
                {
                    lanternLightBoosted = true;
                    readyDlm.SetCandleEquipped(true, 3f);
                }
            }
            ApplyLoadedPoseToModel();
            return;
        }

        Transform hand = FindRightHandAnchor();
        if (hand == null)
        {
            RemoveLanternModel();
            return;
        }

        if (equippedLanternModel == null)
        {
            equippedLanternModel = BuildLanternModel();
            if (equippedLanternModel == null)
                return;

            equippedLanternModel.name = "LanternEquipped";

            // Attach a local point light to the lantern (3x the candle's intensity/range).
            GameObject flameLightGO = new GameObject("LanternFlameLight");
            flameLightGO.transform.SetParent(equippedLanternModel.transform, false);
            flameLightGO.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            equippedLanternLight = flameLightGO.AddComponent<Light>();
            equippedLanternLight.type = LightType.Point;
            equippedLanternLight.color = new Color(1f, 0.85f, 0.55f, 1f);
            equippedLanternLight.intensity = 3.6f;   // candle is 1.2 → 3×
            equippedLanternLight.range = 9f;          // candle is 3 → 3×
            equippedLanternLight.shadows = LightShadows.None;

            ApplyLoadedPoseToModel();
        }

        // Boost the global player light once (3x), retrying until DungeonLightingManager is ready.
        if (!lanternLightBoosted)
        {
            DungeonLightingManager dlm = Object.FindFirstObjectByType<DungeonLightingManager>();
            if (dlm != null)
            {
                lanternLightBoosted = true;
                dlm.SetCandleEquipped(true, 3f);
            }
        }

        equippedLanternModel.transform.SetParent(hand, false);
        ApplyLoadedPoseToModel();
    }

    private void ApplyLoadedPoseToModel()
    {
        if (equippedLanternModel == null) return;

        if (attachedToCamera)
        {
            equippedLanternModel.transform.localPosition = cameraFallbackLocalPosition;
            equippedLanternModel.transform.localRotation = Quaternion.Euler(cameraFallbackLocalEuler);
        }
        else
        {
            equippedLanternModel.transform.localPosition = handLocalPosition;
            equippedLanternModel.transform.localRotation = Quaternion.Euler(handLocalEuler);
        }

        equippedLanternModel.transform.localScale = handLocalScale;
    }

    private void SaveLoadedPoseToPrefs()
    {
        PlayerPrefs.SetFloat(PrefHandPosX, handLocalPosition.x);
        PlayerPrefs.SetFloat(PrefHandPosY, handLocalPosition.y);
        PlayerPrefs.SetFloat(PrefHandPosZ, handLocalPosition.z);
        PlayerPrefs.SetFloat(PrefHandRotX, handLocalEuler.x);
        PlayerPrefs.SetFloat(PrefHandRotY, handLocalEuler.y);
        PlayerPrefs.SetFloat(PrefHandRotZ, handLocalEuler.z);
        PlayerPrefs.SetFloat(PrefHandScaleX, handLocalScale.x);
        PlayerPrefs.SetFloat(PrefHandScaleY, handLocalScale.y);
        PlayerPrefs.SetFloat(PrefHandScaleZ, handLocalScale.z);
        PlayerPrefs.SetFloat(PrefCamPosX, cameraFallbackLocalPosition.x);
        PlayerPrefs.SetFloat(PrefCamPosY, cameraFallbackLocalPosition.y);
        PlayerPrefs.SetFloat(PrefCamPosZ, cameraFallbackLocalPosition.z);
        PlayerPrefs.SetFloat(PrefCamRotX, cameraFallbackLocalEuler.x);
        PlayerPrefs.SetFloat(PrefCamRotY, cameraFallbackLocalEuler.y);
        PlayerPrefs.SetFloat(PrefCamRotZ, cameraFallbackLocalEuler.z);
        PlayerPrefs.Save();
    }

    private void RemoveLanternModel()
    {
        if (equippedLanternModel != null)
        {
            Destroy(equippedLanternModel);
            equippedLanternModel = null;
            equippedLanternLight = null;
        }

        if (lanternLightBoosted)
        {
            lanternLightBoosted = false;
            DungeonLightingManager dlm = Object.FindFirstObjectByType<DungeonLightingManager>();
            if (dlm != null) dlm.SetCandleEquipped(false);
        }
    }


    private void LoadSavedPose()
    {
        if (HasVector3(PrefHandPosX, PrefHandPosY, PrefHandPosZ))
            handLocalPosition = LoadVector3(PrefHandPosX, PrefHandPosY, PrefHandPosZ);

        if (HasVector3(PrefHandRotX, PrefHandRotY, PrefHandRotZ))
            handLocalEuler = LoadVector3(PrefHandRotX, PrefHandRotY, PrefHandRotZ);

        if (HasVector3(PrefHandScaleX, PrefHandScaleY, PrefHandScaleZ))
            handLocalScale = LoadVector3(PrefHandScaleX, PrefHandScaleY, PrefHandScaleZ);

        if (HasVector3(PrefCamPosX, PrefCamPosY, PrefCamPosZ))
            cameraFallbackLocalPosition = LoadVector3(PrefCamPosX, PrefCamPosY, PrefCamPosZ);

        if (HasVector3(PrefCamRotX, PrefCamRotY, PrefCamRotZ))
            cameraFallbackLocalEuler = LoadVector3(PrefCamRotX, PrefCamRotY, PrefCamRotZ);
    }

    private static bool HasVector3(string x, string y, string z)
    {
        return PlayerPrefs.HasKey(x) && PlayerPrefs.HasKey(y) && PlayerPrefs.HasKey(z);
    }

    private static Vector3 LoadVector3(string x, string y, string z)
    {
        return new Vector3(PlayerPrefs.GetFloat(x), PlayerPrefs.GetFloat(y), PlayerPrefs.GetFloat(z));
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

        Camera cam = Camera.main;
        if (cam == null)
        {
            Camera[] all = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].isActiveAndEnabled)
                {
                    cam = all[i];
                    break;
                }
            }
        }

        if (cam != null)
        {
            Transform anchor = cam.transform.Find("LanternHandAnchor");
            if (anchor == null)
            {
                GameObject anchorGO = new GameObject("LanternHandAnchor");
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

    private GameObject BuildLanternModel()
    {
#if UNITY_EDITOR
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(LanternObjPath);
        if (mesh != null)
        {
            GameObject go = new GameObject("LanternObjRuntime");
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
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            Texture2D metallic = LoadTextureFromAny(MetallicTexPaths);
            if (metallic != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.25f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);

            mr.sharedMaterial = mat;

            SphereCollider col = go.AddComponent<SphereCollider>();
            col.enabled = false;
            return go;
        }
#endif

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fallback.name = "LanternFallback";
        fallback.transform.localScale = new Vector3(0.14f, 0.20f, 0.14f);
        Renderer r = fallback.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = new Color(0.82f, 0.66f, 0.28f, 1f);
            r.material = m;
        }
        Collider c = fallback.GetComponent<Collider>();
        if (c != null) Destroy(c);
        return fallback;
    }

#if UNITY_EDITOR
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
}