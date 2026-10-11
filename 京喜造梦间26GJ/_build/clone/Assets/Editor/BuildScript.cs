using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CuoGuo.EditorTools
{
    /// <summary>
    /// 打包脚本：输出 Windows 64 位独立版本到 Build/Windows/。
    /// 只用 Build Settings 里的场景列表，构建前会重新写一遍该列表，避免打出空包。
    ///
    /// 调用方式：
    ///   Unity.exe -batchmode -quit -nographics -projectPath &lt;工程&gt; -logFile &lt;日志&gt; `
    ///     -buildTarget StandaloneWindows64 -executeMethod CuoGuo.EditorTools.BuildScript.BuildWindows
    /// </summary>
    public static class BuildScript
    {
        private const string OutputDirectory = "Build/Windows";
        private const string ExecutableName = "CuoGuoNaYiMiao.exe";

        /// <summary>
        /// 一条命令走完"生成入口场景 + 打包"。
        /// 供命令行无头构建使用，避免先跑一次场景生成再跑一次打包。
        /// </summary>
        [MenuItem("错过了那一秒/3. 一键生成场景并打包")]
        public static void PrepareAndBuildWindows()
        {
            ProjectSetup.SetupScene();
            BuildWindows();
        }

        [MenuItem("错过了那一秒/2. 打包 Windows 版本")]
        public static void BuildWindows()
        {
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
                Debug.Log("[错过了那一秒] 打包成功：" + options.locationPathName
                    + "（" + (summary.totalSize / 1048576f).ToString("0.0") + " MB，"
                    + summary.totalTime.TotalSeconds.ToString("0") + " 秒）");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError("[错过了那一秒] 打包失败：" + summary.result + "，错误 " + summary.totalErrors + " 条");
            EditorApplication.Exit(1);
        }
    }
}
