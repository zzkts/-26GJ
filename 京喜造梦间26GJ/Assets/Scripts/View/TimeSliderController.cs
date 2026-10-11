using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 藏在码头场景里的那枚时间滑块。
    ///
    /// 策划案硬性要求：
    /// - 它是**世界内的实体**，不是常驻 HUD 时间条；
    /// - 只能沿轨道水平拖动，映射成 0—1 的连续 TimelinePosition；
    /// - **不吸附**到任何固定档位，松开鼠标就停在当前位置，不自动播放；
    /// - 滑块不能被 UI 或遮罩挡住而无法操作。
    ///
    /// 右端 = 1.0（现在），左端 = 0.0（这段记忆最早的过去）。
    /// </summary>
    public class TimeSliderController : MonoBehaviour
    {
        private RectTransform _space;      // 坐标换算用的公共父节点（章节根）
        private RectTransform _knob;
        private RectTransform _hintGlow;
        private Canvas _canvas;

        private float _railCenterX;
        private float _railCenterY;
        private float _railWidth;

        private float _position = 1f;
        private float _grabOffset;
        private bool _dragging;
        private bool _interactable = true;

        [Range(0.01f, 0.4f)]
        public float DragSensitivity = 1f;

        /// <summary>位置变化回调（连续值，每帧拖动都会触发）。</summary>
        public event System.Action<float> PositionChanged;

        /// <summary>抓取/松开回调，用于播放音效。</summary>
        public event System.Action<bool> GrabChanged;

        public bool IsDragging
        {
            get { return _dragging; }
        }

        /// <summary>现在能不能拖（演出期间为 false）。</summary>
        public bool Interactable
        {
            get { return _interactable; }
        }

        public float Position
        {
            get { return _position; }
        }

        public RectTransform Knob
        {
            get { return _knob; }
        }

        /// <summary>
        /// 装配。space 是坐标基准（章节根），knob 是要被拖动的把手，
        /// railCenter/railWidth 描述世界内那条轨道的左右端点。
        /// </summary>
        public void Setup(RectTransform space, RectTransform knob, Vector2 railCenter, float railWidth, Canvas canvas)
        {
            _space = space;
            _knob = knob;
            _canvas = canvas;
            _railCenterX = railCenter.x;
            _railCenterY = railCenter.y;
            _railWidth = Mathf.Max(40f, railWidth);
            SnapKnobToPosition();
        }

        /// <summary>发现提示用的柔光（可选）。玩家迟迟找不到滑块时它会轻轻呼吸。</summary>
        public void SetHintGlow(RectTransform glow)
        {
            _hintGlow = glow;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value && _dragging)
            {
                EndDrag();
            }
        }

        /// <summary>外部（结局演出）把滑块挪回某个位置，不触发位置变化回调。</summary>
        public void SetPositionSilently(float position)
        {
            _position = Mathf.Clamp01(position);
            SnapKnobToPosition();
        }

        // ------------------------------------------------------------ 拖动

        /// <summary>由把手或轨道上的命中区域转发进来。</summary>
        public void BeginDrag(PointerEventData eventData)
        {
            if (!_interactable)
            {
                return;
            }

            _dragging = true;
            _grabOffset = 0f;

            // 抓在把手边缘时不要瞬移：记录鼠标与把手中心的偏移。
            Vector2 local;
            if (TryGetLocalPoint(eventData, out local))
            {
                float knobX = _knob != null ? _knob.anchoredPosition.x : 0f;
                float delta = Mathf.Abs(local.x - knobX);
                _grabOffset = delta < 80f ? knobX - local.x : 0f;
            }

            if (GrabChanged != null)
            {
                GrabChanged(true);
            }

            if (Mathf.Abs(_grabOffset) < 0.01f)
            {
                ApplyLocalX(local.x);
            }
        }

        private void Update()
        {
            if (!_dragging)
            {
                AnimateHint();
                return;
            }

            if (!_interactable)
            {
                EndDrag();
                return;
            }

            // 用 Input.mousePosition 而不是事件里的 delta：鼠标移出命中区域时依然跟手。
            Vector2 screen = Input.mousePosition;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, screen, CanvasCamera(), out local))
            {
                ApplyLocalX(local.x + _grabOffset);
            }

            // 松开左键就结束；顺便兜住"在窗口外松手 / 窗口失焦"导致拖动态卡住的情况。
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                EndDrag();
            }
        }

        /// <summary>打断当前拖动（窗口失焦、演出接管等）。无人值守自检也用它收尾。</summary>
        public void CancelDrag()
        {
            EndDrag();
        }

        private void EndDrag()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            if (GrabChanged != null)
            {
                GrabChanged(false);
            }
        }

        private void ApplyLocalX(float localX)
        {
            float minX = _railCenterX - _railWidth * 0.5f;
            float p = Mathf.Clamp01((localX - minX) / _railWidth);
            p = Mathf.Clamp01(Mathf.Lerp(_position, p, Mathf.Clamp01(DragSensitivity)));

            if (Mathf.Abs(p - _position) < 0.000001f)
            {
                return;
            }

            _position = p;
            SnapKnobToPosition();

            if (PositionChanged != null)
            {
                PositionChanged(_position);
            }
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 local)
        {
            local = Vector2.zero;
            if (_space == null || eventData == null)
            {
                return false;
            }

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_space, eventData.position, CanvasCamera(), out local);
        }

        private Camera CanvasCamera()
        {
            if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return _canvas.worldCamera;
        }

        private void SnapKnobToPosition()
        {
            if (_knob == null)
            {
                return;
            }

            float minX = _railCenterX - _railWidth * 0.5f;
            _knob.anchoredPosition = new Vector2(minX + _railWidth * _position, _railCenterY);
        }

        // ------------------------------------------------------------ 发现提示

        private float _hintTimer;

        /// <summary>玩家已经多久没碰过滑块了（秒）。</summary>
        public float IdleSeconds
        {
            get { return _hintTimer; }
        }

        public void ResetIdle()
        {
            _hintTimer = 0f;
        }

        private void AnimateHint()
        {
            _hintTimer += Time.deltaTime;
            if (_hintGlow == null)
            {
                return;
            }

            // 10 秒内不打扰玩家；之后提示逐渐增强，但始终保持"轻微"（策划案：不要让寻找滑块变成卡关惩罚）。
            float t = Mathf.Clamp01((_hintTimer - 10f) / 8f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f);
            float alpha = t * (0.10f + 0.22f * pulse);
            _hintGlow.anchoredPosition = new Vector2(_railCenterX, _railCenterY);

            var graphic = _hintGlow.GetComponent<Graphic>();
            if (graphic != null)
            {
                var c = graphic.color;
                graphic.color = new Color(c.r, c.g, c.b, alpha);
            }

            float scale = 1f + t * 0.08f * pulse;
            _hintGlow.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>鼠标悬停在滑块上时的轻微高亮。</summary>
        public void SetHover(bool hover)
        {
            if (_hintGlow == null)
            {
                return;
            }

            if (hover)
            {
                _hintTimer = 0f;
            }
        }

        // ------------------------------------------------------------ 命中区域

        /// <summary>挂在滑块轨道上的命中区域：点它就能直接开始拖。</summary>
        public class RailHitArea : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public TimeSliderController Controller;
            public System.Action<bool> HoverChanged;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (Controller != null)
                {
                    Controller.BeginDrag(eventData);
                }
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Controller != null)
                {
                    Controller.SetHover(true);
                }

                if (HoverChanged != null)
                {
                    HoverChanged(true);
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (HoverChanged != null)
                {
                    HoverChanged(false);
                }
            }
        }

        /// <summary>挂在把手上：抓住它拖动时不会瞬移。</summary>
        public class KnobGrabber : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler
        {
            public TimeSliderController Controller;
            public System.Action<bool> HoverChanged;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (Controller != null)
                {
                    Controller.BeginDrag(eventData);
                }
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Controller != null)
                {
                    Controller.SetHover(true);
                }

                if (HoverChanged != null)
                {
                    HoverChanged(true);
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (HoverChanged != null)
                {
                    HoverChanged(false);
                }
            }
        }
    }
}
