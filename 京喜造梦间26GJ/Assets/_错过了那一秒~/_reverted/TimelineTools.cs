using System.Collections.Generic;
using System.IO;
using CuoGuo;
using UnityEditor;
using UnityEngine;

namespace CuoGuo.EditorTools
{
    /// <summary>
    /// 给美术的两个时间轴工具。
    ///
    /// 1）把代码里的关键帧导出成 Unity 资产（ScriptableObject），之后就能在 Inspector 里改数；
    /// 2）在编辑器里拖一条时间轴，**不进 Play 模式**就能看到画面在 0—1 之间怎么变，用来定走位。
    ///
    /// 预览只改 Transform（位置/旋转/缩放），不改颜色，也不会保存；关窗或点"还原"会自动恢复。
    /// </summary>
    public static class TimelineTools
    {
        private const string TimelineFolder = "Assets/Resources/" + ChapterTimelineAsset.ResourceFolder;

        [MenuItem("错过了那一秒/6. 导出时间轴为可编辑资产（Inspector 里改关键帧）")]
        public static void ExportTimelines()
        {
            Directory.CreateDirectory(TimelineFolder);

            int created = 0;
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                string path = TimelineFolder + "/Chapter" + (i + 1) + ".asset";
                if (File.Exists(path))
                {
                    Debug.Log("[时间轴] 已存在，跳过以免覆盖你们的修改：" + path);
                    continue;
                }

                ChapterTimelineAsset asset = ChapterTimelineAsset.FromChapter(StoryBook.Get(i));
                AssetDatabase.CreateAsset(asset, path);
                created++;
                Debug.Log("[时间轴] 已导出：" + path + "（" + asset.tracks.Count + " 条轨迹、"
                          + asset.cues.Count + " 条线索）");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (created == 0)
            {
                Debug.Log("[时间轴] 三份资产都已存在，没有新建。"
                          + "想从代码默认值重新生成，先把要重建的那份删掉再跑一次。");
            }
            else
            {
                Debug.Log("[时间轴] 导出完成。运行时如果检测到这些资产就会用它们覆盖代码默认值；"
                          + "删掉资产即可恢复默认。");
            }
        }

        [MenuItem("错过了那一秒/7. 时间轴预览（拖时间轴看画面）")]
        public static void OpenScrubber()
        {
            var window = EditorWindow.GetWindow<TimelineScrubWindow>("时间轴预览");
            window.minSize = new Vector2(320f, 150f);
            window.Show();
        }
    }

    /// <summary>时间轴预览窗口：拖滑块 → 场景里的对象按关键帧连续变化。</summary>
    public class TimelineScrubWindow : EditorWindow
    {
        private int _chapter;
        private float _position = 1f;
        private string _warning;

        private readonly Dictionary<Transform, Pose> _original = new Dictionary<Transform, Pose>();
        private bool _applied;

        private struct Pose
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "拖动下面的滑块，场景里的人物与道具会按该章的关键帧连续变化。\n" +
                "这是预览：只改位置/旋转/缩放，不改颜色，也不保存；关窗会自动还原。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _chapter = EditorGUILayout.IntSlider("章节", _chapter + 1, 1, StoryBook.ChapterCount) - 1;
            _position = EditorGUILayout.Slider("时间位置（1 = 现在，0 = 最早）", _position, 0f, 1f);
            bool changed = EditorGUI.EndChangeCheck();

            if (changed)
            {
                Apply();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("回到现在（1.0）"))
            {
                _position = 1f;
                Apply();
            }

            if (GUILayout.Button("还原并停止预览"))
            {
                Restore();
            }

            ChapterData data = StoryBook.Get(_chapter);
            EditorGUILayout.LabelField("正在预览", data.ChapterLabel + " · " + data.Title);
            EditorGUILayout.LabelField("轨迹数", data.Timeline.Tracks.Count.ToString());

            TimelineCue active = null;
            for (int i = 0; i < data.Timeline.Cues.Count; i++)
            {
                TimelineCue cue = data.Timeline.Cues[i];
                if (_position < cue.Threshold && (active == null || cue.Threshold < active.Threshold))
                {
                    active = cue;
                }
            }

            EditorGUILayout.LabelField("此刻会显示的线索", active == null ? "（无）" : active.Text);

            if (!string.IsNullOrEmpty(_warning))
            {
                EditorGUILayout.HelpBox(_warning, MessageType.Warning);
            }
        }

        private void OnDestroy()
        {
            Restore();
        }

        private void Apply()
        {
            Transform root = FindChapterRoot(_chapter);
            if (root == null)
            {
                _warning = "场景里没有找到「" + "Chapter_" + (_chapter + 1) + "_"
                           + StoryBook.Get(_chapter).BackdropStyle + "」。"
                           + "先跑一次菜单「5. 烘焙成可编辑场景」，或者进 Play 模式让运行时生成。";
                return;
            }

            _warning = null;

            ChapterData data = StoryBook.Get(_chapter);
            for (int i = 0; i < data.Timeline.Tracks.Count; i++)
            {
                TimelineTrack track = data.Timeline.Tracks[i];
                Transform target = UiFactory.FindDeepChild(root, track.Name);
                if (target == null)
                {
                    continue;
                }

                if (!_original.ContainsKey(target))
                {
                    _original[target] = new Pose
                    {
                        Position = target.localPosition,
                        Rotation = target.localRotation,
                        Scale = target.localScale
                    };
                }

                TrackSample sample = track.Sample(_position);
                if (!sample.Valid)
                {
                    continue;
                }

                var rect = target as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(sample.Position.X, sample.Position.Y);
                }
                else
                {
                    target.localPosition = new Vector3(sample.Position.X, sample.Position.Y, 0f);
                }

                target.localRotation = Quaternion.Euler(0f, 0f, sample.Rotation);
                float scale = sample.Scale <= 0.0001f ? 1f : sample.Scale;
                target.localScale = new Vector3(scale, scale, 1f);
            }

            _applied = true;
            SceneView.RepaintAll();
        }

        private void Restore()
        {
            if (!_applied)
            {
                return;
            }

            foreach (var pair in _original)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                var rect = pair.Key as RectTransform;
                if (rect != null)
                {
                    rect.anchoredPosition = new Vector2(pair.Value.Position.x, pair.Value.Position.y);
                }
                else
                {
                    pair.Key.localPosition = pair.Value.Position;
                }

                pair.Key.localRotation = pair.Value.Rotation;
                pair.Key.localScale = pair.Value.Scale;
            }

            _original.Clear();
            _applied = false;
            SceneView.RepaintAll();
        }

        private static Transform FindChapterRoot(int chapterIndex)
        {
            for (int i = 0; i < StoryBook.ChapterCount; i++)
            {
                if (i != chapterIndex)
                {
                    continue;
                }

                string name = "Chapter_" + (i + 1) + "_" + StoryBook.Get(i).BackdropStyle;
                var managers = Object.FindObjectsByType<GameManager>(FindObjectsSortMode.None);
                for (int m = 0; m < managers.Length; m++)
                {
                    Transform stage = managers[m].transform.Find("Canvas/Stage");
                    if (stage == null)
                    {
                        continue;
                    }

                    Transform found = stage.Find(name);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }
    }
}
