using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JieNiYiMiao.EditorTools
{
    /// <summary>
    /// 打包脚本：输出 Windows 64 位独立版本到 Build/Windows/。
    /// 手册要求"打包版本不依赖 Editor 专属对象或开发时的临时路径"，
    /// 所以这里只用 Build Settings 里的场景列表，并且构建前会重新写一遍该列表。
    ///
    /// 调用方式：
    ///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod JieNiYiMiao.EditorTools.BuildScript.BuildWindows -logFile &lt;日志&gt;
    /// </summary>
    public static class BuildScript
    {
        private const string OutputDirectory = "Build/Windows";
        private const string ExecutableName = "JieNiYiMiao.exe";

        [MenuItem("借你一秒/4. 打包 Windows 版本")]
        public static void BuildWindows()
        {
            // 保证入口场景一定在列表里且启用，避免打出空包
            ProjectSetup.WriteBuildSettings();

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputPath = Path.Combine(projectRoot, OutputDirectory);
            Directory.CreateDirectory(outputPath);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.MainScenePath },
                locationPathName = Path.Combine(outputPath, ExecutableName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log("[借你一秒] 打包成功：" + options.locationPathName
                    + "（" + (summary.totalSize / 1048576f).ToString("0.0") + " MB，"
                    + summary.totalTime.TotalSeconds.ToString("0") + " 秒）");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("[借你一秒] 打包失败：" + summary.result + "，错误 " + summary.totalErrors + " 条");
            EditorApplication.Exit(1);
        }
    }
}
