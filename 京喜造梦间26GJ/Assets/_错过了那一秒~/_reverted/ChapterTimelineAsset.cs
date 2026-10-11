using System;
using System.Collections.Generic;
using UnityEngine;

namespace CuoGuo
{
    /// <summary>
    /// 一章时间轴的**可编辑版本**：让关键帧变成 Unity 资产，在 Inspector 里直接改。
    ///
    /// 为什么要有它：关键帧原先写在 `StoryData.cs` 的代码里，美术看不到"母亲在这几个时刻分别在哪"，
    /// 想挪一个走位只能改代码数字再重打包。现在把代码里的默认值**导出成资产**，
    /// 美术在 Inspector 里改这几个数就能调动画，改完直接 Play 看效果。
    ///
    /// 与代码默认值的关系：**资产是覆盖层**。
    /// - `Assets/Resources/Timelines/Chapter1..3.asset` 存在 → 用资产里的数据；
    /// - 不存在 → 用 `StoryData.cs` 里的默认值。
    ///
    /// 所以资产可以随时删掉，游戏照样能跑（和美术贴图那套"缺了就退回占位"是同一个原则）。
    /// Core 目录不引用 UnityEngine，资产到内核模型的转换放在这一层做。
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterTimeline", menuName = "错过了那一秒/章节时间轴")]
    public class ChapterTimelineAsset : ScriptableObject
    {
        [Serializable]
        public class KeyEntry
        {
            [Tooltip("0—1。1 = 现在，0 = 这段记忆最早的过去")]
            public float time = 1f;

            [Tooltip("相对章节根节点的画布坐标（1920×1080，原点在屏幕中心）")]
            public Vector2 position;

            [Tooltip("Z 轴旋转（度）")]
            public float rotation;

            [Tooltip("整体缩放")]
            public float scale = 1f;

            [Tooltip("透明度。0 = 这一刻还看不见")]
            public float alpha = 1f;

            [Tooltip("颜色染色。白色 = 不变色")]
            public Color color = Color.white;
        }

        [Serializable]
        public class TrackEntry
        {
            [Tooltip("必须与场景里的对象同名")]
            public string name;

            [Tooltip("开启后关键帧之间做缓入缓出（位置建议关闭，保证跟手）")]
            public bool ease;

            public List<KeyEntry> keys = new List<KeyEntry>();
        }

        [Serializable]
        public class CueEntry
        {
            [Tooltip("线索编号，只用来说明用途")]
            public string id;

            [Tooltip("滑块越过这条线时触发一次（往过去越深，数值越小）")]
            public float threshold;

            [TextArea(1, 3)]
            public string text;
        }

        [Header("章节信息")]
        public int chapterIndex;
        public string chapterLabel;
        public string title;
        public float debtWeight = 30f;

        [Header("时间轨迹（对象名必须和场景里一致）")]
        public List<TrackEntry> tracks = new List<TrackEntry>();

        [Header("线索")]
        public List<CueEntry> cues = new List<CueEntry>();

        [Tooltip("本章最深处那条线索的 id：它被看见后『继续』按钮就会出现")]
        public string finalCueId;

        /// <summary>转成内核用的时间轴。</summary>
        public ChapterTimeline ToTimeline()
        {
            var timeline = new ChapterTimeline
            {
                Index = chapterIndex,
                ChapterLabel = chapterLabel,
                Title = title,
                DebtWeight = debtWeight,
                FinalCueId = finalCueId
            };

            for (int i = 0; i < tracks.Count; i++)
            {
                TrackEntry entry = tracks[i];
                var track = new TimelineTrack(entry.name) { Ease = entry.ease };

                for (int k = 0; k < entry.keys.Count; k++)
                {
                    KeyEntry key = entry.keys[k];
                    track.Add(new KeyFrame
                    {
                        Time = key.time,
                        Position = new Vec2(key.position.x, key.position.y),
                        Rotation = key.rotation,
                        Scale = key.scale,
                        Color = new Rgba(key.color.r, key.color.g, key.color.b, key.alpha)
                    });
                }

                timeline.Tracks.Add(track);
            }

            for (int i = 0; i < cues.Count; i++)
            {
                timeline.Cues.Add(new TimelineCue(cues[i].id, cues[i].threshold, cues[i].text));
            }

            timeline.Bake();
            return timeline;
        }

        /// <summary>把代码里的默认值导出成资产，供美术在 Inspector 里编辑。</summary>
        public static ChapterTimelineAsset FromChapter(ChapterData data)
        {
            var asset = CreateInstance<ChapterTimelineAsset>();
            asset.chapterIndex = data.Index;
            asset.chapterLabel = data.ChapterLabel;
            asset.title = data.Title;
            asset.debtWeight = data.DebtWeight;
            asset.finalCueId = data.Timeline.FinalCueId;

            for (int i = 0; i < data.Timeline.Tracks.Count; i++)
            {
                TimelineTrack source = data.Timeline.Tracks[i];
                var entry = new TrackEntry { name = source.Name, ease = source.Ease };

                for (int k = 0; k < source.Keys.Count; k++)
                {
                    KeyFrame key = source.Keys[k];
                    entry.keys.Add(new KeyEntry
                    {
                        time = key.Time,
                        position = new Vector2(key.Position.X, key.Position.Y),
                        rotation = key.Rotation,
                        scale = key.Scale,
                        alpha = key.Color.A,
                        color = new Color(key.Color.R, key.Color.G, key.Color.B, 1f)
                    });
                }

                asset.tracks.Add(entry);
            }

            for (int i = 0; i < data.Timeline.Cues.Count; i++)
            {
                TimelineCue cue = data.Timeline.Cues[i];
                asset.cues.Add(new CueEntry { id = cue.Id, threshold = cue.Threshold, text = cue.Text });
            }

            return asset;
        }

        public const string ResourceFolder = "Timelines";

        public static string ResourceName(int chapterIndex)
        {
            return ResourceFolder + "/Chapter" + (chapterIndex + 1);
        }
    }
}
