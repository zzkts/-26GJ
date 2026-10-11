using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JieNiYiMiao
{
    /// <summary>
    /// 把舞台上的几何造型变成可点击区域。
    ///
    /// 实现选择说明：舞台是画布（Canvas）下的 UI 图元，所以点击直接用 UGUI 的图元射线，
    /// 由造型自身的 Image 接管。之前考虑过 2D 物理射线（BoxCollider2D + Physics2DRaycaster），
    /// 但屏幕空间画布下的 UI 元素并没有对应的世界坐标，缩放时极易错位，故放弃。
    /// </summary>
    public class StageClickProxy : MonoBehaviour, IPointerClickHandler
    {
        public ClickableActor Actor;
        public ClickableProp Prop;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Actor != null)
            {
                Actor.HandlePointerClick();
                return;
            }

            if (Prop != null)
            {
                Prop.HandlePointerClick();
            }
        }

        /// <summary>
        /// 让造型下所有可见图元参与射线检测，这样人物身上的每一块（头、身体、手臂）都能被点到，
        /// 不会出现"看起来点了人但没反应"。
        /// </summary>
        public static StageClickProxy Attach(RectTransform target)
        {
            if (target == null)
            {
                return null;
            }

            var proxy = target.GetComponent<StageClickProxy>();
            if (proxy == null)
            {
                proxy = target.gameObject.AddComponent<StageClickProxy>();
            }

            var images = target.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                images[i].raycastTarget = true;
            }

            return proxy;
        }
    }
}
