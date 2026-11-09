using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Repository tooling (not part of the package): imports the Chat sample and builds the WebGL demo.
/// All methods can be run with -executeMethod. ImportSample overwrites Assets/Samples/Chat, and BuildWebGL
/// changes the project's WebGL player settings; edit the sample in Samples~ instead.
/// </summary>
public static class DemoBuilder
{
    const string PackageSample = "Packages/com.ryuchan00.local-llm/Samples~/Chat";
    const string ImportedSample = "Assets/Samples/Chat";
    const string Scene = ImportedSample + "/Chat.unity";
    const string Font = ImportedSample + "/Fonts/BIZUDGothic-Regular.ttf";
    const string OutputPath = "Builds/WebGL";

    /// <summary>Copies the sample into Assets, the way Package Manager's Import button does.</summary>
    [MenuItem("Tools/Local LLM Demo/Import Chat Sample")]
    public static void ImportSample()
    {
        FileUtil.DeleteFileOrDirectory(ImportedSample);
        FileUtil.DeleteFileOrDirectory(ImportedSample + ".meta");
        Directory.CreateDirectory(Path.GetDirectoryName(ImportedSample));
        FileUtil.CopyFileOrDirectory(Path.GetFullPath(PackageSample), ImportedSample);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    /// <summary>Creates the sample scene. Used once when authoring the sample; the result is copied back to Samples~.</summary>
    public static void CreateSampleScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.13f, 0.16f);

        // Looked up by name: the sample assembly only exists after ImportSample.
        var chatType = System.Type.GetType("LocalLLM.Samples.ChatWindow, LocalLLM.Samples.Chat", throwOnError: true);
        var chat = new GameObject("Chat").AddComponent(chatType);
        var serialized = new SerializedObject(chat);
        serialized.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(Font);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, Scene);
    }

    [MenuItem("Tools/Local LLM Demo/Build WebGL Demo")]
    public static void BuildWebGL()
    {
        ImportSample();

        PlayerSettings.WebGL.template = "PROJECT:LocalLLM";
        // Uncompressed: GitHub Pages compresses responses itself, and Unity's decompression fallback
        // would load the framework from a blob: URL, which the page's CSP (script-src 'self') blocks.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.runInBackground = true;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[Demo] WebGL build {summary.result}: {summary.totalSize / (1024 * 1024)} MB, {summary.totalErrors} errors");
        if (Application.isBatchMode && summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
