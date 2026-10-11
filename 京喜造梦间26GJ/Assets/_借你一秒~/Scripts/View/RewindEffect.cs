using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>
    /// 倒流可视化。
    /// 手册要求"必须让玩家明显看见倒流发生"：这里用四层反馈叠加——
    /// 紫青色闪屏、全屏刻度尺式的时间轴回退、人物残影横向淡出、状态文字。
    /// 全部由代码生成，不依赖任何贴图或粒子资源。
    /// </summary>
    public static class RewindEffect
    {
        public static IEnumerator Play(Transform stageRoot, Transform overlayRoot, RectTransform target, string caption)
        {
            if (overlayRoot == null)
            {
                yield break;
            }

            var flash = PlaceholderArt.Block(overlayRoot, "RewindFlash", new Color(0.45f, 0.32f, 0.85f, 0f), new Vector2(4000f, 2400f), Vector2.zero);
            var tint = PlaceholderArt.Block(overlayRoot, "RewindTint", new Color(0.35f, 0.85f, 0.92f, 0f), new Vector2(4000f, 2400f), Vector2.zero);
            var captionText = UiFactory.MakeText(overlayRoot, "RewindCaption", caption, 34, new Color(0.94f, 0.92f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-720f, 240f), new Vector2(720f, 330f));
            captionText.text = string.Empty;

            // 1) 闪屏：紫 → 青
            const float flashTime = 0.30f;
            float t = 0f;
            while (t < flashTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / flashTime);
                flash.color = new Color(0.45f, 0.32f, 0.85f, Mathf.Sin(k * Mathf.PI) * 0.75f);
                tint.color = new Color(0.35f, 0.85f, 0.92f, Mathf.Sin(k * Mathf.PI) * 0.35f);
                if (target != null)
                {
                    target.localScale = Vector3.one * Mathf.Lerp(1.06f, 1f, k);
                }

                yield return null;
            }

            if (target != null)
            {
                target.localScale = Vector3.one;
            }

            // 2) 时间轴回退：尺子 + 向左回退的刻度
            var timeline = new GameObject("RewindTimeline", typeof(RectTransform));
            timeline.transform.SetParent(overlayRoot, false);
            var tlRt = timeline.GetComponent<RectTransform>();
            tlRt.anchorMin = new Vector2(0.5f, 0.5f);
            tlRt.anchorMax = new Vector2(0.5f, 0.5f);
            tlRt.pivot = new Vector2(0.5f, 0.5f);
            tlRt.sizeDelta = new Vector2(1200f, 60f);
            tlRt.anchoredPosition = new Vector2(0f, -180f);

            PlaceholderArt.Block(timeline.transform, "Rule", new Color(0.92f, 0.92f, 0.98f, 0.65f), new Vector2(1200f, 4f), Vector2.zero);
            var ticks = new RectTransform[24];
            for (int i = 0; i < ticks.Length; i++)
            {
                var tick = PlaceholderArt.Block(timeline.transform, "Tick" + i, new Color(0.94f, 0.94f, 1f, 0.85f), new Vector2(3f, i % 4 == 0 ? 34f : 20f), new Vector2(-600f + i * 52f, 0f));
                ticks[i] = tick.rectTransform;
            }

            var head = PlaceholderArt.Block(timeline.transform, "Head", new Color(1f, 0.86f, 0.45f, 0.95f), new Vector2(6f, 70f), new Vector2(600f, 0f));

            captionText.text = caption;

            // 3) 残影：三道人影向右漂出并淡出（视觉上就是"退回去"）
            const float rewindTime = 0.75f;
            t = 0f;
            while (t < rewindTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / rewindTime);

                for (int i = 0; i < ticks.Length; i++)
                {
                    float x = -600f + i * 52f + k * 104f;
                    if (x > 600f)
                    {
                        x -= 1200f;
                    }

                    ticks[i].anchoredPosition = new Vector2(x, 0f);
                }

                head.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(600f, -600f, k), 0f);
                head.color = new Color(1f, 0.86f, 0.45f, 0.95f * (1f - k * 0.4f));

                yield return null;
            }

            if (target != null)
            {
                // 生成三份静态残影副本（只复制视觉，不复制交互组件）
                for (int i = 0; i < 3; i++)
                {
                    var ghost = new GameObject("RewindGhost" + i, typeof(RectTransform));
                    ghost.transform.SetParent(stageRoot, false);
                    var grt = ghost.GetComponent<RectTransform>();
                    grt.anchorMin = new Vector2(0.5f, 0.5f);
                    grt.anchorMax = new Vector2(0.5f, 0.5f);
                    grt.pivot = new Vector2(0.5f, 0.5f);
                    grt.sizeDelta = target.sizeDelta;
                    grt.anchoredPosition = target.anchoredPosition;
                    grt.localScale = target.localScale;

                    var images = target.GetComponentsInChildren<Image>(true);
                    for (int k = 0; k < images.Length; k++)
                    {
                        var src = images[k];
                        var copy = PlaceholderArt.Block(ghost.transform, src.name, UiFactory.WithAlpha(new Color(0.62f, 0.48f, 0.95f), 0.55f), src.rectTransform.sizeDelta, Vector2.zero);
                        var srcRt = src.rectTransform;
                        // 用世界位置对齐，保证残影轮廓和本体一致
                        copy.rectTransform.position = srcRt.position;
                        copy.rectTransform.sizeDelta = srcRt.sizeDelta;
                        copy.rectTransform.rotation = srcRt.rotation;
                        copy.raycastTarget = false;
                    }

                    var mover = ghost.AddComponent<GhostMover>();
                    mover.Initialize(i * 28f + 40f, 0.62f - i * 0.12f);
                }
            }

            // 4) 淡出
            const float fadeTime = 0.35f;
            t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / fadeTime);
                flash.color = new Color(0.45f, 0.32f, 0.85f, 0.25f * (1f - k));
                tint.color = new Color(0.35f, 0.85f, 0.92f, 0.20f * (1f - k));
                captionText.color = new Color(0.94f, 0.92f, 1f, 1f - k);
                yield return null;
            }

            Object.Destroy(flash.gameObject);
            Object.Destroy(tint.gameObject);
            Object.Destroy(captionText.gameObject);
            Object.Destroy(timeline);
        }

        /// <summary>让残影向右漂、淡出、然后自毁。</summary>
        public class GhostMover : MonoBehaviour
        {
            private float _speed = 40f;
            private float _lifetime = 0.6f;
            private float _age;

            public void Initialize(float speed, float lifetime)
            {
                _speed = speed;
                _lifetime = lifetime;
            }

            private void Update()
            {
                _age += Time.deltaTime;
                var rt = transform as RectTransform;
                if (rt != null)
                {
                    rt.anchoredPosition += new Vector2(_speed * Time.deltaTime, 0f);
                }

                if (_age >= _lifetime)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
