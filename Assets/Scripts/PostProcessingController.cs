using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceFeedback
{
    /// <summary>
    /// 后处理控制器 - 使用 URP Volume 控制视觉效果
    /// 根据 ExperienceState 改变 Bloom、色彩调整、晕影等效果
    /// </summary>
    public class PostProcessingController : MonoBehaviour
    {
        [Header("目标后处理")]
        [Tooltip("URP Volume 组件")]
        public Volume volume;

        [Header("Anxious 状态（低对比、轻微模糊感）")]
        public float anxiousSaturation = 0.7f;
        public float anxiousContrast = 0.8f;
        public float anxiousBloomIntensity = 0.4f;
        public float anxiousVignetteIntensity = 0.3f;
        public Color anxiousColorGrade = new Color(1f, 0.85f, 0.7f, 1f);

        [Header("Avoidant 状态（去饱和、高亮）")]
        public float avoidantSaturation = 0.3f;
        public float avoidantContrast = 0.9f;
        public float avoidantBloomIntensity = 0.6f;
        public float avoidantVignetteIntensity = 0.5f;
        public Color avoidantColorGrade = new Color(1f, 1f, 1f, 1f);

        [Header("Explorative 状态（高对比、高饱和）")]
        public float explorativeSaturation = 1.3f;
        public float explorativeContrast = 1.2f;
        public float explorativeBloomIntensity = 0.3f;
        public float explorativeVignetteIntensity = 0.4f;
        public Color explorativeColorGrade = new Color(0.85f, 0.95f, 1.1f, 1f);

        [Header("过渡设置")]
        [Tooltip("参数过渡速度")]
        public float lerpSpeed = 3f;

        [Header("调试")]
        public bool enableDebugLog = true;

        // 后处理效果引用
        private Bloom _bloom;
        private ColorAdjustments _colorAdjustments;
        private Vignette _vignette;

        // 当前值 (public 供调试 UI 访问)
        public float _currentSaturation;
        public float _currentContrast;
        public float _currentBloomIntensity;
        public float _currentVignetteIntensity;
        public Color _currentColorGrade;

        // 目标值
        private float _targetSaturation;
        private float _targetContrast;
        private float _targetBloomIntensity;
        private float _targetVignetteIntensity;
        private Color _targetColorGrade;

        private ExperienceState _currentState;
        private bool _isInitialized;

        private void Start()
        {
            // 订阅状态变化
            ExperienceManager.OnStateChanged += HandleStateChange;
            ExperienceManager.OnStateTransitionStart += HandleTransitionStart;

            // 初始化后处理引用
            InitializePostProcessing();

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
            UpdatePostProcessing();
        }

        private void InitializePostProcessing()
        {
            if (volume == null)
            {
                volume = FindObjectOfType<Volume>();
            }

            if (volume == null)
            {
                Debug.LogWarning("[PostProcessingController] 未找到 Volume 组件！请确保场景中有 URP Volume。");
                return;
            }

            // 获取或创建效果
            if (!volume.profile.TryGet<Bloom>(out _bloom))
            {
                _bloom = volume.profile.Add<Bloom>(false);
            }

            if (!volume.profile.TryGet<ColorAdjustments>(out _colorAdjustments))
            {
                _colorAdjustments = volume.profile.Add<ColorAdjustments>(false);
            }

            if (!volume.profile.TryGet<Vignette>(out _vignette))
            {
                _vignette = volume.profile.Add<Vignette>(false);
            }

            // 初始化当前值
            if (_colorAdjustments != null)
            {
                _currentSaturation = _colorAdjustments.saturation.value;
                _currentContrast = _colorAdjustments.contrast.value;
                _currentColorGrade = _colorAdjustments.colorFilter.value;
            }

            if (_bloom != null)
            {
                _currentBloomIntensity = _bloom.intensity.value;
            }

            if (_vignette != null)
            {
                _currentVignetteIntensity = _vignette.intensity.value;
            }

            _isInitialized = true;
            LogDebug("后处理控制器初始化完成");
        }

        private void HandleStateChange(ExperienceState newState)
        {
            _currentState = newState;
            ApplyState(newState, false);
        }

        private void HandleTransitionStart(ExperienceState from, ExperienceState to)
        {
            _currentState = to;
        }

        private void ApplyState(ExperienceState state, bool instant)
        {
            switch (state)
            {
                case ExperienceState.Anxious:
                    _targetSaturation = anxiousSaturation;
                    _targetContrast = anxiousContrast;
                    _targetBloomIntensity = anxiousBloomIntensity;
                    _targetVignetteIntensity = anxiousVignetteIntensity;
                    _targetColorGrade = anxiousColorGrade;
                    break;

                case ExperienceState.Avoidant:
                    _targetSaturation = avoidantSaturation;
                    _targetContrast = avoidantContrast;
                    _targetBloomIntensity = avoidantBloomIntensity;
                    _targetVignetteIntensity = avoidantVignetteIntensity;
                    _targetColorGrade = avoidantColorGrade;
                    break;

                case ExperienceState.Explorative:
                    _targetSaturation = explorativeSaturation;
                    _targetContrast = explorativeContrast;
                    _targetBloomIntensity = explorativeBloomIntensity;
                    _targetVignetteIntensity = explorativeVignetteIntensity;
                    _targetColorGrade = explorativeColorGrade;
                    break;
            }

            if (instant)
            {
                _currentSaturation = _targetSaturation;
                _currentContrast = _targetContrast;
                _currentBloomIntensity = _targetBloomIntensity;
                _currentVignetteIntensity = _targetVignetteIntensity;
                _currentColorGrade = _targetColorGrade;
                ApplyToPostProcessing();
            }

            LogDebug($"应用状态: {state}");
        }

        private void UpdatePostProcessing()
        {
            if (!_isInitialized) return;

            // 使用 SmoothStep 进行平滑过渡
            float t = Time.deltaTime * lerpSpeed;

            _currentSaturation = Mathf.Lerp(_currentSaturation, _targetSaturation, t);
            _currentContrast = Mathf.Lerp(_currentContrast, _targetContrast, t);
            _currentBloomIntensity = Mathf.Lerp(_currentBloomIntensity, _targetBloomIntensity, t);
            _currentVignetteIntensity = Mathf.Lerp(_currentVignetteIntensity, _targetVignetteIntensity, t);
            _currentColorGrade = Color.Lerp(_currentColorGrade, _targetColorGrade, t);

            ApplyToPostProcessing();
        }

        private void ApplyToPostProcessing()
        {
            if (_colorAdjustments != null)
            {
                _colorAdjustments.saturation.value = _currentSaturation;
                _colorAdjustments.contrast.value = _currentContrast;
                _colorAdjustments.colorFilter.value = _currentColorGrade;
            }

            if (_bloom != null)
            {
                _bloom.intensity.value = _currentBloomIntensity;
            }

            if (_vignette != null)
            {
                _vignette.intensity.value = _currentVignetteIntensity;
            }
        }

        /// <summary>
        /// 手动设置所有参数（覆盖状态逻辑）
        /// </summary>
        public void SetParameters(float saturation, float contrast, float bloom, float vignette)
        {
            _targetSaturation = saturation;
            _targetContrast = contrast;
            _targetBloomIntensity = bloom;
            _targetVignetteIntensity = vignette;
        }

        /// <summary>
        /// 启用/禁用后处理
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (volume != null)
            {
                volume.enabled = enabled;
            }
        }

        private void LogDebug(string message)
        {
            // 只在消息包含 memory 相关关键词时输出
            if (enableDebugLog && message.ToLower().Contains("memory"))
            {
                Debug.Log($"<color=#66B3FF>[PostProcessing]</color> {message}");
            }
        }
    }
}
