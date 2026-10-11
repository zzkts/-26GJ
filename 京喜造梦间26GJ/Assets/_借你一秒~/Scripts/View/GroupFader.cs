using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>
    /// 一组 UI 元素的显隐与淡入淡出。用它替代 Animator/CanvasGroup，
    /// 好处是"面板里之后才生成的元素"也能在 Capture 时自动纳入，不需要手动维护引用。
    /// </summary>
    public class GroupFader : MonoBehaviour
    {
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly List<Button> _buttons = new List<Button>();
        private Coroutine _fade;

        /// <summary>把子节点里所有可绘制元素登记进来，并立即应用一次目标透明度。</summary>
        public void Capture(bool visible)
        {
            _graphics.Clear();
            _buttons.Clear();
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                _graphics.Add(graphic);
            }

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                _buttons.Add(button);
            }

            Apply(visible ? 1f : 0f);
            gameObject.SetActive(visible || HasVisibleContent());
        }

        public void SetVisible(bool visible)
        {
            SetVisible(visible, false);
        }

        /// <summary>显隐切换。淡出结束后才真正 SetActive(false)，避免闪烁。</summary>
        public void SetVisible(bool visible, bool instant)
        {
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            if (visible)
            {
                gameObject.SetActive(true);
                if (instant)
                {
                    Capture(true);
                }
                else
                {
                    _fade = StartCoroutine(FadeRoutine(1f, 0.2f));
                }
            }
            else
            {
                if (instant || !gameObject.activeSelf)
                {
                    Apply(0f);
                    gameObject.SetActive(false);
                }
                else
                {
                    _fade = StartCoroutine(FadeRoutine(0f, 0.18f));
                }
            }
        }

        /// <summary>把当前透明度平滑过渡到目标值。</summary>
        public IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (_fade != null)
            {
                StopCoroutine(_fade);
                _fade = null;
            }

            yield return FadeRoutine(targetAlpha, duration);
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration)
        {
            if (duration <= 0f)
            {
                Apply(targetAlpha);
                yield break;
            }

            float startAlpha = CurrentAlpha();
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                Apply(Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            Apply(targetAlpha);
            if (targetAlpha <= 0.001f)
            {
                gameObject.SetActive(false);
            }

            _fade = null;
        }

        private void Apply(float alpha)
        {
            for (int i = 0; i < _graphics.Count; i++)
            {
                var graphic = _graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                // 文字和图片都保留各自的基色，只改 alpha
                if (graphic is Text)
                {
                    var text = (Text)graphic;
                    var c = text.color;
                    text.color = new Color(c.r, c.g, c.b, alpha);
                }
                else
                {
                    var c = graphic.color;
                    graphic.color = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        private float CurrentAlpha()
        {
            for (int i = 0; i < _graphics.Count; i++)
            {
                if (_graphics[i] != null)
                {
                    return _graphics[i].color.a;
                }
            }

            return 1f;
        }

        private bool HasVisibleContent()
        {
            for (int i = 0; i < _graphics.Count; i++)
            {
                if (_graphics[i] != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
