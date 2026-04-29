using UnityEngine;
using System;
using System.Collections.Generic;
using SpaceFeedback;

/// <summary>
/// Gap Breath 主控系统
/// 整合感知、决策、记忆、响应四大模块
/// 实现完整的智能场景交互闭环
/// </summary>
public class GapBreathController : MonoBehaviour
{
    [Header("模块引用")]
    public PerceptionModule perceptionModule;
    public LLMSpaceDecision decisionModule;
    public RuntimeMemoryManager memoryModule;
    public ExperienceManager experienceManager;
    [Header("可选的响应模块（已通过 ExperienceManager 自动驱动）")]
    public LightingController lightingController;
    public PostProcessingController postProcessingController;
    public AudioController audioController;
    public CustomEventDetector customEventDetector;
    public GazeTracker gazeTracker;
    public PlayerStateTracker stateTracker;

    [Header("系统配置")]
    public bool enableSystem = true;
    public bool autoFindComponents = true;

    [Header("事件映射")]
    public bool mapCustomEventsToPerception = true;

    [Header("调试")]
    public bool enableDebugUI = true;
    public bool verboseLogging = false;

    // 内部状态
    private bool isInitialized;
    private Dictionary<string, float> eventCooldowns = new Dictionary<string, float>();
    private float uiUpdateInterval = 0.5f;
    private float uiUpdateTimer;

    // 统计
    private int totalEventsProcessed;
    private int totalDecisionsMade;
    private float sessionStartTime;

    void Start()
    {
        sessionStartTime = Time.time;
        SetupComponents();
    }

    void Update()
    {
        if (!enableSystem) return;

        UpdateCooldowns();
        UpdateDebugUI();
    }

    #region 初始化

    void SetupComponents()
    {
        if (autoFindComponents)
        {
            AutoFindComponents();
        }

        if (!ValidateComponents())
        {
            Debug.LogWarning("[GapBreath] 组件验证失败，部分功能可能不可用");
            return;
        }

        SubscribeToEvents();
        isInitialized = true;

        Debug.Log("<color=#00CED1>[GapBreath]</color> 系统初始化完成");
    }

    void AutoFindComponents()
    {
        // 感知模块
        if (perceptionModule == null)
            perceptionModule = FindObjectOfType<PerceptionModule>();
        if (perceptionModule == null)
            perceptionModule = gameObject.AddComponent<PerceptionModule>();

        // 决策模块
        if (decisionModule == null)
            decisionModule = FindObjectOfType<LLMSpaceDecision>();
        if (decisionModule == null)
            decisionModule = gameObject.AddComponent<LLMSpaceDecision>();

        // 记忆模块
        if (memoryModule == null)
            memoryModule = FindObjectOfType<RuntimeMemoryManager>();
        if (memoryModule == null)
            memoryModule = gameObject.AddComponent<RuntimeMemoryManager>();

        // 体验管理器（核心状态管理器）
        if (experienceManager == null)
            experienceManager = FindObjectOfType<ExperienceManager>();
        if (experienceManager == null)
            experienceManager = gameObject.AddComponent<ExperienceManager>();

        // 灯光控制器
        if (lightingController == null)
            lightingController = FindObjectOfType<LightingController>();
        if (lightingController == null)
            lightingController = FindObjectOfType<LightingController>();

        // 后处理控制器
        if (postProcessingController == null)
            postProcessingController = FindObjectOfType<PostProcessingController>();
        if (postProcessingController == null)
            postProcessingController = FindObjectOfType<PostProcessingController>();

        // 音频控制器
        if (audioController == null)
            audioController = FindObjectOfType<AudioController>();
        if (audioController == null)
            audioController = FindObjectOfType<AudioController>();

        // 自定义事件检测
        if (customEventDetector == null)
            customEventDetector = FindObjectOfType<CustomEventDetector>();
        if (customEventDetector == null)
            customEventDetector = gameObject.AddComponent<CustomEventDetector>();

        // 注视追踪
        if (gazeTracker == null)
            gazeTracker = FindObjectOfType<GazeTracker>();
        if (gazeTracker == null)
            gazeTracker = gameObject.AddComponent<GazeTracker>();

        // 状态追踪
        if (stateTracker == null)
            stateTracker = FindObjectOfType<PlayerStateTracker>();
        if (stateTracker == null)
            stateTracker = gameObject.AddComponent<PlayerStateTracker>();
    }

    bool ValidateComponents()
    {
        bool valid = true;

        if (perceptionModule == null)
        {
            Debug.LogError("[GapBreath] PerceptionModule 缺失");
            valid = false;
        }

        if (decisionModule == null)
        {
            Debug.LogError("[GapBreath] LLMSpaceDecision 缺失");
            valid = false;
        }

        if (memoryModule == null)
        {
            Debug.LogError("[GapBreath] RuntimeMemoryManager 缺失");
            valid = false;
        }

        // ExperienceManager 是可选的，因为控制器会自己订阅
        if (experienceManager == null)
        {
            Debug.LogWarning("[GapBreath] ExperienceManager 未设置，将自动查找");
            experienceManager = FindObjectOfType<ExperienceManager>();
        }

        return valid;
    }

