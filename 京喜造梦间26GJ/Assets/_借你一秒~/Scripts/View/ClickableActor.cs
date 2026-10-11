using System;
using UnityEngine;

namespace JieNiYiMiao
{
    /// <summary>
    /// 舞台上可以被"选中"的人。点击只做一件事：把点击事件交给 GameManager，
    /// 由 GameManager 决定当前阶段该不该响应（这样重复点击绝不会造成重复倒流）。
    /// </summary>
    public class ClickableActor : MonoBehaviour
    {
        /// <summary>屏幕上的唯一 id，例如 boy / grown_up / elder / mother。</summary>
        public string ActorId = string.Empty;

        public string DisplayName = string.Empty;

        /// <summary>点击回调，由 SceneBuilder 在创建时注入。</summary>
        private Action<string> _onClicked;

        /// <summary>选中光圈（由 SceneBuilder 注入，用于显示选中反馈）。</summary>
        private RectTransform _highlight;

        /// <summary>头顶的气泡文字。</summary>
        private UnityEngine.UI.Text _moodText;

        private bool _selected;

        public bool IsSelected
        {
            get { return _selected; }
        }

        public void Configure(string actorId, string displayName, Action<string> onClicked, RectTransform highlight, UnityEngine.UI.Text moodText)
        {
            ActorId = actorId;
            DisplayName = displayName;
            _onClicked = onClicked;
            _highlight = highlight;
            _moodText = moodText;
            SetSelected(false);
        }

        /// <summary>选中状态：光圈显隐 + 轻微放大。用代码直接切换，不依赖 Animator。</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_highlight != null)
            {
                _highlight.gameObject.SetActive(selected);
            }

            var rt = transform as RectTransform;
            if (rt != null)
            {
                rt.localScale = selected ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
            }
        }

        public void SetMood(string mood)
        {
            if (_moodText != null)
            {
                _moodText.text = mood;
            }
        }

        public void HandlePointerClick()
        {
            if (string.IsNullOrEmpty(ActorId))
            {
                Debug.LogWarning("[借你一秒] ClickableActor 缺少 ActorId，忽略点击。");
                return;
            }

            if (_onClicked != null)
            {
                _onClicked(ActorId);
            }
        }
    }
}
