using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 一个可以被"整体染色"的物件：记录自己所有子元素的基色，
    /// 之后无论时间轴要它变色/淡出，还是记忆褪色要它受损，都只在基色上乘一层，
    /// 不会互相覆盖、也不会把美术自己配的颜色弄丢。
    ///
    /// 两条通道：
    /// - TimelineTint：由时间轴关键帧驱动（这物件在"那一刻"应该多亮、什么色）；
    /// - Decay：由借时债务驱动（记忆损坏程度 0—1）。
    /// </summary>
    public class Tintable : MonoBehaviour
    {
        private Graphic[] _graphics;
        private Color[] _baseColors;

        private Color _timelineTint = Color.white;
        private float _decay;

        /// <summary>
        /// 这个物件对"记忆褪色"的敏感度。0 = 完全免疫（关键线索用），1 = 全额受损（布景、路人用）。
        /// 策划案要求核心线索在被回溯到时必须仍然可读，就靠这个值兜底。
        /// </summary>
        [Range(0f, 1f)]
        public float DecayWeight = 1f;

        private void Awake()
        {
            Capture();
        }

        /// <summary>收集子元素与基色。物件内容在运行期还会被加东西时要再调一次。</summary>
        public void Capture()
        {
            _graphics = GetComponentsInChildren<Graphic>(true);
            _baseColors = new Color[_graphics.Length];
            for (int i = 0; i < _graphics.Length; i++)
            {
                _baseColors[i] = _graphics[i] == null ? Color.white : _graphics[i].color;
            }

            Apply();
        }

        /// <summary>时间轴给的颜色（含透明度）。</summary>
        public void SetTimelineTint(Rgba color)
        {
            _timelineTint = new Color(color.R, color.G, color.B, color.A);
            Apply();
        }

        public void SetTimelineTint(Color color)
        {
            _timelineTint = color;
            Apply();
        }

        /// <summary>记忆褪色程度 0—1（由 MemoryDecayController 统一给）。</summary>
        public void SetDecay(float decay01)
        {
            _decay = Mathf.Clamp01(decay01);
            Apply();
        }

        public Color TimelineTint
        {
            get { return _timelineTint; }
        }

        private void Apply()
        {
            if (_graphics == null)
            {
                return;
            }

            float d = _decay * Mathf.Clamp01(DecayWeight);

            // 褪色：整体向冷灰靠拢，同时透明度下降。
            // 债务满时普通物件几乎消失，而 DecayWeight 小的关键线索仍然看得清。
            Color decayTint = new Color(
                Mathf.Lerp(1f, 0.68f, d),
                Mathf.Lerp(1f, 0.72f, d),
                Mathf.Lerp(1f, 0.84f, d),
                1f - 0.92f * d);

            for (int i = 0; i < _graphics.Length; i++)
            {
                var graphic = _graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color b = _baseColors[i];
                graphic.color = new Color(
                    b.r * _timelineTint.r * decayTint.r,
                    b.g * _timelineTint.g * decayTint.g,
                    b.b * _timelineTint.b * decayTint.b,
                    b.a * _timelineTint.a * decayTint.a);
            }
        }
    }
}
