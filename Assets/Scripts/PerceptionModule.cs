using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 感知模块：整合区域进入、停留时长、快速移动、注视方向的检测
/// 生成感知事件供LLM决策系统使用
/// </summary>
public class PerceptionModule : MonoBehaviour
{
    [Header("区域定义")]
    public ZoneDefinition[] zones = new ZoneDefinition[]
    {
        new ZoneDefinition { zoneId = "A", zoneName = "边缘墙体区", description = "靠墙、贴边、试探性移动" },
        new ZoneDefinition { zoneId = "B", zoneName = "中心空地", description = "步速、转头、停顿最明显" },
        new ZoneDefinition { zoneId = "C", zoneName = "角落残损区", description = "长时间注视或蹲下观察" }
    };

    [Header("行为检测阈值")]
    public float fastMoveSpeed = 1.5f;
    public float fastMoveHoldTime = 1.0f;
    public float lingerTime = 4f;
    public float nearWallDistance = 0.8f;
    public float gazeThreshold = 3f;

    [Header("组件引用")]
    public PlayerStateTracker stateTracker;
    public GazeTracker gazeTracker;
    public Transform playerRoot;

    [Header("调试")]
    public bool enableDebugLog = true;

    // 感知事件
    public event Action<PerceptionEvent> OnPerceptionEvent;

    // 当前感知状态
    public string CurrentZone { get; private set; }
    public float ZoneStayDuration { get; private set; }
    public bool IsNearWall { get; private set; }
    public bool IsFastMoving { get; private set; }
    public bool IsGazing { get; private set; }
    public float GazeDuration { get; private set; }
    public Vector3 CurrentGazeDirection { get; private set; }

    // 内部状态
    private string lastZone;
    private float zoneEnterTime;
    private float fastMoveTimer;
    private float wallProximityTimer;
    private bool wallProximityTriggered;
    private bool fastMoveTriggered;
    private bool gazeTriggered;
    private bool lingeringTriggered;  // 新增：追踪Lingering事件是否已触发
    private const float WALL_PROXIMITY_THRESHOLD = 2f;

    void Start()
    {
        SetupReferences();
    }

    void SetupReferences()
    {
        if (stateTracker == null)
            stateTracker = FindObjectOfType<PlayerStateTracker>();

        if (gazeTracker == null)
            gazeTracker = FindObjectOfType<GazeTracker>();

        if (playerRoot == null)
        {
            var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
            playerRoot = xrOrigin != null ? xrOrigin.transform : transform;
        }
    }

    private void Update()
    {
        UpdatePerception();
    }

    void UpdatePerception()
    {
        // 1. 检测区域
        DetectZone();

        // 2. 检测快速移动
        DetectFastMove();

        // 3. 检测靠墙状态
        DetectWallProximity();

        // 4. 检测注视状态
        UpdateGazeState();
    }

    void DetectZone()
    {
        var zoneTriggers = FindObjectsOfType<ZoneTrigger>();
        string currentZoneId = "";
        float longestStay = 0f;

        foreach (var trigger in zoneTriggers)
        {
            if (trigger.IsPlayerInside && trigger.StayDuration > longestStay)
            {
                currentZoneId = trigger.zoneName;
                longestStay = trigger.StayDuration;
            }
        }

        if (currentZoneId != lastZone)
        {
            lingeringTriggered = false; // 重置停留触发状态
            
            if (!string.IsNullOrEmpty(lastZone))
            {
                RaisePerceptionEvent("ZoneExit", lastZone, 0f);
            }

            lastZone = currentZoneId;
            CurrentZone = currentZoneId;
            zoneEnterTime = Time.time;
            ZoneStayDuration = 0f;

            if (!string.IsNullOrEmpty(currentZoneId))
            {
                RaisePerceptionEvent("ZoneEnter", currentZoneId, 0f);
                LogDebug($"进入区域: {currentZoneId}");
            }
        }
        else if (!string.IsNullOrEmpty(currentZoneId))
        {
            ZoneStayDuration = Time.time - zoneEnterTime;

            if (ZoneStayDuration >= lingerTime && !lingeringTriggered)
            {
                lingeringTriggered = true;
                RaisePerceptionEvent("Lingering", currentZoneId, ZoneStayDuration);
            }
        }
    }

