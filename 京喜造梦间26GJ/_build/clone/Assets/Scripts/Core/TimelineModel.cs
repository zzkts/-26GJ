using System;
using System.Collections.Generic;

namespace CuoGuo
{
    /// <summary>
    /// 二维坐标载体（0—1 无关，直接用像素/画布单位）。
    /// Core 目录刻意不引用 UnityEngine，位置、颜色都用自定义结构，
    /// 这样"连续时间轴"这套核心规则可以脱离引擎编译并跑断言。
    /// </summary>
    [Serializable]
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Vec2 Zero
        {
            get { return new Vec2(0f, 0f); }
        }
    }

    /// <summary>颜色载体（0—1 的 rgba）。</summary>
    [Serializable]
    public struct Rgba
    {
        public float R;
        public float G;
        public float B;
        public float A;

        public Rgba(float r, float g, float b, float a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Rgba White
        {
            get { return new Rgba(1f, 1f, 1f, 1f); }
        }
    }

    /// <summary>某个时间点上，一个对象应该呈现的完整姿态。视图层拿到它直接套用。</summary>
    public struct TrackSample
    {
        /// <summary>轨道是否有效（没有任何关键帧时为 false，视图层应跳过）。</summary>
        public bool Valid;

        public Vec2 Position;
        public float Rotation;
        public float Scale;
        public Rgba Color;
    }

    /// <summary>
    /// 一个动画关键帧。它是**程序内部的控制点**，不是玩家可见的时间档位：
    /// 玩家可以把滑块停在 0—1 之间的任意值，对象属性在相邻关键帧之间连续插值。
    /// </summary>
    [Serializable]
    public class KeyFrame
    {
        /// <summary>0—1 的时间位置。1 = 现在，0 = 这段记忆最早的过去。</summary>
        public float Time;

        /// <summary>相对所属章节根节点的锚点位置（画布单位，原点在屏幕中心）。</summary>
        public Vec2 Position;

        /// <summary>Z 轴旋转（度）。</summary>
        public float Rotation;

        /// <summary>整体缩放。</summary>
        public float Scale;

        /// <summary>颜色 + 透明度。会与对象自身基色相乘，用来做淡入淡出与失色。</summary>
        public Rgba Color;

        public static KeyFrame At(float time, Vec2 position)
        {
            return new KeyFrame { Time = time, Position = position, Rotation = 0f, Scale = 1f, Color = Rgba.White };
        }

        public KeyFrame WithAlpha(float alpha)
        {
            Color = new Rgba(Color.R, Color.G, Color.B, alpha);
            return this;
        }

        public KeyFrame WithColor(Rgba color)
        {
            Color = color;
            return this;
        }

        public KeyFrame WithRotation(float degrees)
        {
            Rotation = degrees;
            return this;
        }

        public KeyFrame WithScale(float scale)
        {
            Scale = scale;
            return this;
        }
    }

    /// <summary>
    /// 一条时间轨迹：把一个场景对象（人物、道具、环境层）绑到时间轴上。
    /// 视图层按 Name 找到同名对象，每帧把 Sample 的结果套上去。
    /// </summary>
    public class TimelineTrack
    {
        /// <summary>与场景对象同名的绑定键。</summary>
        public string Name;

        /// <summary>关键帧列表（构造时可以乱序，采样前会自动按时间升序排好）。</summary>
        public List<KeyFrame> Keys = new List<KeyFrame>();

        /// <summary>是否对插值做缓入缓出。位置默认线性（保证跟手），透明度这类建议开。</summary>
        public bool Ease;

        public TimelineTrack(string name)
        {
            Name = name;
        }

        public TimelineTrack Add(KeyFrame key)
        {
            Keys.Add(key);
            return this;
        }

        /// <summary>按时间升序整理，去重（同一时间点保留后写入的那一帧）。</summary>
        public TimelineTrack Sorted()
        {
            Keys.Sort(delegate (KeyFrame a, KeyFrame b) { return a.Time.CompareTo(b.Time); });

            for (int i = Keys.Count - 1; i > 0; i--)
            {
                if (Math.Abs(Keys[i].Time - Keys[i - 1].Time) < 0.0001f)
                {
                    Keys.RemoveAt(i - 1);
                }
            }

            return this;
        }

        /// <summary>
        /// 采样：找到夹住 position 的相邻两帧，算出归一化比例后逐通道插值。
        /// position 在两个关键帧之间时**不吸附到最近关键帧**，这是本游戏最核心的一条规则。
        /// </summary>
        public TrackSample Sample(float position)
        {
            var sample = new TrackSample { Valid = false, Scale = 1f, Color = Rgba.White };
            if (Keys.Count == 0)
            {
                return sample;
            }

            float p = Clamp01(position);

            if (p <= Keys[0].Time)
            {
                return FromKey(Keys[0]);
            }

            if (p >= Keys[Keys.Count - 1].Time)
            {
                return FromKey(Keys[Keys.Count - 1]);
            }

            for (int i = 0; i < Keys.Count - 1; i++)
            {
                KeyFrame a = Keys[i];
                KeyFrame b = Keys[i + 1];
                if (p < a.Time || p > b.Time)
                {
                    continue;
                }

                float span = b.Time - a.Time;
                float t = span <= 0.000001f ? 0f : (p - a.Time) / span;
                if (Ease)
                {
                    t = t * t * (3f - 2f * t);
                }

                sample.Valid = true;
                sample.Position = new Vec2(Lerp(a.Position.X, b.Position.X, t), Lerp(a.Position.Y, b.Position.Y, t));
                sample.Rotation = Lerp(a.Rotation, b.Rotation, t);
                sample.Scale = Lerp(a.Scale, b.Scale, t);
                sample.Color = new Rgba(
                    Lerp(a.Color.R, b.Color.R, t),
                    Lerp(a.Color.G, b.Color.G, t),
                    Lerp(a.Color.B, b.Color.B, t),
                    Lerp(a.Color.A, b.Color.A, t));
                return sample;
            }

            return FromKey(Keys[Keys.Count - 1]);
        }

        private static TrackSample FromKey(KeyFrame key)
        {
            return new TrackSample
            {
                Valid = true,
                Position = key.Position,
                Rotation = key.Rotation,
                Scale = key.Scale,
                Color = key.Color
            };
        }

        internal static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        internal static float Clamp01(float v)
        {
            if (v < 0f)
            {
                return 0f;
            }

            return v > 1f ? 1f : v;
        }

        // ------------------------------------------------------------ 构造语法糖

        /// <summary>只关心位置与透明度的帧。</summary>
        public static KeyFrame Key(float time, float x, float y, float alpha)
        {
            return KeyFrame.At(time, new Vec2(x, y)).WithAlpha(alpha);
        }

        /// <summary>只关心透明度的帧（道具在原地淡入淡出）。</summary>
        public static KeyFrame Fade(float time, float alpha)
        {
            return new KeyFrame { Time = time, Position = Vec2.Zero, Rotation = 0f, Scale = 1f, Color = new Rgba(1f, 1f, 1f, alpha) };
        }
    }

    /// <summary>
    /// 时间线索：当 TimelinePosition 从阈值以上**第一次**进入阈值以下时触发一次。
    /// 触发后记入本章已发现线索，来回拖动不会重复弹字；只要当前时间仍在阈值以内，文字就保持可读。
    /// </summary>
    public class TimelineCue
    {
        public string Id;
        public float Threshold;

        /// <summary>线索正文（短句，单条不超过 50 字）。</summary>
        public string Text;

        public TimelineCue(string id, float threshold, string text)
        {
            Id = id;
            Threshold = threshold;
            Text = text;
        }
    }

    /// <summary>
    /// 线索看板：管理一堆 TimelineCue 的触发状态。
    /// 之所以单独成类，是因为"不重复触发""来回拖动只在第一次收费/弹字"这些规则必须能被独立断言。
    /// </summary>
    public class CueBoard
    {
        private readonly List<TimelineCue> _cues = new List<TimelineCue>();
        private readonly List<bool> _fired = new List<bool>();

        public CueBoard(List<TimelineCue> cues)
        {
            if (cues != null)
            {
                for (int i = 0; i < cues.Count; i++)
                {
                    _cues.Add(cues[i]);
                    _fired.Add(false);
                }
            }

            // 阈值从大到小：先遇到浅处的线索，深处的后触发
            for (int i = 0; i < _cues.Count - 1; i++)
            {
                for (int j = i + 1; j < _cues.Count; j++)
                {
                    if (_cues[j].Threshold > _cues[i].Threshold)
                    {
                        TimelineCue tmp = _cues[i];
                        _cues[i] = _cues[j];
                        _cues[j] = tmp;
                        bool f = _fired[i];
                        _fired[i] = _fired[j];
                        _fired[j] = f;
                    }
                }
            }
        }

        public int Count
        {
            get { return _cues.Count; }
        }

        public TimelineCue Get(int index)
        {
            return _cues[index];
        }

        public bool IsFired(int index)
        {
            return index >= 0 && index < _fired.Count && _fired[index];
        }

        public bool IsFired(string id)
        {
            return IndexOf(id) >= 0 && _fired[IndexOf(id)];
        }

        public int FiredCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _fired.Count; i++)
                {
                    if (_fired[i])
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_cues[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 按当前时间位置推进。返回**本次新触发**的线索（一次大幅拖动可能跨过好几条，
        /// 这时返回最深的那个，因为它才是要显示给玩家看的），没有新触发则返回 null。
        /// </summary>
        public TimelineCue Update(float position)
        {
            TimelineCue deepest = null;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (_fired[i] || position >= _cues[i].Threshold)
                {
                    continue;
                }

                _fired[i] = true;
                deepest = _cues[i];
            }

            return deepest;
        }

        /// <summary>
        /// 当前应该显示在屏幕上的线索文字：在所有已触发、且当前时间仍在阈值以内的线索里，
        /// 取阈值最小的那条（最深处的真相）。玩家向右拖回现在时文字自然消失。
        /// </summary>
        public TimelineCue ActiveCue(float position)
        {
            TimelineCue best = null;
            for (int i = 0; i < _cues.Count; i++)
            {
                if (!_fired[i] || position >= _cues[i].Threshold)
                {
                    continue;
                }

                if (best == null || _cues[i].Threshold < best.Threshold)
                {
                    best = _cues[i];
                }
            }

            return best;
        }

        /// <summary>该线索现在是否应该显示。</summary>
        public bool IsActive(int index, float position)
        {
            return index >= 0 && index < _cues.Count && _fired[index] && position < _cues[index].Threshold;
        }

        public void Reset()
        {
            for (int i = 0; i < _fired.Count; i++)
            {
                _fired[i] = false;
            }
        }
    }

    /// <summary>
    /// 一章的时间轴数据：一组轨迹 + 一组线索 + 本章的借时权重。
    /// 视图层只负责按名字把轨迹绑到对象上，不含任何动画逻辑。
    /// </summary>
    public class ChapterTimeline
    {
        public int Index;
        public string ChapterLabel;
        public string Title;
        public float DebtWeight;

        public List<TimelineTrack> Tracks = new List<TimelineTrack>();
        public List<TimelineCue> Cues = new List<TimelineCue>();

        /// <summary>本章最深处那条线索的 Id。它被触发后，"继续"按钮就会出现（防止玩家卡住）。</summary>
        public string FinalCueId;

        /// <summary>线索在表里的下标，找不到返回 -1。用于按 Id 查最终线索是否已被看见。</summary>
        public int IndexOfCue(string id)
        {
            for (int i = 0; i < Cues.Count; i++)
            {
                if (Cues[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        public TimelineTrack FindTrack(string name)
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i].Name == name)
                {
                    return Tracks[i];
                }
            }

            return null;
        }

        /// <summary>把所有轨迹整理好（排序 + 去重）。构造完数据后调用一次。</summary>
        public ChapterTimeline Bake()
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                Tracks[i].Sorted();
            }

            return this;
        }
    }
}
