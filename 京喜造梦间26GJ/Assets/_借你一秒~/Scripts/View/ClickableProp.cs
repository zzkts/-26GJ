using System;
using UnityEngine;

namespace JieNiYiMiao
{
    /// <summary>
    /// 舞台上可以被点击的关键物件（木船、手机、旧木板……）。
    /// 和 ClickableActor 一样，重复点击只会被 GameManager 按当前阶段过滤掉。
    /// </summary>
    public class ClickableProp : MonoBehaviour
    {
        public string PropId = string.Empty;
        public string DisplayName = string.Empty;

        private Action<string> _onClicked;
        private RectTransform _highlight;

        public void Configure(string propId, string displayName, Action<string> onClicked, RectTransform highlight)
        {
            PropId = propId;
            DisplayName = displayName;
            _onClicked = onClicked;
            _highlight = highlight;
            SetHighlighted(false);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_highlight != null)
            {
                _highlight.gameObject.SetActive(highlighted);
            }
        }

        public void HandlePointerClick()
        {
            if (string.IsNullOrEmpty(PropId))
            {
                Debug.LogWarning("[借你一秒] ClickableProp 缺少 PropId，忽略点击。");
                return;
            }

            if (_onClicked != null)
            {
                _onClicked(PropId);
            }
        }
    }
}
