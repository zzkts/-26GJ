using CuoGuo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo.EditorTools
{
    /// <summary>
    /// 把程序化生成的内容**烘焙成场景里真实存在的对象**，让美术可以在 Unity 里直接配置。
    ///
    /// 烘焙之后：
    /// - 背景、木船、人物、道具、三处滑块都是 Hierarchy 里的真实节点；
    /// - Sprite 是**序列化引用**，可以在 Inspector 里直接换，Scene 视图里立刻能看到；
    /// - 位置、缩放、层级随便拖，运行时**不会**被代码覆盖（生成走的是"缺了才建"）。
    ///
    /// 运行时如果发现场景里没有这些对象（例如有人清空了场景），会自动重新生成，
    /// 所以烘焙不是必须步骤，只是为了让美术能可视化地调。
    ///
    /// 调用方式：
    ///   Unity.exe -batchmode -quit -projectPath &lt;工程&gt; -executeMethod CuoGuo.EditorTools.SceneBaker.BakeMainScene
    /// </summary>
    public static class SceneBaker
    {
        [MenuItem("错过了那一秒/5. 烘焙成可编辑场景（美术可直接在 Unity 里调）")]
        public static void BakeMainScene()
        {
            Bake();
            EditorApplication.Exit(0);
        }

        /// <summary>只烘焙、不退出进程——供打包流程调用（打包还要接着跑）。</summary>
        public static void Bake()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 相机 ----
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.AddComponent<AudioListener>();

            // ---- 入口 ----
            var bootstrapObject = new GameObject("GameBootstrap");
            bootstrapObject.AddComponent<GameBootstrap>();

            // ---- 内容 ----
            // GameManager.Awake 在编辑模式下不会被调用，所以这里手动让它生成一次内容。
            var managerObject = new GameObject("GameManager");
            var manager = managerObject.AddComponent<GameManager>();
            manager.BakeForEditing();

            StripRuntimeSprites();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            bool saved = EditorSceneManager.SaveScene(scene, ProjectSetup.MainScenePath);
            if (!saved)
            {
                Debug.LogError("[错过了那一秒] 场景保存失败：" + ProjectSetup.MainScenePath);
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ProjectSetup.WriteBuildSettings();

            Debug.Log("[错过了那一秒] 已烘焙成可编辑场景：" + ProjectSetup.MainScenePath
                      + "。在 Hierarchy 里展开 GameManager/Canvas/Stage，"
                      + "三章舞台分别是 Chapter_1_dusk（已打开）、Chapter_2_dawn、Chapter_3_night（默认关闭，手动打开即可）。");
        }

        /// <summary>
        /// 把场景里**不是资产**的 Sprite 引用清空。
        ///
        /// 程序化造型（色块、剪影）用的是 `UiFactory.Disc()`——一张运行时 `Sprite.Create` 出来的白图。
        /// 它带着 `HideAndDontSave`，不是任何资产，却被上千个 Image 引用。
        /// 这种"指向运行期对象的引用"写进场景会让序列化文件出问题
        /// （实测：打包后播放器报 `level0 is corrupted / Position out of bounds` 直接崩溃）。
        ///
        /// 而这些 Image 本来只需要"画一个纯白方块"——**Sprite 置空时 Unity 渲染的就是纯白方块**，
        /// 配合各自的 color 效果完全一致。所以烘焙时一律置空，只留下真正的美术资产引用。
        /// </summary>
        private static void StripRuntimeSprites()
        {
            int stripped = 0;
            var images = Object.FindObjectsByType<Image>(FindObjectsSortMode.None);
            for (int i = 0; i < images.Length; i++)
            {
                Sprite sprite = images[i].sprite;
                if (sprite == null || AssetDatabase.Contains(sprite))
                {
                    continue;
                }

                images[i].sprite = null;
                stripped++;
            }

            Debug.Log("[错过了那一秒] 已清掉 " + stripped + " 处运行期 Sprite 引用"
                      + "（它们只需要画纯白方块，置空后渲染效果不变），只保留美术资产引用。");
        }
    }
}
