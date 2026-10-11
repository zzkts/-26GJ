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
            // 用最小场景 + 运行时生成，**不用烘焙出来的大场景**。
            //
            // 原因（实测，别改回去）：烘焙场景打包后，播放器读 level0 直接崩：
            //   The file '..._Data/level0' is corrupted!  [Position out of bounds!]
            // 试过剔除所有"指向运行期 Sprite 的引用"（161 处）仍然崩，根因未定位，
            // 怀疑是 Unity 对上千个 UI 对象的场景序列化问题。
            // 最小场景这条路已经反复验证可用，所以打包走它。
            //
            // 烘焙（菜单「5. 烘焙成可编辑场景」）保留为**编辑器内的编辑与预览手段**，
            // 不要用它出包。见 _doc/02-新版交付说明.md 第 6.6 节。
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