    void SubscribeToEvents()
    {
        // 感知事件 -> 注入决策
        if (perceptionModule != null)
        {
            perceptionModule.OnPerceptionEvent += HandlePerceptionEvent;
        }

        // 自定义事件 -> 映射到感知
        if (customEventDetector != null && mapCustomEventsToPerception)
        {
            customEventDetector.OnNearWall += (duration) =>
            {
                var evt = new PerceptionEvent
                {
                    EventType = "NearWall",
                    Zone = perceptionModule?.CurrentZone ?? "",
                    Value = duration,
                    Description = $"靠近墙体持续{duration:F1}秒"
                };
                HandlePerceptionEvent(evt);
            };

            customEventDetector.OnGazeDamageCorner += () =>
            {
                var evt = new PerceptionEvent
                {
                    EventType = "GazeDamageCorner",
                    Zone = perceptionModule?.CurrentZone ?? "",
                    Value = gazeTracker != null ? gazeTracker.GazeDuration : 3f,
                    Description = $"凝视破损角落 时长{gazeTracker?.GazeDuration:F1}秒"
                };
                HandlePerceptionEvent(evt);
            };
        }

        // 决策结果 -> 执行响应
        if (decisionModule != null)
        {
            decisionModule.OnDecisionMade += HandleDecision;
        }
    }

    #endregion

    #region 事件处理

    void HandlePerceptionEvent(PerceptionEvent perceptionEvent)
    {
        if (!isInitialized || !enableSystem) return;

        totalEventsProcessed++;

        // 检查冷却
        if (IsOnCooldown(perceptionEvent.EventType))
        {
            LogVerbose($"事件 {perceptionEvent.EventType} 在冷却中");
            return;
        }

        // 注入到决策系统
        decisionModule?.InjectEvent(perceptionEvent);

        // 立即响应某些紧急事件
        switch (perceptionEvent.EventType)
        {
            case "FastMove":
                HandleFastMove();
                break;
            case "GazeDamageCorner":
                HandleGazeDamageCorner(perceptionEvent);
                break;
            case "NearWall":
                HandleNearWall(perceptionEvent);
                break;
        }

        LogDebug($"感知事件: {perceptionEvent}");
    }

    void HandleDecision(DecisionResult result)
    {
        if (!isInitialized || !enableSystem) return;

        totalDecisionsMade++;

        // 通过 ExperienceManager 驱动响应系统
        if (experienceManager != null)
        {
            switch (result.Emotion?.ToLower())
            {
                case "anxious":
                    experienceManager.TransitionTo(ExperienceState.Anxious);
                    break;
                case "avoidant":
                case "calm":
                    experienceManager.TransitionTo(ExperienceState.Avoidant);
                    break;
                case "resonant":
                case "explorative":
                    experienceManager.TransitionTo(ExperienceState.Explorative);
                    break;
                default:
                    experienceManager.TransitionTo(ExperienceState.Avoidant);
                    break;
            }
        }

        // 记录到记忆
        memoryModule?.AppendSummary(result.Summary);

        LogDebug($"决策结果: {result.Emotion} ({result.Intensity:P0}) - {result.Summary}");
    }

    #endregion

    #region 特殊事件处理

    void HandleFastMove()
    {
        SetCooldown("FastMove", 3f);
        experienceManager?.TransitionTo(ExperienceState.Anxious);
        LogDebug("快速移动检测 -> 触发焦虑响应");
    }

    void HandleGazeDamageCorner(PerceptionEvent evt)
    {
        SetCooldown("GazeDamageCorner", 5f);
        experienceManager?.TransitionTo(ExperienceState.Explorative);
        LogDebug("凝视破损角落 -> 触发共鸣响应");
    }

    void HandleNearWall(PerceptionEvent evt)
    {
        SetCooldown("NearWall", 4f);
        experienceManager?.TransitionTo(ExperienceState.Avoidant);
        LogDebug("靠近墙体 -> 触发平静响应");
    }

    void ExecuteDefaultResponse(DecisionResult result)
    {
        if (result.IsAnxious)
        {
            experienceManager?.TransitionTo(ExperienceState.Anxious);
        }
        else if (result.IsResonant)
        {
            experienceManager?.TransitionTo(ExperienceState.Explorative);
        }
        else
        {
            experienceManager?.TransitionTo(ExperienceState.Avoidant);
        }
    }

    #endregion

    #region 冷却管理

    void UpdateCooldowns()
    {
        var keysToRemove = new List<string>();
        foreach (var kvp in eventCooldowns)
        {
            if (Time.time > kvp.Value)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            eventCooldowns.Remove(key);
        }
    }