    void DetectFastMove()
    {
        if (stateTracker == null) return;

        if (stateTracker.CurrentSpeed > fastMoveSpeed)
        {
            fastMoveTimer += Time.deltaTime;
            if (fastMoveTimer >= fastMoveHoldTime && !fastMoveTriggered)
            {
                fastMoveTriggered = true;
                IsFastMoving = true;
                RaisePerceptionEvent("FastMove", CurrentZone, stateTracker.CurrentSpeed);
                LogDebug($"快速移动: 速度 {stateTracker.CurrentSpeed:F2}");
            }
        }
        else
        {
            fastMoveTimer = 0f;
            if (fastMoveTriggered)
            {
                fastMoveTriggered = false;
                IsFastMoving = false;
            }
        }
    }

    void DetectWallProximity()
    {
        if (playerRoot == null) return;

        Collider[] colliders = Physics.OverlapSphere(playerRoot.position, nearWallDistance);
        bool nearWall = false;

        foreach (var col in colliders)
        {
            if (IsWall(col.gameObject))
            {
                nearWall = true;
                break;
            }
        }

        if (nearWall && !IsNearWall)
        {
            wallProximityTimer += Time.deltaTime;
            if (wallProximityTimer >= WALL_PROXIMITY_THRESHOLD && !wallProximityTriggered)
            {
                wallProximityTriggered = true;
                IsNearWall = true;
                RaisePerceptionEvent("NearWall", CurrentZone, wallProximityTimer);
                LogDebug($"靠近墙体: 持续 {wallProximityTimer:F1}s");
            }
        }
        else if (!nearWall)
        {
            wallProximityTimer = 0f;
            if (IsNearWall)
            {
                IsNearWall = false;
                wallProximityTriggered = false;
            }
        }
    }

    bool IsWall(GameObject obj)
    {
        if (obj.CompareTag("Wall") || obj.CompareTag("Boundary"))
            return true;

        string name = obj.name.ToLower();
        if (name.Contains("wall") || name.Contains("墙"))
            return true;

        return false;
    }

    void UpdateGazeState()
    {
        if (gazeTracker == null) return;

        CurrentGazeDirection = gazeTracker.CurrentGazeDirection;
        GazeDuration = gazeTracker.GazeDuration;
        IsGazing = gazeTracker.IsGazing;

        // 检测凝视破损角落（自定义事件2）
        if (GazeDuration >= gazeThreshold && !gazeTriggered)
        {
            gazeTriggered = true;
            RaisePerceptionEvent("GazeDamageCorner", CurrentZone, GazeDuration);
            LogDebug($"长时间凝视: {GazeDuration:F1}s");
        }
        else if (GazeDuration < 0.1f)
        {
            gazeTriggered = false;
        }
    }

    void RaisePerceptionEvent(string eventType, string zone, float value)
    {
        var perceptionEvent = new PerceptionEvent
        {
            EventType = eventType,
            Zone = zone ?? "",
            Value = value,
            Timestamp = Time.time,
            Description = GetEventDescription(eventType, zone, value)
        };

        OnPerceptionEvent?.Invoke(perceptionEvent);
    }

    string GetEventDescription(string eventType, string zone, float value)
    {
        switch (eventType)
        {
            case "ZoneEnter": return $"进入区域{zone}";
            case "ZoneExit": return $"离开区域{zone}";
            case "Lingering": return $"在区域{zone}停留{value:F1}s";
            case "FastMove": return $"快速移动 速度:{value:F2}";
            case "NearWall": return $"靠近墙体 持续{value:F1}s";
            case "GazeDamageCorner": return $"凝视破损角落 {value:F1}s";
            default: return $"{eventType} in {zone}";
        }
    }

    void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"<color=#90EE90>[Perception]</color> {message}");
        }
    }

    /// <summary>获取当前感知状态摘要</summary>
    public string GetPerceptionSummary()
    {
        return $"区域:{CurrentZone ?? "无"} | 停留:{ZoneStayDuration:F1}s | " +
               $"快速:{IsFastMoving} | 靠墙:{IsNearWall} | 凝视:{IsGazing}";
    }

    /// <summary>重置所有状态</summary>
    public void Reset()
    {
        lastZone = null;
        CurrentZone = null;
        fastMoveTimer = 0f;
        fastMoveTriggered = false;
        wallProximityTimer = 0f;
        wallProximityTriggered = false;
        gazeTriggered = false;
        lingeringTriggered = false;
    }
}

/// <summary>
/// 区域定义
/// </summary>
[Serializable]
public class ZoneDefinition
{
    public string zoneId;
    public string zoneName;
    public string description;
}

/// <summary>
/// 感知事件数据结构
/// </summary>
[Serializable]
public class PerceptionEvent
{
    public string EventType;
    public string Zone;
    public float Value;
    public float Timestamp;
    public string Description;

    public override string ToString()
    {
        return $"{EventType} | {Zone} | {Value:F2} | {Description}";
    }
}
