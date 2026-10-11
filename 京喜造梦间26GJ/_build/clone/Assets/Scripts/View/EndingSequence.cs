using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CuoGuo
{
    /// <summary>
    /// 唯一结局《记忆归还》。
    ///
    /// 演出顺序（策划案第二部分"唯一结局"）：
    ///   1. 三个细节快速闪回：母亲刻字时笑了 / 朋友在入口站了很久才转身 / 纸船上画着笑脸；
    ///   2. "你借走了那么多秒。" → "现在，把它们还回去。"；
    ///   3. 滑块回到起点，场景逐渐恢复，先前褪去的动作与物件重新显现；
    ///   4. 最终木船特写：船底有罗盘，船缝里藏着海图，夹层里是系红线的纸船，
    ///      船舷上刻着"往有光的地方去。"；
    ///   5. 镜头拉远，海浪声持续，天色渐亮，最后一行："他们都知道。"
    ///
    /// 无论玩家借了多少秒，这里都只有一条路径——不产生分支结局。
    /// </summary>
    public class EndingSequence
    {
        private readonly GameManager _gm;
        private RectTransform _scene;
        private Text _line;
        private Text _carving;

        public EndingSequence(GameManager gm)
        {
            _gm = gm;
            _line = gm.EndingText;
            _carving = gm.EndingCarving;
        }

        public IEnumerator Play()
        {
            // 音乐推向情感峰值……
            _gm.Sound.FadeMusic(0.34f, 1.4f);

            // ---- 1. 三段细节闪回 ----
            for (int i = 0; i < _gm.Stages.Length; i++)
            {
                ChapterStage stage = _gm.Stages[i];
                stage.Show();
                stage.SetSliderInteractable(false);
                stage.PoseAt(0f);          // 停在每段记忆最早、线索最完整的位置
                stage.ApplyDecay(0f);      // 结局里记忆被归还，褪色一并撤掉

                yield return FadeGroup(stage, 0f, 1f, 0.45f);
                yield return _gm.FadeTextRoutine(_line, StoryBook.EndingFlashbacks[i], 1.15f, 0.35f);
                yield return FadeGroup(stage, 1f, 0f, 0.4f);
            }

            for (int i = 0; i < _gm.Stages.Length; i++)
            {
                _gm.Stages[i].Hide();
            }

            // ---- 2. 借走的那些秒 ----
            for (int i = 0; i < StoryBook.EndingLines.Length; i++)
            {
                yield return _gm.FadeTextRoutine(_line, StoryBook.EndingLines[i], 1.2f, 0.45f);
            }

            // ……然后突然安静，只留海浪声。
            _gm.Sound.FadeMusic(0f, 0.55f);
            _gm.Sound.FadeWaves(0.5f, 2.8f);

            // ---- 3 & 4. 木船特写：三件线索同时出现 ----
            EnsureScene();
            _scene.gameObject.SetActive(true);
            yield return FadeCanvas(_scene, 0f, 1f, 1.1f);

            yield return _gm.FadeTextRoutine(_carving, StoryBook.EndingCarving, 1.4f, 0.6f);

            // ---- 5. 最后一行 ----
            yield return _gm.FadeTextRoutine(_line, StoryBook.FinalLine, 3.0f, 0.9f);
        }

        /// <summary>重新开始时把结局布景收回。</summary>
        public void Reset()
        {
            if (_scene != null)
            {
                _scene.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------ 结局布景

        private void EnsureScene()
        {
            if (_scene != null)
            {
                return;
            }

            _scene = UiFactory.MakeStretchedGroup(_gm.StageRoot, "EndingScene");
            _scene.gameObject.AddComponent<CanvasGroup>();

            // 天色渐亮：比黄昏更亮、更淡的一层。
            RectTransform backdrop = DockArt.BuildBackdrop(_scene, "dawn",
                new Color(0.88f, 0.86f, 0.80f), new Color(0.52f, 0.60f, 0.64f), new Color(0.48f, 0.40f, 0.32f));
            backdrop.name = "EndingBackdrop";

            // 木船特写：船身占据画面下半部。
            RectTransform boat = DockArt.BuildBoat(_scene, new Vector2(1180f, 520f), new Color(0.66f, 0.50f, 0.34f));
            boat.name = "EndingBoat";
            boat.anchoredPosition = new Vector2(0f, -120f);

            // 三件线索：船底的罗盘、船缝里的海图、夹层里的纸船。
            RectTransform compass = DockArt.BuildCompass(_scene, new Vector2(86f, 86f));
            compass.anchoredPosition = new Vector2(-322f, -168f);

            RectTransform chart = DockArt.BuildChart(_scene, new Vector2(150f, 116f));
            chart.anchoredPosition = new Vector2(36f, -158f);

            RectTransform paper = DockArt.BuildPaperBoat(_scene, new Vector2(132f, 104f), false);
            paper.anchoredPosition = new Vector2(352f, -176f);

            // 船身侧面露出的那一截红线。
            RectTransform thread = DockArt.BuildRedThread(_scene, new Vector2(110f, 74f));
            thread.anchoredPosition = new Vector2(352f, -132f);

            // 镜头拉远之后，刻字仍在船舷上。
            RectTransform carving = DockArt.BuildCarving(_scene, new Vector2(420f, 116f));
            carving.anchoredPosition = new Vector2(-20f, -46f);

            UiFactory.MakePanel(_scene, "EndingVeil", new Color(0.04f, 0.05f, 0.08f, 0.28f));
        }

        // ------------------------------------------------------------ 淡入淡出

        private static IEnumerator FadeGroup(ChapterStage stage, float from, float to, float duration)
        {
            duration = Scale(duration);
            stage.SetAlpha(from);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                stage.SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }

            stage.SetAlpha(to);
        }

        /// <summary>自检模式下的演出倍速，只影响等待时长。</summary>
        private static float Scale(float seconds)
        {
            float speed = GameManager.SpeedFactor <= 0.01f ? 1f : GameManager.SpeedFactor;
            return seconds / speed;
        }

        private static IEnumerator FadeCanvas(RectTransform root, float from, float to, float duration)
        {
            duration = Scale(duration);
            var group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            group.alpha = to;
        }
    }
}
