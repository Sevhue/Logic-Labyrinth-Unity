#if UNITY_EDITOR
using System;
using System.Reflection;
using Unity.Burst;
using UnityEditor;

[InitializeOnLoad]
public static class DisableBurstCompilationForProject
{
    static DisableBurstCompilationForProject()
    {
        DisableBurst();
        EditorApplication.delayCall += DisableBurst;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredEditMode)
            DisableBurst();
    }

    private static void DisableBurst()
    {
        EditorPrefs.SetBool("BurstCompilation", false);
        BurstCompiler.Options.EnableBurstCompilation = false;

        // Also force the editor-side Burst option if the internal type is present.
        Type burstEditorOptionsType = Type.GetType("Unity.Burst.Editor.BurstEditorOptions, Unity.Burst.Editor");
        if (burstEditorOptionsType != null)
        {
            PropertyInfo enableProp = burstEditorOptionsType.GetProperty(
                "EnableBurstCompilation",
                BindingFlags.Public | BindingFlags.Static);

            if (enableProp != null && enableProp.CanWrite)
                enableProp.SetValue(null, false);
        }
    }
}
#endif
