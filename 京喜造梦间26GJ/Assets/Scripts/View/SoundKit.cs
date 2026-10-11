using UnityEngine;

namespace CuoGuo
{
    /// <summary>
    /// 全部音效在运行时用代码合成，不引入任何音频素材文件。
    ///
    /// 原因是本工程的技术边界："没有正式素材时用可替换的占位"——
    /// 音频同理，先用合成音把"拖动时的倒流感、线索出现、纸张褪色、结尾海浪"
    /// 这几处必需的声音反馈补上，正式素材到位后只要换掉生成函数即可。
    ///
    /// 采样率 44100，单声道。
    /// </summary>
    public class SoundKit : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private AudioSource _fx;
        private AudioSource _ambience;
        private AudioSource _music;

        private AudioClip _rewind;
        private AudioClip _clue;
        private AudioClip _paper;
        private AudioClip _waves;
        private AudioClip _tick;
        private AudioClip _theme;

        private float _lastTickTime;

        public bool Muted { get; set; }

        public bool AudioMuted
        {
            get { return Muted; }
        }

        /// <summary>音乐、倒带、线索、纸张、海浪、滑块六段音频是否都已合成（或已优雅静音）。</summary>
        public bool AudioReady
        {
            get
            {
                if (Muted)
                {
                    return true;
                }

                return _theme != null && _theme.samples > 1000
                       && _rewind != null && _clue != null && _paper != null
                       && _waves != null && _tick != null;
            }
        }

        public void Init()
        {
            var fxGo = new GameObject("SfxSource", typeof(AudioSource));
            fxGo.transform.SetParent(transform, false);
            _fx = fxGo.GetComponent<AudioSource>();
            _fx.playOnAwake = false;
            _fx.volume = 0.55f;

            var ambGo = new GameObject("AmbienceSource", typeof(AudioSource));
            ambGo.transform.SetParent(transform, false);
            _ambience = ambGo.GetComponent<AudioSource>();
            _ambience.playOnAwake = false;
            _ambience.loop = true;
            _ambience.volume = 0f;

            var musicGo = new GameObject("MusicSource", typeof(AudioSource));
            musicGo.transform.SetParent(transform, false);
            _music = musicGo.GetComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0f;

            // 音频初始化失败（例如无声卡的无头环境）不应该影响游戏本身。
            try
            {
                _rewind = BuildRewind();
                _clue = BuildClue();
                _paper = BuildPaper();
                _waves = BuildWaves();
                _tick = BuildTick();
                _theme = BuildTheme();
            }
            catch (System.Exception e)
            {
                Muted = true;
                Debug.LogWarning("[错过了那一秒] 音频初始化失败，已静音继续：" + e.Message);
            }
        }

        /// <summary>主背景音乐：温暖、怀旧、克制，可循环。</summary>
        public void PlayMusic()
        {
            if (Muted || _music == null || _theme == null)
            {
                return;
            }

            _music.clip = _theme;
            _music.volume = 0.22f;
            _music.Play();
        }

        private Coroutine _musicFade;
        private Coroutine _ambienceFade;

        /// <summary>把音乐推上去或收下来。结尾用它把音乐推到峰值后突然安静，只留海浪。</summary>
        public void FadeMusic(float targetVolume, float duration)
        {
            if (Muted || _music == null)
            {
                return;
            }

            // 只打断音乐自己的淡变，不要连海浪一起掐掉
            if (_musicFade != null)
            {
                StopCoroutine(_musicFade);
            }

            _musicFade = StartCoroutine(FadeSourceRoutine(_music, targetVolume, duration));
        }

        // ------------------------------------------------------------ 播放接口

        /// <summary>拖动时间时的倒带感（反向气流）。按拖动速度调整音量与音高。</summary>
        public void PlayRewind(float intensity)
        {
            if (Muted || _fx == null || _rewind == null)
            {
                return;
            }

            float v = Mathf.Clamp01(intensity);
            _fx.pitch = Mathf.Lerp(0.85f, 1.35f, v);
            _fx.PlayOneShot(_rewind, Mathf.Lerp(0.25f, 0.85f, v));
        }

