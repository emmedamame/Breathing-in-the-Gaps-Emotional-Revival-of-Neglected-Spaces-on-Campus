using UnityEngine;

namespace SpaceFeedback
{
    /// <summary>
    /// 音频控制器 - 管理环境音效和状态对应的声音反馈
    /// 使用 Spatial Blend = 3D 空间音频
    /// </summary>
    public class AudioController : MonoBehaviour
    {
        [Header("音频源配置")]
        [Tooltip("背景音乐/氛围音 AudioSource")]
        public AudioSource ambientSource;

        [Tooltip("状态音效 AudioSource（如 hum、pulse 等）")]
        public AudioSource effectSource;

        [Tooltip("UI 反馈音效 AudioSource")]
        public AudioSource uiSource;

        [Header("Anxious 状态音效")]
        [Tooltip("低频嗡鸣声（恐惧/焦虑感）")]
        public AudioClip anxiousHumClip;
        [Range(0f, 1f)]
        public float anxiousHumVolume = 0.4f;
        [Range(0f, 2f)]
        public float anxiousHumPitch = 0.8f;

        [Header("Avoidant 状态音效")]
        [Tooltip("几乎无声的环境底噪")]
        public AudioClip avoidantAmbientClip;
        [Range(0f, 1f)]
        public float avoidantAmbientVolume = 0.15f;
        [Range(0f, 2f)]
        public float avoidantAmbientPitch = 1f;

        [Header("Explorative 状态音效")]
        [Tooltip("丰富的环境声（探索感）")]
        public AudioClip explorativeAmbientClip;
        [Range(0f, 1f)]
        public float explorativeAmbientVolume = 0.6f;
        [Range(0f, 2f)]
        public float explorativeAmbientPitch = 1.1f;

        [Header("过渡设置")]
        [Tooltip("音量过渡速度")]
        public float volumeLerpSpeed = 2f;
        [Tooltip("切换音效时的淡入淡出时间")]
        public float crossfadeDuration = 1f;

        [Header("3D 音频设置")]
        [Tooltip("空间混合模式（0=2D, 1=3D）")]
        [Range(0f, 1f)]
        public float spatialBlend = 1f;
        [Tooltip("3D 声音衰减模式")]
        public AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
        [Tooltip("最大聆听距离")]
        public float maxDistance = 100f;
        [Tooltip("3D 声音锥形角度")]
        public float spread = 0f;

        [Header("调试")]
        public bool enableDebugLog = true;

        // 内部状态
        private ExperienceState _currentState;
        private float _currentVolume;
        private float _targetVolume;
        private bool _isInitialized;
        private AudioClip _currentClip;

        private void Start()
        {
            // 订阅状态变化
            ExperienceManager.OnStateChanged += HandleStateChange;
            ExperienceManager.OnStateTransitionStart += HandleTransitionStart;

            // 初始化音频源
            InitializeAudioSources();

            // 设置初始状态
            _currentState = ExperienceManager.Instance != null
                ? ExperienceManager.Instance.CurrentState
                : ExperienceState.Anxious;

            ApplyState(_currentState, true);
        }

        private void OnDestroy()
        {
            ExperienceManager.OnStateChanged -= HandleStateChange;
            ExperienceManager.OnStateTransitionStart -= HandleTransitionStart;
        }

        private void Update()
        {
            UpdateAudio();
        }

        private void InitializeAudioSources()
        {
            // 初始化 Ambient Source
            if (ambientSource == null)
            {
                GameObject ambientObj = new GameObject("AmbientAudio");
                ambientObj.transform.SetParent(transform);
                ambientSource = ambientObj.AddComponent<AudioSource>();
            }
            ConfigureSpatialAudio(ambientSource);

            // 初始化 Effect Source
            if (effectSource == null)
            {
                GameObject effectObj = new GameObject("EffectAudio");
                effectObj.transform.SetParent(transform);
                effectSource = effectObj.AddComponent<AudioSource>();
            }
            ConfigureSpatialAudio(effectSource);
            effectSource.loop = true;

            // 初始化 UI Source
            if (uiSource == null)
            {
                GameObject uiObj = new GameObject("UIAudio");
                uiObj.transform.SetParent(transform);
                uiSource = uiObj.AddComponent<AudioSource>();
            }
            ConfigureSpatialAudio(uiSource);
            uiSource.spatialBlend = 0f; // UI 音效不使用 3D

            _isInitialized = true;
            LogDebug("音频控制器初始化完成");
        }

        private void ConfigureSpatialAudio(AudioSource source)
        {
            if (source == null) return;

            source.spatialBlend = spatialBlend;
            source.rolloffMode = rolloffMode;
            source.maxDistance = maxDistance;
            source.spread = spread;
            source.playOnAwake = false;
            source.loop = true;
        }

        private void HandleStateChange(ExperienceState newState)
        {
            _currentState = newState;
            ApplyState(newState, false);
        }

