using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ExportSceneInfo
{
    private const string OutputPath = "Assets/scene_hierarchy.txt";

    [MenuItem("Tools/Diagnostics/Export Active Scene Info")]
    public static void ExportActiveSceneInfo()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogError("No active scene is loaded.");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Scene: {scene.name}");
        builder.AppendLine($"Path: {scene.path}");
        builder.AppendLine($"Root Objects: {scene.rootCount}");
        builder.AppendLine();

        foreach (var root in scene.GetRootGameObjects())
        {
            WriteTransform(builder, root.transform, 0);
        }

        File.WriteAllText(OutputPath, builder.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"Scene info exported to {OutputPath}");
        EditorUtility.RevealInFinder(OutputPath);
    }

    private static void WriteTransform(StringBuilder builder, Transform transform, int depth)
    {
        string indent = new string(' ', depth * 2);
        var gameObject = transform.gameObject;

        builder.Append(indent);
        builder.Append("- ");
        builder.Append(gameObject.name);
        builder.Append(" [");
        builder.Append(gameObject.activeInHierarchy ? "Active" : "Inactive");
        builder.Append("] ");
        builder.Append("Pos=");
        builder.Append(transform.position);

        var components = gameObject.GetComponents<Component>();
        builder.Append(" Components: ");
        for (int i = 0; i < components.Length; i++)
        {
            var componentName = components[i] == null ? "MissingScript" : components[i].GetType().Name;
            builder.Append(componentName);
            if (i < components.Length - 1)
            {
                builder.Append(", ");
            }
        }

        builder.AppendLine();

        for (int i = 0; i < transform.childCount; i++)
        {
            WriteTransform(builder, transform.GetChild(i), depth + 1);
        }
    }
}