    bool IsOnCooldown(string eventType)
    {
        if (eventCooldowns.TryGetValue(eventType, out float endTime))
        {
            return Time.time < endTime;
        }
        return false;
    }

    void SetCooldown(string eventType, float duration)
    {
        eventCooldowns[eventType] = Time.time + duration;
    }

    #endregion

    #region 调试UI

    void UpdateDebugUI()
    {
        if (!enableDebugUI) return;

        uiUpdateTimer += Time.deltaTime;
        if (uiUpdateTimer >= uiUpdateInterval)
        {
            uiUpdateTimer = 0f;
            UpdateDebugPanel();
        }
    }

    void UpdateDebugPanel()
    {
        // 可以在此更新Canvas上的调试信息
        // 简单实现为Debug输出
    }

    void OnGUI()
    {
        if (!enableDebugUI || !isInitialized) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, Screen.height - 20));
        GUILayout.BeginVertical("box");

        GUILayout.Label($"<color=#00CED1><b>Gap Breath System</b></color>", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
        GUILayout.Space(5);

        GUILayout.Label($"系统状态: {(enableSystem ? "<color=green>运行中</color>" : "<color=red>暂停</color>")}", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"运行时长: {Time.time - sessionStartTime:F1}s");
        GUILayout.Label($"处理事件: {totalEventsProcessed}");
        GUILayout.Label($"决策次数: {totalDecisionsMade}");

        GUILayout.Space(10);

        if (perceptionModule != null)
        {
            GUILayout.Label("<b>感知状态</b>");
            GUILayout.Label($"当前区域: {perceptionModule.CurrentZone ?? "无"}");
            GUILayout.Label($"停留时长: {perceptionModule.ZoneStayDuration:F1}s");
            GUILayout.Label($"快速移动: {perceptionModule.IsFastMoving}");
            GUILayout.Label($"靠近墙体: {perceptionModule.IsNearWall}");
            GUILayout.Label($"凝视中: {perceptionModule.IsGazing}");
        }

        GUILayout.Space(10);

        if (gazeTracker != null)
        {
            GUILayout.Label("<b>注视状态</b>");
            GUILayout.Label($"注视方向: {gazeTracker.CurrentGazeDirection}");
            GUILayout.Label($"注视时长: {gazeTracker.GazeDuration:F1}s");
        }

        GUILayout.Space(10);

        if (memoryModule != null)
        {
            var stats = memoryModule.GetStats();
            GUILayout.Label("<b>记忆状态</b>");
            GUILayout.Label($"记忆条数: {stats.TotalEntries}");
            GUILayout.Label($"主要情绪: {stats.MostCommonFeeling}");
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }

    #endregion

    #region 公共方法

    /// <summary>启用/禁用系统</summary>
    public void SetEnabled(bool enabled)
    {
        enableSystem = enabled;
        LogDebug($"系统{(enabled ? "启用" : "禁用")}");
    }

    /// <summary>强制触发一次决策</summary>
    public void ForceDecision()
    {
        decisionModule?.ForceProcess();
    }

    /// <summary>注入自定义感知事件</summary>
    public void InjectCustomEvent(string eventType, string zone, float value)
    {
        var evt = new PerceptionEvent
        {
            EventType = eventType,
            Zone = zone,
            Value = value,
            Description = $"{eventType} in {zone}"
        };
        HandlePerceptionEvent(evt);
    }

    /// <summary>执行指定情感响应</summary>
    public void TriggerEmotion(string emotion, float intensity = 0.7f)
    {
        switch (emotion.ToLower())
        {
            case "anxious":
                experienceManager?.TransitionTo(ExperienceState.Anxious);
                break;
            case "avoidant":
            case "calm":
                experienceManager?.TransitionTo(ExperienceState.Avoidant);
                break;
            case "resonant":
            case "explorative":
                experienceManager?.TransitionTo(ExperienceState.Explorative);
                break;
            default:
                experienceManager?.TransitionTo(ExperienceState.Avoidant);
                break;
        }
    }

    /// <summary>获取系统报告</summary>
    public string GetSystemReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Gap Breath System Report ===");
        sb.AppendLine($"运行时长: {Time.time - sessionStartTime:F1}s");
        sb.AppendLine($"处理事件: {totalEventsProcessed}");
        sb.AppendLine($"决策次数: {totalDecisionsMade}");

        if (memoryModule != null)
        {
            sb.AppendLine("\n=== Memory ===");
            sb.AppendLine(memoryModule.ExportAsText());
        }

        return sb.ToString();
    }

    #endregion

    #region 日志

    void LogDebug(string message)
    {
        // 只显示 memory 相关日志
        if (message.ToLower().Contains("memory"))
        {
            Debug.Log($"<color=#00CED1>[GapBreath]</color> {message}");
        }
    }

    void LogVerbose(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"<color=#808080>[GapBreath/Verbose]</color> {message}");
        }
    }

    #endregion
}
