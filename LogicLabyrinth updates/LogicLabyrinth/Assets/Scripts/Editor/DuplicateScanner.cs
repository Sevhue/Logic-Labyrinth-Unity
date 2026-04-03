
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class DuplicateScanner : EditorWindow
{
    [MenuItem("Tools/Scan Duplicates")]
    public static void ScanAllScenes()
    {
        string[] scenePaths = new string[]
        {
            "Assets/Scenes/Level1.unity",
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
            "Assets/Scenes/Level4.unity",
            "Assets/Scenes/Level5.unity",
            "Assets/Scenes/Level6.unity",
            "Assets/Scenes/Level7.unity",
            "Assets/Scenes/Level8.unity",
            "Assets/Scenes/Level9.unity",
            "Assets/Scenes/Level10.unity",
            "Assets/Scenes/Level11.unity",
            "Assets/Scenes/Level12.unity",
            "Assets/Scenes/Main.unity"
        };

        var totalReport = new StringBuilder();
        int totalDups = 0;

        foreach (var scenePath in scenePaths)
        {
            if (!System.IO.File.Exists(scenePath)) continue;
            
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            
            var allTransforms = new List<Transform>();
            foreach (var root in roots)
                allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));

            var byParent = new Dictionary<int, List<Transform>>();
            foreach (var t in allTransforms)
            {
                int key = t.parent != null ? t.parent.GetInstanceID() : -1;
                if (!byParent.ContainsKey(key))
                    byParent[key] = new List<Transform>();
                byParent[key].Add(t);
            }

            int dupCount = 0;
            var sceneDups = new StringBuilder();

            foreach (var kvp in byParent)
            {
                var nameMap = new Dictionary<string, List<Transform>>();
                foreach (var t in kvp.Value)
                {
                    string baseName = GetBaseName(t.name);
                    if (!nameMap.ContainsKey(baseName))
                        nameMap[baseName] = new List<Transform>();
                    nameMap[baseName].Add(t);
                }

                foreach (var nm in nameMap)
                {
                    if (nm.Value.Count < 2) continue;
                    var sorted = nm.Value.OrderBy(t => t.name.Length).ThenBy(t => t.name).ToList();
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        for (int j = i + 1; j < sorted.Count; j++)
                        {
                            float dist = Vector3.Distance(sorted[i].position, sorted[j].position);
                            float rotDiff = Quaternion.Angle(sorted[i].rotation, sorted[j].rotation);
                            if (dist < 0.01f && rotDiff < 0.5f)
                            {
                                dupCount++;
                                sceneDups.AppendLine("  DUP: " + GetPath(sorted[j]) + " overlaps " + sorted[i].name);
                            }
                        }
                    }
                }
            }

            if (dupCount > 0)
            {
                totalReport.AppendLine("=== " + scene.name + " (" + dupCount + " dups) ===");
                totalReport.Append(sceneDups);
                totalDups += dupCount;
            }
        }

        if (totalDups == 0)
            Debug.Log("[DuplicateScanner] No duplicates found.");
        else
            Debug.Log("[DuplicateScanner] Found " + totalDups + " total duplicates:\n" + totalReport.ToString());
    }

    [MenuItem("Tools/Remove Duplicate GameObjects")]
    public static void RemoveDuplicatesAllScenes()
    {
        string[] scenePaths = new string[]
        {
            "Assets/Scenes/Level1.unity",
            "Assets/Scenes/Level2.unity",
            "Assets/Scenes/Level3.unity",
            "Assets/Scenes/Level4.unity",
            "Assets/Scenes/Level5.unity",
            "Assets/Scenes/Level6.unity",
            "Assets/Scenes/Level7.unity",
            "Assets/Scenes/Level8.unity",
            "Assets/Scenes/Level9.unity",
            "Assets/Scenes/Level10.unity",
            "Assets/Scenes/Level11.unity",
            "Assets/Scenes/Level12.unity",
            "Assets/Scenes/Main.unity"
        };

        int totalRemoved = 0;

        foreach (var scenePath in scenePaths)
        {
            if (!System.IO.File.Exists(scenePath)) continue;
            
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            
            var allTransforms = new List<Transform>();
            foreach (var root in roots)
                allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));

            var byParent = new Dictionary<int, List<Transform>>();
            foreach (var t in allTransforms)
            {
                int key = t.parent != null ? t.parent.GetInstanceID() : -1;
                if (!byParent.ContainsKey(key))
                    byParent[key] = new List<Transform>();
                byParent[key].Add(t);
            }

            var toRemove = new List<GameObject>();

            foreach (var kvp in byParent)
            {
                var nameMap = new Dictionary<string, List<Transform>>();
                foreach (var t in kvp.Value)
                {
                    string baseName = GetBaseName(t.name);
                    if (!nameMap.ContainsKey(baseName))
                        nameMap[baseName] = new List<Transform>();
                    nameMap[baseName].Add(t);
                }

                foreach (var nm in nameMap)
                {
                    if (nm.Value.Count < 2) continue;
                    
                    // Sort so the original (shorter name) comes first
                    var sorted = nm.Value.OrderBy(t => t.name.Length).ThenBy(t => t.name).ToList();
                    
                    // Keep a set of already-matched originals to avoid removing both
                    var matched = new HashSet<int>();
                    
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        if (matched.Contains(sorted[i].GetInstanceID())) continue;
                        
                        for (int j = i + 1; j < sorted.Count; j++)
                        {
                            if (matched.Contains(sorted[j].GetInstanceID())) continue;
                            
                            float dist = Vector3.Distance(sorted[i].position, sorted[j].position);
                            float rotDiff = Quaternion.Angle(sorted[i].rotation, sorted[j].rotation);
                            
                            if (dist < 0.01f && rotDiff < 0.5f)
                            {
                                // The .001 version is the duplicate — remove it
                                // But only if it doesn't have MORE components/children that the original lacks
                                var original = sorted[i];
                                var duplicate = sorted[j];
                                
                                int origComponents = original.GetComponents<Component>().Length;
                                int dupComponents = duplicate.GetComponents<Component>().Length;
                                
                                // Skip if the "duplicate" has more components (it might have scripts attached)
                                if (dupComponents > origComponents + 1)
                                    continue;
                                
                                // Skip certain important objects
                                string dupName = duplicate.name.ToLower();
                                if (dupName.Contains("use") ||
                                    dupName.Contains("manager") || dupName.Contains("controller") || 
                                    dupName.Contains("camera") || dupName.Contains("light") ||
                                    dupName.Contains("player") || dupName.Contains("canvas") ||
                                    dupName.Contains("eventsystem") || dupName.Contains("puzzle") ||
                                    dupName.Contains("interactive") || dupName.Contains("success") ||
                                    dupName.Contains("tutorial") || dupName.Contains("door") ||
                                    dupName.Contains("trigger") || dupName.Contains("key") ||
                                    dupName.Contains("candle") || dupName.Contains("cutscene") ||
                                    dupName.Contains("inventory") || dupName.Contains("ui") ||
                                    dupName.Contains("gate") || dupName.Contains("portal"))
                                    continue;
                                
                                toRemove.Add(duplicate.gameObject);
                                matched.Add(duplicate.GetInstanceID());
                                
                                Debug.Log("[DupRemover] " + scene.name + ": Removing \"" + GetPath(duplicate) + "\" (overlaps \"" + original.name + "\")");
                            }
                        }
                    }
                }
            }

            if (toRemove.Count > 0)
            {
                foreach (var go in toRemove)
                {
                    if (go != null)
                        Object.DestroyImmediate(go);
                }
                totalRemoved += toRemove.Count;
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[DupRemover] " + scene.name + ": Removed " + toRemove.Count + " duplicates and saved.");
            }
        }

        Debug.Log("[DupRemover] Done! Total removed: " + totalRemoved);
    }

    static string GetBaseName(string name)
    {
        if (name.Length > 4)
        {
            string suffix = name.Substring(name.Length - 4);
            if (suffix[0] == '.' && char.IsDigit(suffix[1]) && char.IsDigit(suffix[2]) && char.IsDigit(suffix[3]))
                return name.Substring(0, name.Length - 4);
        }
        return name;
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        var current = t.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }
}
#endif
