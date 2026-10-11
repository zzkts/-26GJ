using UnityEngine;

namespace CuoGuo
{
    /// <summary>
    /// 把全局借时债务翻译成"眼前的现在正在损坏"这件事。
    ///
    /// 策划案第三部分第 6 节的档位：
    ///   0—24  画面基本完整，只有少量颗粒或淡化；
    ///   25—49 人物略微透明，色彩开始褪去；
    ///   50—74 面部与背景细节变淡，部分物件失色；
    ///   75—99 非关键对象像纸片一样从边缘消散，人物接近轮廓；
    ///   100   当前画面可以完全消失。
    ///
    /// 实现只用透明度与失色（SpriteRenderer/UGUI 的 alpha 与 Color.Lerp），
    /// 不做燃烧模拟、不写专用 Shader。
    /// </summary>
    public class MemoryDecayController : MonoBehaviour
    {
        private int _lastDebt = -1;

        /// <summary>当前档位 0—4，供 UI 与音效判断是否跨过了阈值。</summary>
        public int Stage { get; private set; }

        /// <summary>0—1 的连续损坏程度。</summary>
        public float Amount { get; private set; }

        /// <summary>跨过 25/50/75/100 阈值时触发一次（用于播放纸张褪色音效）。</summary>
        public event System.Action<int> StageChanged;

        /// <summary>
        /// 汇报债务。只有数值真的变了才重新计算并广播，避免每帧刷屏。
        /// </summary>
        public void Report(int debt)
        {
            if (debt == _lastDebt)
            {
                return;
            }

            int previousStage = _lastDebt < 0 ? 0 : DebtModel.DecayStage(_lastDebt);
            _lastDebt = debt;
            Stage = DebtModel.DecayStage(debt);
            Amount = DebtModel.DecayAmount(debt);

            if (Stage != previousStage && StageChanged != null)
            {
                StageChanged(Stage);
            }
        }

        /// <summary>重新开始时清空状态。</summary>
        public void ResetTo(int debt)
        {
            _lastDebt = -1;
            Report(debt);
        }

        /// <summary>轻微的画面颗粒感强度（0—1），供 UI 层画一层噪点用。</summary>
        public float GrainStrength
        {
            get { return Mathf.Clamp01(Amount * 1.2f); }
        }
    }
}