        private void HandleTransitionStart(ExperienceState from, ExperienceState to)
        {
            // 状态开始过渡时，触发音效切换
            if (from != to)
            {
                StartCoroutine(CrossfadeAudio(from, to));
            }
        }

        private void ApplyState(ExperienceState state, bool instant)
        {
            AudioClip targetClip = null;
            float targetVolume = 0f;
            float targetPitch = 1f;

            switch (state)
            {
                case ExperienceState.Anxious:
                    targetClip = anxiousHumClip;
                    targetVolume = anxiousHumVolume;
                    targetPitch = anxiousHumPitch;
                    break;

                case ExperienceState.Avoidant:
                    targetClip = avoidantAmbientClip;
                    targetVolume = avoidantAmbientVolume;
                    targetPitch = avoidantAmbientPitch;
                    break;

                case ExperienceState.Explorative:
                    targetClip = explorativeAmbientClip;
                    targetVolume = explorativeAmbientVolume;
                    targetPitch = explorativeAmbientPitch;
                    break;
            }

            _targetVolume = targetVolume;

            if (instant || _currentClip == null)
            {
                _currentClip = targetClip;
                _currentVolume = targetVolume;

                if (effectSource != null && targetClip != null)
                {
                    effectSource.clip = targetClip;
                    effectSource.volume = _currentVolume;
                    effectSource.pitch = targetPitch;
                    if (!effectSource.isPlaying)
                    {
                        effectSource.Play();
                    }
                }
            }

            LogDebug($"应用状态: {state}, 音量: {targetVolume}");
        }

        private System.Collections.IEnumerator CrossfadeAudio(ExperienceState from, ExperienceState to)
        {
            float timer = 0f;

            // 获取目标音效参数
            AudioClip targetClip = null;
            float targetVolume = 0f;
            float targetPitch = 1f;

            switch (to)
            {
                case ExperienceState.Anxious:
                    targetClip = anxiousHumClip;
                    targetVolume = anxiousHumVolume;
                    targetPitch = anxiousHumPitch;
                    break;
                case ExperienceState.Avoidant:
                    targetClip = avoidantAmbientClip;
                    targetVolume = avoidantAmbientVolume;
                    targetPitch = avoidantAmbientPitch;
                    break;
                case ExperienceState.Explorative:
                    targetClip = explorativeAmbientClip;
                    targetVolume = explorativeAmbientVolume;
                    targetPitch = explorativeAmbientPitch;
                    break;
            }

            // 淡出当前音
            float startVolume = effectSource != null ? effectSource.volume : 0f;

            while (timer < crossfadeDuration)
            {
                timer += Time.deltaTime;
                float t = timer / crossfadeDuration;

                if (effectSource != null)
                {
                    effectSource.volume = Mathf.Lerp(startVolume, targetVolume, t);
                }

                yield return null;
            }

            // 切换音频剪辑
            _currentClip = targetClip;
            _targetVolume = targetVolume;

            if (effectSource != null && targetClip != null)
            {
                effectSource.clip = targetClip;
                effectSource.pitch = targetPitch;
                effectSource.volume = targetVolume;

                if (!effectSource.isPlaying)
                {
                    effectSource.Play();
                }
            }

            LogDebug($"交叉淡入淡出完成: {to}");
        }

        private void UpdateAudio()
        {
            if (!_isInitialized || effectSource == null) return;

            // 平滑过渡音量
            _currentVolume = Mathf.Lerp(_currentVolume, _targetVolume, Time.deltaTime * volumeLerpSpeed);
            effectSource.volume = _currentVolume;
        }

        /// <summary>
        /// 播放一次性音效（UI 反馈等）
        /// </summary>
        public void PlayUISound(AudioClip clip, float volume = 1f)
        {
            if (uiSource != null && clip != null)
            {
                uiSource.PlayOneShot(clip, volume);
            }
        }

        /// <summary>
        /// 设置总静音
        /// </summary>
        public void SetMute(bool mute)
        {
            if (ambientSource != null) ambientSource.mute = mute;
            if (effectSource != null) effectSource.mute = mute;
        }

        /// <summary>
        /// 暂停所有音频
        /// </summary>
        public void PauseAll()
        {
            if (ambientSource != null) ambientSource.Pause();
            if (effectSource != null) effectSource.Pause();
        }

        /// <summary>
        /// 恢复所有音频
        /// </summary>
        public void ResumeAll()
        {
            if (ambientSource != null) ambientSource.UnPause();
            if (effectSource != null) effectSource.UnPause();
        }

        private void LogDebug(string message)
        {
            // 只在消息包含 memory 相关关键词时输出
            if (enableDebugLog && message.ToLower().Contains("memory"))
            {
                Debug.Log($"<color=#FF66B2>[Audio]</color> {message}");
            }
        }
    }
}
