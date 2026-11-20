using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class OutlineRendererFeatureInstaller
{
    private static readonly string[] RendererPaths =
    {
        "Assets/Settings/URP-Balanced-Renderer.asset",
        "Assets/Settings/URP-HighFidelity-Renderer.asset",
        "Assets/Settings/URP-Performant-Renderer.asset",
    };

    private const string Folder = "Assets/Scripts/Rendering/Outline/";

    static OutlineRendererFeatureInstaller() => EditorApplication.delayCall += EnsureInstalledOnLoad;

    [MenuItem("Tools/Rendering/Install Outline Feature")]
    public static void InstallFromMenu() => EnsureInstalled();

    private static void EnsureInstalledOnLoad()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        EnsureInstalled();
    }

    private static void EnsureInstalled()
    {
        var extrusion = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "OutlineExtrusion.shader");
        var composite = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "OutlineComposite.shader");
        var blur = AssetDatabase.LoadAssetAtPath<Shader>(Folder + "OutlineBlur.shader");
        if (extrusion == null || composite == null || blur == null) return;

        foreach (string path in RendererPaths)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (rendererData == null) continue;

            var feature = FindFeature(rendererData) ?? CreateAndRegisterFeature(rendererData);
            if (feature == null) continue;

            var so = new SerializedObject(feature);
            so.FindProperty("m_Name").stringValue = nameof(OutlineRendererFeature);
            so.FindProperty("m_Active").boolValue = true;
            so.FindProperty("settings.extrusionShader").objectReferenceValue = extrusion;
            so.FindProperty("settings.compositeShader").objectReferenceValue = composite;
            so.FindProperty("settings.blurShader").objectReferenceValue = blur;
            if (!so.ApplyModifiedPropertiesWithoutUndo()) continue;

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.SaveAssetIfDirty(feature);
            AssetDatabase.SaveAssetIfDirty(rendererData);
        }
    }

    private static OutlineRendererFeature FindFeature(UniversalRendererData rendererData)
    {
        foreach (var f in rendererData.rendererFeatures)
            if (f is OutlineRendererFeature outline) return outline;
        return null;
    }

    private static OutlineRendererFeature CreateAndRegisterFeature(UniversalRendererData rendererData)
    {
        var feature = ScriptableObject.CreateInstance<OutlineRendererFeature>();
        feature.name = nameof(OutlineRendererFeature);
        AssetDatabase.AddObjectToAsset(feature, rendererData);

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId)) return null;

        var rendererObject = new SerializedObject(rendererData);
        var features = rendererObject.FindProperty("m_RendererFeatures");
        var map = rendererObject.FindProperty("m_RendererFeatureMap");
        int index = features.arraySize;
        features.arraySize++;
        features.GetArrayElementAtIndex(index).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(index).longValue = localId;
        rendererObject.ApplyModifiedPropertiesWithoutUndo();
        return feature;
    }
}
