using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class FixChapter2Scenes : EditorWindow
{
    [MenuItem("Tools/Fix Chapter 2 Scenes")]
    public static void FixScenes()
    {
        string[] sceneNames = { "Level5", "Level6", "Level7" };
        string[] fbxRootNames = { "chapter2problem5", "chapter2problem6", "chapter2problem7" };

        for (int i = 0; i < sceneNames.Length; i++)
        {
            string scenePath = $"Assets/Scenes/{sceneNames[i]}.unity";
            string fbxRootName = fbxRootNames[i];

            if (!System.IO.File.Exists(scenePath))
            {
                Debug.LogWarning($"Scene not found: {scenePath}, skipping...");
                continue;
            }

            Debug.Log($"=== Processing {sceneNames[i]} ===");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Find the FBX root object
            GameObject fbxRoot = GameObject.Find(fbxRootName);
            if (fbxRoot == null)
            {
                Debug.LogWarning($"FBX root '{fbxRootName}' not found in {sceneNames[i]}, skipping...");
                continue;
            }

            // 1. Calculate a good spawn position above the floor tiles
            Vector3 floorCenter = Vector3.zero;
            float highestFloorY = float.MinValue;
            int floorCount = 0;

            // Find all floor meshes
            MeshRenderer[] allRenderers = fbxRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var renderer in allRenderers)
            {
                if (renderer.gameObject.name.StartsWith("Floor_Flat"))
                {
                    floorCenter += renderer.bounds.center;
                    if (renderer.bounds.max.y > highestFloorY)
                        highestFloorY = renderer.bounds.max.y;
                    floorCount++;
                }
            }

            if (floorCount > 0)
            {
                floorCenter /= floorCount;
                Debug.Log($"Found {floorCount} floor tiles. Center: {floorCenter}, Highest Y: {highestFloorY}");
            }
            else
            {
                // Fallback: use all renderers
                foreach (var renderer in allRenderers)
                {
                    floorCenter += renderer.bounds.center;
                    if (renderer.bounds.max.y > highestFloorY)
                        highestFloorY = renderer.bounds.max.y;
                    floorCount++;
                }
                if (floorCount > 0)
                    floorCenter /= floorCount;
                Debug.Log($"No Floor_Flat found, using all {floorCount} renderers. Center: {floorCenter}");
            }

            // 2. Move the player to a good position
            GameObject player = GameObject.Find("FirstPersonPlayer");
            if (player != null && floorCount > 0)
            {
                // Place player 1.5 units above the highest floor point, at the center
                Vector3 spawnPos = new Vector3(floorCenter.x, highestFloorY + 1.5f, floorCenter.z);
                player.transform.position = spawnPos;
                Debug.Log($"Moved player to: {spawnPos}");
            }

            // 3. Add MeshColliders to any mesh that doesn't have one
            MeshFilter[] allMeshFilters = fbxRoot.GetComponentsInChildren<MeshFilter>(true);
            int collidersAdded = 0;
            foreach (var mf in allMeshFilters)
            {
                if (mf.GetComponent<Collider>() == null)
                {
                    MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    collidersAdded++;
                }
            }
            Debug.Log($"Added {collidersAdded} MeshColliders to objects without colliders.");

            // 4. Make sure the FBX directional light inside the FBX is disabled 
            // (the scene already has DungeonLighting)
            Transform fbxLight = fbxRoot.transform.Find("Directional_Light");
            if (fbxLight != null)
            {
                Light lightComp = fbxLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.enabled = false;
                    Debug.Log("Disabled FBX embedded directional light");
                }
            }

            // 5. Disable any FBX embedded camera
            Transform fbxCamera = fbxRoot.transform.Find("Main_Camera");
            if (fbxCamera != null)
            {
                fbxCamera.gameObject.SetActive(false);
                Debug.Log("Disabled FBX embedded camera");
            }

            // 6. Also update SpawnPoint1 to the player's position for consistency
            GameObject sp1 = GameObject.Find("SpawnPoint1");
            if (sp1 != null && player != null)
            {
                sp1.transform.position = player.transform.position;
                Debug.Log($"Updated SpawnPoint1 to: {player.transform.position}");
            }

            // 7. Delete the old inactive "Environment" from Level1 if it exists
            GameObject oldEnv = GameObject.Find("Environment");
            if (oldEnv == null)
            {
                // It might be inactive, search root objects
                foreach (GameObject rootObj in scene.GetRootGameObjects())
                {
                    if (rootObj.name == "Environment" && !rootObj.activeSelf)
                    {
                        oldEnv = rootObj;
                        break;
                    }
                }
            }
            if (oldEnv != null && !oldEnv.activeSelf)
            {
                DestroyImmediate(oldEnv);
                Debug.Log("Removed old inactive Environment object from Level1 template");
            }

            // Save the scene
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"=== {sceneNames[i]} saved successfully ===\n");
        }

        Debug.Log("All Chapter 2 scenes have been fixed!");
        EditorUtility.DisplayDialog("Done", "All Chapter 2 scenes have been fixed!\n\n- Player repositioned above floor\n- MeshColliders added\n- Old Environment removed\n- FBX lights/cameras disabled", "OK");
    }
}
