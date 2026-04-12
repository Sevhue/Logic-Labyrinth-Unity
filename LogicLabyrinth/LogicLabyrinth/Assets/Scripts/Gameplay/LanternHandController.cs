using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class LanternHandController : MonoBehaviour
{
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
    private bool attachedToCamera;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;

        GameObject go = new GameObject("LanternHandController");
        instance = go.AddComponent<LanternHandController>();
        DontDestroyOnLoad(go);
    }

    private void Update()
    {
        bool shouldShow = ShouldShowLanternInHand();

        if (shouldShow)
            EnsureLanternModel();
        else
            RemoveLanternModel();
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

        if (CollectibleCandle.IsEquipped)
            return false;

        if (GameInventoryUI.Instance != null && GameInventoryUI.Instance.GetSelectedItem() == GameInventoryUI.ItemType.Adrenaline)
            return false;

        return true;
    }

    private bool IsGameplayLevelScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Level");
    }

    private void EnsureLanternModel()
    {
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
        }

        if (equippedLanternModel.transform.parent != hand)
            equippedLanternModel.transform.SetParent(hand, false);

        if (attachedToCamera)
        {
            equippedLanternModel.transform.localPosition = new Vector3(0.22f, -0.28f, 0.40f);
            equippedLanternModel.transform.localRotation = Quaternion.Euler(5f, -20f, 8f);
            equippedLanternModel.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
        }
        else
        {
            equippedLanternModel.transform.localPosition = new Vector3(-0.01f, 0.03f, 0.01f);
            equippedLanternModel.transform.localRotation = Quaternion.Euler(-80f, 0f, 0f);
            equippedLanternModel.transform.localScale = new Vector3(0.11f, 0.11f, 0.11f);
        }
    }

    private void RemoveLanternModel()
    {
        if (equippedLanternModel != null)
        {
            Destroy(equippedLanternModel);
            equippedLanternModel = null;
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