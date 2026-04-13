using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScannerHandController : MonoBehaviour
{
    private const string PrefHandPosX = "LL_SCN_HAND_POS_X";
    private const string PrefHandPosY = "LL_SCN_HAND_POS_Y";
    private const string PrefHandPosZ = "LL_SCN_HAND_POS_Z";
    private const string PrefHandRotX = "LL_SCN_HAND_ROT_X";
    private const string PrefHandRotY = "LL_SCN_HAND_ROT_Y";
    private const string PrefHandRotZ = "LL_SCN_HAND_ROT_Z";
    private const string PrefHandScaleX = "LL_SCN_HAND_SCALE_X";
    private const string PrefHandScaleY = "LL_SCN_HAND_SCALE_Y";
    private const string PrefHandScaleZ = "LL_SCN_HAND_SCALE_Z";

    private const string PrefCamPosX = "LL_SCN_CAM_POS_X";
    private const string PrefCamPosY = "LL_SCN_CAM_POS_Y";
    private const string PrefCamPosZ = "LL_SCN_CAM_POS_Z";
    private const string PrefCamRotX = "LL_SCN_CAM_ROT_X";
    private const string PrefCamRotY = "LL_SCN_CAM_ROT_Y";
    private const string PrefCamRotZ = "LL_SCN_CAM_ROT_Z";

    private const string ScannerObjPath = "Assets/Store/Purchase/Scanner/source/model/base.obj";
    private static readonly string[] DiffuseTexPaths =
    {
        "Assets/Store/Purchase/Scanner/textures/texture_diffuse.png",
        "Assets/Store/Purchase/Scanner/source/model/texture_diffuse.png"
    };
    private static readonly string[] NormalTexPaths =
    {
        "Assets/Store/Purchase/Scanner/textures/texture_normal.png",
        "Assets/Store/Purchase/Scanner/source/model/texture_normal.png"
    };
    private static readonly string[] MetallicTexPaths =
    {
        "Assets/Store/Purchase/Scanner/textures/texture_metallic.png",
        "Assets/Store/Purchase/Scanner/source/model/texture_metallic.png"
    };

    private static ScannerHandController instance;
    private GameObject equippedScannerModel;
    private bool attachedToCamera;
    private bool scannerEditLockActive;
    private bool cachedArmAnimatorEnabled;
    private bool cachedCursorInputForLook;
    private bool cachedCursorLocked;

    [Header("Pose")]
    public KeyCode savePoseKey = KeyCode.P;
    public bool autoSavePoseOnDisable = true;

    [Header("View Model")]
    public Vector3 handLocalPosition = new Vector3(-0.01f, 0.02f, 0.02f);
    public Vector3 handLocalEuler = new Vector3(-75f, 0f, 10f);
    public Vector3 handLocalScale = new Vector3(0.10f, 0.10f, 0.10f);
    public Vector3 cameraFallbackLocalPosition = new Vector3(0.24f, -0.24f, 0.36f);
    public Vector3 cameraFallbackLocalEuler = new Vector3(8f, -18f, 12f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("ScannerHandController");
        instance = go.AddComponent<ScannerHandController>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        LoadSavedPose();
        SaveLoadedPoseToPrefs();
    }

    private void Update()
    {
        bool shouldShow = ShouldShowScannerInHand();

        if (shouldShow)
            EnsureScannerModel();
        else
            RemoveScannerModel();

        if (equippedScannerModel != null)
            ApplyLoadedPoseToModel();
    }

    private void OnDisable()
    {
        RemoveScannerModel();
    }

    private bool ShouldShowScannerInHand()
    {
        if (!IsGameplayLevelScene()) return false;
        if (AccountManager.Instance == null) return false;
        if (AccountManager.Instance.GetScannerCount() <= 0) return false;
        if (GameInventoryUI.Instance == null || GameInventoryUI.Instance.GetSelectedItem() != GameInventoryUI.ItemType.Scanner)
            return false;
        if (CollectibleCandle.IsEquipped)
            return false;
        return true;
    }

    private static bool IsGameplayLevelScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName)
               && (sceneName.StartsWith("Level") || sceneName.StartsWith("Chapter") || sceneName == "SampleScene");
    }

    private void EnsureScannerModel()
    {
        if (equippedScannerModel != null)
        {
            ApplyLoadedPoseToModel();
            return;
        }

        Transform hand = FindRightHandAnchor();
        if (hand == null)
        {
            RemoveScannerModel();
            return;
        }

        equippedScannerModel = BuildScannerModel();
        if (equippedScannerModel == null)
            return;

        equippedScannerModel.name = "ScannerEquipped";
        equippedScannerModel.transform.SetParent(hand, false);

        ApplyLoadedPoseToModel();
    }

    private void ApplyLoadedPoseToModel()
    {
        if (equippedScannerModel == null) return;

        if (attachedToCamera)
        {
            equippedScannerModel.transform.localPosition = cameraFallbackLocalPosition;
            equippedScannerModel.transform.localRotation = Quaternion.Euler(cameraFallbackLocalEuler);
        }
        else
        {
            equippedScannerModel.transform.localPosition = handLocalPosition;
            equippedScannerModel.transform.localRotation = Quaternion.Euler(handLocalEuler);
        }

        equippedScannerModel.transform.localScale = handLocalScale;
    }

    private void SaveLoadedPoseToPrefs()
    {
        SaveVector3(PrefHandPosX, PrefHandPosY, PrefHandPosZ, handLocalPosition);
        SaveVector3(PrefHandRotX, PrefHandRotY, PrefHandRotZ, handLocalEuler);
        SaveVector3(PrefHandScaleX, PrefHandScaleY, PrefHandScaleZ, handLocalScale);
        SaveVector3(PrefCamPosX, PrefCamPosY, PrefCamPosZ, cameraFallbackLocalPosition);
        SaveVector3(PrefCamRotX, PrefCamRotY, PrefCamRotZ, cameraFallbackLocalEuler);
        PlayerPrefs.Save();
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

    private static void SaveVector3(string x, string y, string z, Vector3 value)
    {
        PlayerPrefs.SetFloat(x, value.x);
        PlayerPrefs.SetFloat(y, value.y);
        PlayerPrefs.SetFloat(z, value.z);
    }

    private void RemoveScannerModel()
    {
        if (equippedScannerModel != null)
        {
            Destroy(equippedScannerModel);
            equippedScannerModel = null;
        }
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
            Transform anchor = cam.transform.Find("ScannerHandAnchor");
            if (anchor == null)
            {
                GameObject anchorGO = new GameObject("ScannerHandAnchor");
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
            if (nested != null)
                return nested;
        }

        return null;
    }

    private GameObject BuildScannerModel()
    {
#if UNITY_EDITOR
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ScannerObjPath);
        if (mesh != null)
        {
            GameObject go = new GameObject("ScannerObjRuntime");
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

        GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fallback.name = "ScannerFallback";
        fallback.transform.localScale = new Vector3(0.18f, 0.08f, 0.14f);
        Renderer r = fallback.GetComponent<Renderer>();
        if (r != null)
        {
            Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.color = new Color(0.18f, 0.72f, 0.82f, 1f);
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
