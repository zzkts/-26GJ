using System.Collections.Generic;
using UnityEngine;

namespace CuoGuo
{
    /// <summary>
    /// 把一章的时间轴轨迹绑到场景里同名的对象上，并按当前 TimelinePosition 连续更新它们。
    ///
    /// 它是"连续时间轴"在视图层的唯一落点：
    /// 每帧（其实只在滑块真被拖动时）从 Core 拿一组 TrackSample，
    /// 位置、旋转、缩放、颜色、透明度一次性套上去，中间没有任何离散状态切换。
    /// </summary>
    public class TimelineBinder : MonoBehaviour
    {
        private ChapterTimeline _timeline;
        private readonly Dictionary<string, RectTransform> _targets = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Tintable> _tints = new Dictionary<string, Tintable>();
        private readonly List<TimelineTrack> _tracks = new List<TimelineTrack>();

        private float _position = 1f;

        public float Position
        {
            get { return _position; }
        }

        public int BoundTrackCount
        {
            get { return _tracks.Count; }
        }

        /// <summary>把章节根节点下所有对象登记进来看，只有名字能对上轨迹的才会被驱动。</summary>
        public void Bind(ChapterTimeline timeline, Transform root)
        {
            _timeline = timeline;
            _targets.Clear();
            _tints.Clear();
            _tracks.Clear();

            if (timeline == null || root == null)
            {
                return;
            }

            for (int i = 0; i < timeline.Tracks.Count; i++)
            {
                TimelineTrack track = timeline.Tracks[i];
                Transform found = UiFactory.FindDeepChild(root, track.Name);
                if (found == null)
                {
                    continue; // 美术还没做的对象：跳过，不影响其它轨迹
                }

                var rect = found as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                _targets[track.Name] = rect;
                _tracks.Add(track);

                var tint = found.GetComponent<Tintable>();
                if (tint != null)
                {
                    _tints[track.Name] = tint;
                }
            }

            Apply(_position);
        }

        /// <summary>按连续时间位置刷新场景。position 可以是 0—1 之间的任意值。</summary>
        public void Apply(float position)
        {
            _position = Mathf.Clamp01(position);

            for (int i = 0; i < _tracks.Count; i++)
            {
                TimelineTrack track = _tracks[i];

                RectTransform target;
                if (!_targets.TryGetValue(track.Name, out target) || target == null)
                {
                    continue;
                }

                TrackSample sample = track.Sample(_position);
                if (!sample.Valid)
                {
                    continue;
                }

                target.anchoredPosition = new Vector2(sample.Position.X, sample.Position.Y);
                target.localRotation = Quaternion.Euler(0f, 0f, sample.Rotation);
                float scale = sample.Scale <= 0.0001f ? 1f : sample.Scale;
                target.localScale = new Vector3(scale, scale, 1f);

                Tintable tint;
                if (_tints.TryGetValue(track.Name, out tint) && tint != null)
                {
                    tint.SetTimelineTint(sample.Color);
                }
            }
        }

        /// <summary>给某一章的所有物件送一次"记忆褪色"。</summary>
        public void ApplyDecay(float decay01)
        {
            foreach (var pair in _tints)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetDecay(decay01);
                }
            }
        }
    }
}
