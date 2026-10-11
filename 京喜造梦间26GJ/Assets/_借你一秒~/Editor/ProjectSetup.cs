using System.Collections.Generic;
using JieNiYiMiao;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JieNiYiMiao.EditorTools
{
    /// <summary>
    /// 用命令行为工程准备入口场景：
    /// 1. 创建 Assets/Scenes/Main.unity，里面只有一个挂着 GameBootstrap 的物体；
    /// 2. 把它写进 Build Settings 的场景列表并置顶；
    /// 3. 顺手把 URP 的全局设置确认可用（本工程已自带，不改动）。
    ///
    /// 之所以用脚本生成而不是手写一份场景 YAML：场景序列化格式细节多，
    /// 交给 Unity 自己写能避免版本差异导致的加载失败。
    ///
    /// 调用方式：
    ///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod JieNiYiMiao.EditorTools.ProjectSetup.SetupScene
    /// </summary>
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("借你一秒/1. 生成入口场景并写入 Build Settings")]
        public static void SetupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrapObject = new GameObject("GameBootstrap");
            bootstrapObject.AddComponent<GameBootstrap>();

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            bool saved = EditorSceneManager.SaveScene(scene, MainScenePath);
            if (!saved)
            {
                Debug.LogError("[借你一秒] 场景保存失败：" + MainScenePath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteBuildSettings();
            Debug.Log("[借你一秒] 入口场景已生成：" + MainScenePath + "，并已写入 Build Settings。");
        }

        /// <summary>把入口场景放到 Build Settings 第一位（游戏启动即进入）。</summary>
        public static void WriteBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene(MainScenePath, true));

            var existing = EditorBuildSettings.scenes;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i].path == MainScenePath)
                {
                    continue;
                }

                // 保留工程里原有的场景（例如模板带的 SampleScene），但排在入口场景之后
                scenes.Add(new EditorBuildSettingsScene(existing[i].path, existing[i].enabled));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[借你一秒] Build Settings 场景列表已更新，共 " + scenes.Count + " 个场景。");
        }
    }
}
