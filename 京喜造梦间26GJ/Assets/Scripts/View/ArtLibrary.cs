using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 运行时取图：能取到正式美术就用正式美术，取不到就返回 null，由调用方退回程序化几何占位。
    ///
    /// 这样做的原因（也是本工程一贯的原则）：美术是**增量接入**的，不能因为某张图没到位
    /// 就让游戏跑不起来或出现空白。任何一张图缺失，画面退回原来的色块造型，流程与玩法完全不受影响。
    ///
    /// 图放在 Assets/Resources/Art/，由 Assets/Editor/ArtPipeline.cs 从 Art/Processed 同步过去。
    /// </summary>
    public static class ArtLibrary
    {
        private const string Root = "Art/";

        /// <summary>取一张 Sprite；没有就返回 null。</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return Resources.Load<Sprite>(Root + name);
        }

        public static bool Has(string name)
        {
            return Get(name) != null;
        }

        /// <summary>
        /// 把一张背景按 16:9 铺满整块画布：**等比放大到刚好盖住**，居中，不拉伸变形。
        /// </summary>
        public static RectTransform PlaceBackground(Transform parent, string name)
        {
            Sprite sprite = Get(name);
            if (sprite == null)
            {
                return null;
            }

            var go = new GameObject("BG_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;

            // 等比缩放到"盖住"画布：取两个方向所需比例的较大者，绝不非等比拉伸
            float scale = Mathf.Max(UiFactory.CanvasWidth / sprite.rect.width,
                                    UiFactory.CanvasHeight / sprite.rect.height);
            rt.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale);
            return rt;
        }
    }
}
