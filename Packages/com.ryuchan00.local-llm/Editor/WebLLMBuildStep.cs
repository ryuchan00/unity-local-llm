using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LocalLLM.Editor
{
    /// <summary>
    /// Copies the bundled WebLLM into StreamingAssets/local-llm/ of every WebGL build.
    /// Packages cannot ship StreamingAssets, so this runs after the build.
    /// Every bundled file is checked against its recorded SHA-256 first, and nothing is copied if one differs.
    /// Builds that define LOCALLLM_FORCE_OLLAMA do not use WebLLM and get nothing.
    /// </summary>
    sealed class WebLLMBuildStep : IPostprocessBuildWithReport
    {
        internal const string WebLLMFile = "web-llm.js";
        internal const string LoaderFile = "local-llm-loader.js";
        const string ForceOllamaDefine = "LOCALLLM_FORCE_OLLAMA";

        // SHA-256 of every file copied into builds. web-llm.js is lib/index.js of @mlc-ai/web-llm 0.2.86
        // from the npm registry; the loader holds the model pins. Update these when changing the files.
        internal static readonly IReadOnlyDictionary<string, string> Sha256 = new Dictionary<string, string>
        {
            [WebLLMFile] = "ae2642e73a4a9896c21efb3a067c4666446f8b49cbe2012f4b576f5d83d5f206",
            [LoaderFile] = "0f6819e21b041f68e62acb6c81caaa5ca75eecf2513f99f9a21687e45c6cee97",
            ["web-llm.LICENSE.txt"] = "87935f332472db09253871326ef23d5d6a45f4fd2e6c96772f72052866dfab0d",
        };

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL) return;
            if (report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled) return;
            if (UsesOllama()) return;

            var destination = Path.Combine(report.summary.outputPath, "StreamingAssets", WebLLMBackend.StreamingAssetsFolder);
            CopyTo(destination);
            Debug.Log($"[LocalLLM] Copied WebLLM to {destination}");
        }

        internal static string SourceDirectory()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(WebLLMBuildStep).Assembly);
            if (package == null) throw new InvalidOperationException("LocalLLM package not found.");
            return Path.Combine(package.resolvedPath, "WebLLM~");
        }

        static bool UsesOllama()
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.WebGL);
            return defines.Split(';').Contains(ForceOllamaDefine);
        }

        internal static void CopyTo(string destination)
        {
            var source = SourceDirectory();
            // Verify everything before copying anything.
            foreach (var file in Sha256.Keys) Verify(Path.Combine(source, file), file);
            Directory.CreateDirectory(destination);
            foreach (var file in Sha256.Keys)
                File.Copy(Path.Combine(source, file), Path.Combine(destination, file), overwrite: true);
        }

        internal static void Verify(string path, string file)
        {
            var expected = Sha256[file];
            var actual = Sha256Hex(path);
            if (actual != expected)
                throw new BuildFailedException(
                    $"[LocalLLM] {path} does not match the bundled file (SHA-256 {actual}, expected {expected}). " +
                    "Reinstall the package; do not ship a modified file.");
        }

        internal static string Sha256Hex(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return string.Concat(sha.ComputeHash(stream).Select(b => b.ToString("x2")));
        }
    }
}
