using UnityEngine;
using SpaceFeedback;

namespace SpaceFeedback
{
    /// <summary>
    /// 空间反馈调试 UI - 在屏幕上显示当前状态和效果参数
    /// </summary>
    public class SpaceEffectsDebugUI : MonoBehaviour
    {
        [Header("UI 配置")]
        [Tooltip("是否显示调试 UI")]
        public bool showDebugUI = true;

        [Tooltip("UI 位置")]
        public Rect windowRect = new Rect(20, 20, 300, 380);

        [Tooltip("UI 透明度")]
        [Range(0f, 1f)]
        public float backgroundAlpha = 0.9f;

        [Header("组件引用")]
        public LightingController lightingController;
        public PostProcessingController postProcessingController;
        public AudioController audioController;

        private Vector2 _scrollPosition;
        private Texture2D _backgroundTexture;
        private Texture2D _lineTexture;

        private void Start()
        {
            // 查找引用
            if (lightingController == null)
                lightingController = FindObjectOfType<LightingController>();
            if (postProcessingController == null)
                postProcessingController = FindObjectOfType<PostProcessingController>();
            if (audioController == null)
                audioController = FindObjectOfType<AudioController>();

            // 订阅状态变化以更新 UI
            ExperienceManager.OnStateChanged += OnStateChanged;

            // 创建背景纹理
            _backgroundTexture = new Texture2D(1, 1);
            _backgroundTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.15f, backgroundAlpha));
            _backgroundTexture.Apply();
        }

        private void OnDestroy()
        {
            ExperienceManager.OnStateChanged -= OnStateChanged;
            if (_backgroundTexture != null)
                Destroy(_backgroundTexture);
            if (_lineTexture != null)
                Destroy(_lineTexture);
        }

        private void OnStateChanged(ExperienceState state)
        {
            // 状态变化时 UI 会自动更新
        }

        private void OnGUI()
        {
            if (!showDebugUI || ExperienceManager.Instance == null) return;

            GUI.backgroundColor = Color.white;
            windowRect = GUI.Window(0, windowRect, DrawDebugWindow, "Space Feedback System");
        }

        private void DrawDebugWindow(int windowID)
        {
            GUILayout.Space(5);

            // 标题
            GUILayout.Label("<size=18><b>Space Feedback System</b></size>", new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true
            });

            GUILayout.Space(5);
            DrawLine();

            // 当前状态
            DrawCurrentState();

            GUILayout.Space(5);
            DrawLine();

            // 灯光参数
            DrawLightingSection();

            GUILayout.Space(5);
            DrawLine();

            // 后处理参数
            DrawPostProcessingSection();

            GUILayout.Space(5);
            DrawLine();

            // 音频参数
            DrawAudioSection();

            GUILayout.Space(5);
            DrawLine();

            // 控制按钮
            DrawControls();

            GUI.DragWindow(new Rect(0, 0, windowRect.width, 25));
        }

        private void DrawCurrentState()
        {
            ExperienceState currentState = ExperienceManager.Instance.CurrentState;
            Color stateColor = GetStateColor(currentState);
            string stateText = GetStateText(currentState);

            GUIStyle stateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };

            GUILayout.Label($"<color=#{ColorToHex(stateColor)}><b>状态: {stateText}</b></color>", stateStyle);

            // 过渡进度条
            float progress = ExperienceManager.Instance.GetTransitionProgress();
            GUILayout.Label($"过渡进度: {progress * 100:F0}%");

            // 简单进度条
            Rect progressRect = GUILayoutUtility.GetRect(windowRect.width - 40, 16);
            GUI.Box(progressRect, "");
            Rect fillRect = new Rect(progressRect.x + 2, progressRect.y + 2, (progressRect.width - 4) * progress, progressRect.height - 4);
            GUI.color = stateColor;
            DrawSolidRect(fillRect, stateColor);
            GUI.color = Color.white;
        }

        private void DrawLightingSection()
        {
            GUILayout.Label("<b>灯光控制</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 });

            if (lightingController != null && lightingController.mainLight != null)
            {
                Light light = lightingController.mainLight;
                GUILayout.Label($"颜色: {ColorToHex(light.color)}");
                GUILayout.Label($"强度: {light.intensity:F2}");

                // 颜色预览
                GUILayout.BeginHorizontal();
                GUILayout.Label("预览: ");
                Rect colorRect = GUILayoutUtility.GetRect(60, 20);
                DrawSolidRect(colorRect, light.color);
                GUI.Box(colorRect, "");
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("未找到灯光组件");
            }
        }

        private void DrawPostProcessingSection()
        {
            GUILayout.Label("<b>后处理</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 });

            if (postProcessingController != null)
            {
                GUILayout.Label($"饱和度: {postProcessingController._currentSaturation:F2}");
                GUILayout.Label($"对比度: {postProcessingController._currentContrast:F2}");
                GUILayout.Label($"Bloom: {postProcessingController._currentBloomIntensity:F2}");
                GUILayout.Label($"晕影: {postProcessingController._currentVignetteIntensity:F2}");
            }
            else
            {
                GUILayout.Label("未找到后处理组件");
            }
        }

        private void DrawAudioSection()
        {
            GUILayout.Label("<b>音频控制</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 });

            if (audioController != null && audioController.effectSource != null)
            {
                GUILayout.Label($"音量: {audioController.effectSource.volume:F2}");
                GUILayout.Label($"播放: {(audioController.effectSource.isPlaying ? "是" : "否")}");
            }
            else
            {
                GUILayout.Label("未找到音频组件");
            }
        }

        private void DrawControls()
        {
            GUILayout.Label("<b>快速切换</b>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 13 });

            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Anxious", GUILayout.Height(30)))
            {
                ExperienceManager.Instance.ForceState(ExperienceState.Anxious);
            }

            if (GUILayout.Button("Avoidant", GUILayout.Height(30)))
            {
                ExperienceManager.Instance.ForceState(ExperienceState.Avoidant);
            }

            if (GUILayout.Button("Explorative", GUILayout.Height(30)))
            {
                ExperienceManager.Instance.ForceState(ExperienceState.Explorative);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            // 静音控制
            if (audioController != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("静音"))
                {
                    audioController.SetMute(true);
                }
                if (GUILayout.Button("取消静音"))
                {
                    audioController.SetMute(false);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(3);
            showDebugUI = GUILayout.Toggle(showDebugUI, "显示调试 UI");
        }

        private void DrawLine()
        {
            Rect rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            DrawSolidRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            if (_lineTexture == null)
                _lineTexture = new Texture2D(1, 1);
            _lineTexture.SetPixel(0, 0, color);
            _lineTexture.Apply();
            GUI.DrawTexture(rect, _lineTexture);
        }

        private Color GetStateColor(ExperienceState state)
        {
            switch (state)
            {
                case ExperienceState.Anxious:
                    return new Color(1f, 0.5f, 0.2f);
                case ExperienceState.Avoidant:
                    return new Color(0.3f, 0.7f, 1f);
                case ExperienceState.Explorative:
                    return new Color(0.3f, 1f, 0.5f);
                default:
                    return Color.white;
            }
        }

        private string GetStateText(ExperienceState state)
        {
            switch (state)
            {
                case ExperienceState.Anxious:
                    return "Anxious (焦虑)";
                case ExperienceState.Avoidant:
                    return "Avoidant (回避)";
                case ExperienceState.Explorative:
                    return "Explorative (探索)";
                default:
                    return "未知";
            }
        }

        private string ColorToHex(Color color)
        {
            return $"{ToHex(color.r)}{ToHex(color.g)}{ToHex(color.b)}";
        }

        private string ToHex(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 255).ToString("X2");
        }
    }
}