        /// <summary>线索出现：柔和、轻短，不要像奖励弹窗。</summary>
        public void PlayClue()
        {
            if (Muted || _fx == null || _clue == null)
            {
                return;
            }

            _fx.pitch = 1f;
            _fx.PlayOneShot(_clue, 0.42f);
        }

        /// <summary>画面褪色：轻微纸张摩擦 / 纸片碎裂。</summary>
        public void PlayPaper()
        {
            if (Muted || _fx == null || _paper == null)
            {
                return;
            }

            _fx.pitch = 1f;
            _fx.PlayOneShot(_paper, 0.38f);
        }

        /// <summary>拖动时极轻的机械声，节流到每秒最多十几次。</summary>
        public void PlayTick()
        {
            if (Muted || _fx == null || _tick == null)
            {
                return;
            }

            if (Time.unscaledTime - _lastTickTime < 0.07f)
            {
                return;
            }

            _lastTickTime = Time.unscaledTime;
            _fx.pitch = Random.Range(0.92f, 1.12f);
            _fx.PlayOneShot(_tick, 0.16f);
        }

        /// <summary>结尾的海浪：一直循环，结束时音乐停掉只留它。</summary>
        public void FadeWaves(float targetVolume, float duration)
        {
            if (_ambience == null || _waves == null)
            {
                return;
            }

            if (!_ambience.isPlaying)
            {
                _ambience.clip = _waves;
                _ambience.volume = 0f;
                _ambience.Play();
            }

            if (_ambienceFade != null)
            {
                StopCoroutine(_ambienceFade);
            }

            _ambienceFade = StartCoroutine(FadeSourceRoutine(_ambience, targetVolume, duration));
        }

