using UnityEngine;

namespace CuoGuo
{
    /// <summary>
    /// 场景唯一入口。整个游戏只有这一个挂在场景里的物体：
    /// 画布、章节舞台、人物、道具、按钮、文字全部在运行时由代码生成。
    ///
    /// 这样场景文件里只有一个空物体，换机器、重新导入资源都不会丢引用，
    /// 也不会出现"美术挪了一下预制体导致脚本引用断裂"这类问题。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureCamera();
            UiFactory.EnsureEventSystem();
            EnsureGameManager();

            // 打包版本带 -cuoguo-selfcheck 启动时，跑一遍无人值守的运行时自检后退出。
            // 它走的是和玩家操作完全相同的代码路径，用于在没有人工点按的环境里验证真实运行状态。
            if (SelfCheckRunner.Requested)
            {
                gameObject.AddComponent<SelfCheckRunner>();
            }
        }

        /// <summary>保证有主摄像机：正交、纯色背景。</summary>
        private static void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            camera.transform.position = new Vector3(0f, 0f, -10f);

            if (camera.GetComponent<AudioListener>() == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
        }

        private static void EnsureGameManager()
        {
            if (FindAnyObjectByType<GameManager>() != null)
            {
                return;
            }

            var go = new GameObject("GameManager", typeof(GameManager));
            DontDestroyOnLoad(go);
        }
    }
}
