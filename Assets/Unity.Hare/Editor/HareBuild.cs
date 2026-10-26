using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Builds the standalone Windows player for development: a bare build (no icon, product-settings polish or
    /// release checklist -- those are U.HARE-T71) good enough for <c>tools/play.ps1</c> to launch and prove the
    /// game runs, and sounds, as built -- not just in the Editor (U.HARE-T80).
    /// tools/deploy.ps1 runs this in batch mode:
    /// Unity -batchmode -quit -projectPath . -executeMethod Unity.Hare.EditorTools.HareBuild.BuildWindows
    /// </summary>
    public static class HareBuild
    {
        public const string WindowsPath = "Build/Hare/Hare.exe";

        [MenuItem("Unity.Hare/Build Windows Player (dev)")]
        public static void BuildWindows()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(WindowsPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = WindowsPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            var summary = report.summary;
            Debug.Log($"[Unity.Hare] build: {summary.result}, {summary.totalErrors} errors, {summary.totalSize} bytes -> {WindowsPath}");
            if (Application.isBatchMode)
                EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
