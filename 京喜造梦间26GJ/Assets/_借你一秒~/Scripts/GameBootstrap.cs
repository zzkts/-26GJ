using UnityEngine;

namespace JieNiYiMiao
{
    /// <summary>
    /// 场景唯一入口。整个游戏只有这一个挂在场景里的脚本：
    /// 其余画布、按钮、文字、人物造型全部由代码在运行时生成。
    ///
    /// 这样做的原因（手册：不要要求团队手动创建大量 Inspector 引用）：
    /// 场景文件里只需一个空物体，换机器、重新导入资源都不会丢引用。
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            EnsureCamera();
            UiFactory.EnsureEventSystem();
            EnsureGameManager();
        }

        /// <summary>保证有主摄像机：正交、纯色背景，UI 画布走屏幕空间覆盖。</summary>
        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
                go.tag = "MainCamera";
                camera = go.GetComponent<Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
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