        private static System.Collections.IEnumerator FadeSourceRoutine(AudioSource source, float target, float duration)
        {
            float start = source.volume;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, target, Mathf.Clamp01(t / duration));
                yield return null;
            }

            source.volume = target;
        }

        // ------------------------------------------------------------ 合成

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>倒带：一段被低通扫频的噪声，加一点下行的正弦，长约 0.6 秒。</summary>
        private static AudioClip BuildRewind()
        {
            int n = (int)(SampleRate * 0.6f);
            var data = new float[n];
            float lowpass = 0f;
            float phase = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;

                // 噪声：越往后越闷，像磁带倒转时高频先消失
                float noise = Random.Range(-1f, 1f);
                float cutoff = Mathf.Lerp(0.55f, 0.06f, t);
                lowpass += (noise - lowpass) * cutoff;

                // 下行正弦：给一点"往回抽"的方向感
                float freq = Mathf.Lerp(760f, 210f, t);
                phase += 2f * Mathf.PI * freq / SampleRate;

                // 包络：轻轻推起、缓慢收尾，避免啪声
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.75f)) * (1f - t * 0.35f);

                data[i] = (lowpass * 1.6f + Mathf.Sin(phase) * 0.16f) * envelope * 0.5f;
            }

            return Make("sfx_rewind", data);
        }

        /// <summary>线索：两个正弦分音 + 指数衰减，温和不刺耳。</summary>
        private static AudioClip BuildClue()
        {
            int n = (int)(SampleRate * 0.45f);
            var data = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Exp(-t * 7.5f);
                float a = Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.5f;
                float b = Mathf.Sin(2f * Mathf.PI * 990f * t) * 0.28f;
                float c = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.12f;
                data[i] = (a + b + c) * envelope * 0.42f;
            }

            return Make("sfx_clue", data);
        }

        /// <summary>纸张：极短的高频摩擦噪声。</summary>
        private static AudioClip BuildPaper()
        {
            int n = (int)(SampleRate * 0.3f);
            var data = new float[n];
            float previous = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float noise = Random.Range(-1f, 1f);

                // 一阶高通：保留沙沙的高频，去掉沉闷的低频
                float high = noise - previous;
                previous = noise;

                float envelope = Mathf.Pow(1f - t, 2.6f);
                data[i] = high * envelope * 0.32f;
            }

            return Make("sfx_paper", data);
        }

        /// <summary>海浪：棕噪声 + 缓慢起伏，循环 4 秒。</summary>
        private static AudioClip BuildWaves()
        {
            int n = SampleRate * 4;
            var data = new float[n];
            float brown = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float white = Random.Range(-1f, 1f);
                brown = (brown + 0.022f * white) / 1.022f;

                // 两层不同周期的起伏，听上去像潮水一涨一退
                float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / 4f);
                float ripple = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t * 1.7f);

                data[i] = brown * 3.4f * swell * ripple;
            }

            // 首尾交叉淡化，循环时不会咔哒一声
            int fade = SampleRate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[n - 1 - i] *= k;
            }

            return Make("sfx_waves", data);
        }

        /// <summary>滑块机械声：非常短的一记轻响。</summary>
        private static AudioClip BuildTick()
        {
            int n = (int)(SampleRate * 0.035f);
            var data = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float envelope = Mathf.Pow(1f - t, 4f);
                float body = Mathf.Sin(2f * Mathf.PI * 1750f * (i / (float)SampleRate));
                float click = Random.Range(-1f, 1f) * 0.25f;
                data[i] = (body * 0.7f + click) * envelope * 0.5f;
            }

            return Make("sfx_tick", data);
        }

        /// <summary>
        /// 主背景音乐：一段缓慢的琶音加低音，温暖克制，可无缝循环。
        /// 用 Am—F—C—G 四个和弦、每个 4 秒，整段 32 秒，后半段略轻以减弱机械感。
        /// </summary>
        private static AudioClip BuildTheme()
        {
            const int ChordCount = 4;
            float chordSeconds = 4f;
            int chordSamples = (int)(SampleRate * chordSeconds);
            int total = chordSamples * ChordCount * 2;
            var data = new float[total];

            var bass = new[] { 110.00f, 87.31f, 130.81f, 98.00f };      // A2 / F2 / C3 / G2
            var arp = new[]
            {
                new[] { 220.00f, 261.63f, 329.63f, 261.63f },           // Am
                new[] { 220.00f, 261.63f, 349.23f, 261.63f },           // F
                new[] { 196.00f, 261.63f, 329.63f, 392.00f },           // C
                new[] { 246.94f, 293.66f, 392.00f, 293.66f }            // G
            };

            for (int chord = 0; chord < ChordCount; chord++)
            {
                AddChord(data, chord * chordSamples, chordSamples, bass[chord], arp[chord]);
            }

            // 第二遍整体轻一点，听上去像一遍安静的重复而不是死循环
            int half = chordSamples * ChordCount;
            for (int i = 0; i < half; i++)
            {
                data[half + i] = data[i] * 0.82f;
            }

            float peak = 0f;
            for (int i = 0; i < total; i++)
            {
                float a = data[i] < 0f ? -data[i] : data[i];
                if (a > peak)
                {
                    peak = a;
                }
            }

            if (peak > 0.0001f)
            {
                float k = 0.62f / peak;
                for (int i = 0; i < total; i++)
                {
                    data[i] *= k;
                }
            }

            // 首尾交叉淡化，循环接缝不会咔哒
            int fade = SampleRate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[total - 1 - i] *= k;
            }

            return Make("bgm_theme", data);
        }

        private static void AddChord(float[] data, int start, int samples, float bassFreq, float[] arpNotes)
        {
            // 低音：整小节持续，很轻
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Mathf.Min(1f, t * 3f) * Mathf.Min(1f, (samples - i) / (SampleRate * 0.8f));
                float value = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * 0.30f
                              + Mathf.Sin(2f * Mathf.PI * bassFreq * 2f * t) * 0.06f;
                data[start + i] += value * envelope;
            }

            // 琶音：每 0.5 秒一个音，轮转和弦音级，尾音自然衰减到下一个音上
            int noteSamples = (int)(SampleRate * 0.5f);
            int noteIndex = 0;
            for (int offset = 0; offset < samples; offset += noteSamples)
            {
                float freq = arpNotes[noteIndex % arpNotes.Length];
                noteIndex++;

                for (int i = 0; i < noteSamples * 2 && offset + i < samples; i++)
                {
                    float t = i / (float)SampleRate;
                    float envelope = Mathf.Exp(-t * 3.1f) * (1f - Mathf.Exp(-t * 120f));
                    float value = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.42f
                                  + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.11f
                                  + Mathf.Sin(2f * Mathf.PI * freq * 3f * t) * 0.035f;
                    data[start + offset + i] += value * envelope;
                }
            }
        }
    }
}
