using UnityEngine;

namespace SpaceFeedback
{
    /// <summary>
    /// 灯光控制器 - 控制场景主光源
    /// 根据 ExperienceState 改变灯光颜色、强度和氛围
    /// </summary>
    public class LightingController : MonoBehaviour
    {
        [Header("目标灯光")]
        [Tooltip("场景主方向光")]
        public Light mainLight;

        [Header("Anxious 状态（暖色、低强度）")]
        public Color anxiousColor = new Color(1f, 0.7f, 0.4f, 1f);      // 偏橙暖色
        public float anxiousIntensity = 0.6f;
        public float anxiousShadowIntensity = 0.3f;

        [Header("Avoidant 状态（高亮、白色）")]
        public Color avoidantColor = new Color(1f, 1f, 0.95f, 1f);       // 接近白色
        public float avoidantIntensity = 1.2f;
        public float avoidantShadowIntensity = 0.8f;

        [Header("Explorative 状态（高对比、色彩变化）")]
        public Color explorativeColor = new Color(0.8f, 0.95f, 1f, 1f);  // 冷蓝色调
        public float explorativeIntensity = 0.9f;
        public float explorativeShadowIntensity = 0.6f;

        [Header("过渡设置")]
        [Tooltip("颜色过渡速度")]
        public float colorLerpSpeed = 3f;
        [Tooltip("强度过渡速度")]
        public float intensityLerpSpeed = 3f;

        [Header("额外效果")]
        [Tooltip("是否启用体积光（需要相关后处理）")]
        public bool enableVolumetricLight = false;
        [Tooltip("闪烁效果强度")]
        [Range(0f, 1f)]
        public float flickerIntensity = 0.1f;

        // 内部状态
        private ExperienceState _currentState;
        private Color _currentColor;
        private float _currentIntensity;
        private float _targetIntensity;
        private float _flickerTimer;
        private float _originalIntensity;

        private void Start()
        {
            // 订阅状态变化事件
            ExperienceManager.OnStateChanged += HandleStateChange;
            ExperienceManager.OnStateTransitionStart += HandleTransitionStart;

            // 初始化
            if (mainLight == null)
            {
                mainLight = FindObjectOfType<Light>();
            }

            if (mainLight != null)
            {
                _originalIntensity = mainLight.intensity;
                _currentColor = mainLight.color;
                _currentIntensity = mainLight.intensity;
            }

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
            // 平滑过渡颜色
            UpdateLighting();
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
            Color targetColor;
            float targetIntensity;

            switch (state)
            {
                case ExperienceState.Anxious:
                    targetColor = anxiousColor;
                    targetIntensity = anxiousIntensity;
                    break;

                case ExperienceState.Avoidant:
                    targetColor = avoidantColor;
                    targetIntensity = avoidantIntensity;
                    break;

                case ExperienceState.Explorative:
                    targetColor = explorativeColor;
                    targetIntensity = explorativeIntensity;
                    break;

                default:
                    targetColor = anxiousColor;
                    targetIntensity = anxiousIntensity;
                    break;
            }

            if (instant)
            {
                _currentColor = targetColor;
                _currentIntensity = targetIntensity;
                _targetIntensity = targetIntensity;

                if (mainLight != null)
                {
                    mainLight.color = _currentColor;
                    mainLight.intensity = _currentIntensity;
                }
            }
            else
            {
                _targetIntensity = targetIntensity;
            }

            Debug.Log($"[LightingController] 应用状态: {state}, 目标颜色: {targetColor}, 目标强度: {targetIntensity}");
            // Memory 相关日志已过滤，如需调试请取消上面这行注释
        }

        private void UpdateLighting()
        {
            if (mainLight == null) return;

            // Lerp 颜色
            Color targetColor;
            switch (_currentState)
            {
                case ExperienceState.Anxious:
                    targetColor = anxiousColor;
                    break;
                case ExperienceState.Avoidant:
                    targetColor = avoidantColor;
                    break;
                case ExperienceState.Explorative:
                    targetColor = explorativeColor;
                    break;
                default:
                    targetColor = anxiousColor;
                    break;
            }

            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * colorLerpSpeed);
            mainLight.color = _currentColor;

            // Lerp 强度
            _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, Time.deltaTime * intensityLerpSpeed);

            // 添加轻微闪烁效果
            float flicker = 0f;
            if (_currentState == ExperienceState.Anxious && flickerIntensity > 0)
            {
                _flickerTimer += Time.deltaTime;
                flicker = Mathf.Sin(_flickerTimer * 10f) * flickerIntensity * 0.1f;
            }

            mainLight.intensity = _currentIntensity + flicker;
        }

        /// <summary>
        /// 手动设置灯光颜色（覆盖状态逻辑）
        /// </summary>
        public void SetLightColor(Color color, float intensity)
        {
            if (mainLight != null)
            {
                mainLight.color = color;
                mainLight.intensity = intensity;
            }
        }

        /// <summary>
        /// 启用/禁用灯光
        /// </summary>
        public void SetLightEnabled(bool enabled)
        {
            if (mainLight != null)
            {
                mainLight.enabled = enabled;
            }
        }

        /// <summary>
        /// 获取当前状态对应的阴影强度
        /// </summary>
        private float GetShadowIntensity(ExperienceState state)
        {
            switch (state)
            {
                case ExperienceState.Anxious:
                    return anxiousShadowIntensity;
                case ExperienceState.Avoidant:
                    return avoidantShadowIntensity;
                case ExperienceState.Explorative:
                    return explorativeShadowIntensity;
                default:
                    return anxiousShadowIntensity;
            }
        }
    }
}
