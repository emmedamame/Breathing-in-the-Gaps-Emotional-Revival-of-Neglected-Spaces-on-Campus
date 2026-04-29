using UnityEngine;
using SpaceFeedback;

/// <summary>
/// Demo 演示控制器：整合玩家追踪、区域触发、空间响应
/// 已适配 ExperienceManager 架构
/// </summary>
public class DemoController : MonoBehaviour
{
    [Header("组件引用")]
    public PlayerStateTracker stateTracker;
    public ExperienceManager experienceManager;
    public DemoMemoryLogger memoryLogger;

    [Header("区域引用")]
    public ZoneTrigger zoneWallTrace;
    public ZoneTrigger zoneDoorNote;
    public ZoneTrigger zoneLightView;

    [Header("行为检测开关")]
    public bool enableFastPass = true;
    public bool enableWallPause = true;
    public bool enableLookUp = true;

    [Header("========== 调试变量（Public，供Inspector调整）==========")]
    [Header("相机状态")]
    public Vector3 cameraPosition;
    public Vector3 cameraRotation;
    
    [Header("移动状态")]
    public float currentSpeed;
    public float speedThreshold = 0.3f;
    
    [Header("停留时间")]
    public float currentStayDuration;
    
    [Header("距墙距离")]
    public float distanceToWall;
    public float wallDistanceThreshold = 0.5f;
    
    [Header("头部姿态")]
    public float currentPitch;
    public float lookDownThreshold = 30f;
    public bool isLookingDown;

    [Header("========== 靠墙触发条件（可调整）==========")]
    [Header("必须同时满足以下条件才能触发:")]
    public float requiredWallDistance = 0.5f;
    public float requiredPitchAngle = 30f;
    public float requiredStayTime = 2f;
    public float requiredSlowSpeed = 0.3f;

    private bool fastPassTriggered;
    private bool wallPauseTriggered;
    private bool lookUpTriggered;

    private float fastPassCooldown = 5f;
    private float wallPauseCooldown = 5f;
    private float lookUpCooldown = 5f;

    private void Start()
    {
        SetupDefaultComponents();
        SetupZoneReferences();
        SubscribeToEvents();
    }

    void SetupDefaultComponents()
    {
        // 自动查找或创建
        if (stateTracker == null)
            stateTracker = FindObjectOfType<PlayerStateTracker>();
        if (experienceManager == null)
            experienceManager = FindObjectOfType<ExperienceManager>();
        if (memoryLogger == null)
            memoryLogger = FindObjectOfType<DemoMemoryLogger>();

        // 创建默认组件
        GameObject managerObj = gameObject;

        if (stateTracker == null)
            stateTracker = managerObj.AddComponent<PlayerStateTracker>();
        if (experienceManager == null)
            experienceManager = managerObj.AddComponent<ExperienceManager>();
        if (memoryLogger == null)
            memoryLogger = managerObj.AddComponent<DemoMemoryLogger>();
    }

    void SetupZoneReferences()
    {
        // 自动查找所有 ZoneTrigger
        var allZones = FindObjectsOfType<ZoneTrigger>();

        foreach (var zone in allZones)
        {
            if (zone == null) continue;

            string name = zone.zoneName.ToUpper();

            if (name.Contains("WALLTRACE") && zoneWallTrace == null)
                zoneWallTrace = zone;
            else if (name.Contains("DOORNOTE") && zoneDoorNote == null)
                zoneDoorNote = zone;
            else if (name.Contains("LIGHTVIEW") && zoneLightView == null)
                zoneLightView = zone;
        }

        // 调试：输出找到的区域
        Debug.Log($"[Demo] 找到区域: WallTrace={zoneWallTrace != null}, DoorNote={zoneDoorNote != null}, LightView={zoneLightView != null}");
    }

    void SubscribeToEvents()
    {
        // 门贴纸区域（可扩展）
        if (zoneDoorNote != null)
        {
            zoneDoorNote.OnStayComplete += (zoneName) =>
            {
                Debug.Log($"[Demo] 进入门贴纸区域: {zoneName}");
            };
        }
    }

    private void Update()
    {
        UpdateDebugVariables();
        CheckFastPass();
        CheckLookUp();
        CheckWallPause();
    }
    
    /// <summary>更新调试变量</summary>
    void UpdateDebugVariables()
    {
        if (stateTracker != null && stateTracker.xrCamera != null)
        {
            cameraPosition = stateTracker.xrCamera.transform.position;
            cameraRotation = stateTracker.xrCamera.transform.eulerAngles;
            currentSpeed = stateTracker.CurrentSpeed;
            currentPitch = stateTracker.CurrentPitch;
            isLookingDown = stateTracker.IsLookingDown;
            
            // 同步到 PlayerStateTracker
            stateTracker.UpdateDebugVariables(
                cameraPosition, 
                cameraRotation, 
                currentSpeed, 
                zoneWallTrace != null ? zoneWallTrace.StayDuration : 0f,
                zoneWallTrace != null ? zoneWallTrace.currentDistanceToWall : 0f
            );
        }
        
        if (zoneWallTrace != null)
        {
            currentStayDuration = zoneWallTrace.StayDuration;
            distanceToWall = zoneWallTrace.currentDistanceToWall;
        }
    }
    
    /// <summary>靠墙区域触发检查：靠近墙体 + 低头 + 慢速 + 停留2秒</summary>
    void CheckWallPause()
    {
        if (!enableWallPause || wallPauseTriggered) return;
        if (zoneWallTrace == null || stateTracker == null) return;
        
        // 检查所有触发条件
        bool nearWall = distanceToWall <= requiredWallDistance && distanceToWall > 0f;
        bool lookingDown = isLookingDown || currentPitch >= requiredPitchAngle;
        bool slowSpeed = currentSpeed <= requiredSlowSpeed;
        bool enoughStayTime = currentStayDuration >= requiredStayTime;
        
        if (nearWall && lookingDown && slowSpeed && enoughStayTime)
        {
            wallPauseTriggered = true;
            experienceManager.ForceState(ExperienceState.Avoidant);
            Debug.Log("[Demo] 靠墙低头慢速停留 -> Avoidant");
            Debug.Log($"[条件] 距墙:{distanceToWall:F2}m | 低头:{lookingDown}(pitch:{currentPitch:F1}°) | 速度:{currentSpeed:F2}m/s | 停留:{currentStayDuration:F1}s");
            Invoke(nameof(ResetWallPause), wallPauseCooldown);
        }
    }

    void CheckFastPass()
    {
        if (!enableFastPass || fastPassTriggered) return;

        // 门贴纸区域附近 + 快速移动 = FastPass -> Anxious
        if (zoneDoorNote != null && stateTracker != null && stateTracker.IsFastMoving)
        {
            fastPassTriggered = true;
            experienceManager.ForceState(ExperienceState.Anxious);
            Debug.Log("[Demo] 快速穿过 -> Anxious");
            stateTracker.ResetSpeedTracking();
            Invoke(nameof(ResetFastPass), fastPassCooldown);
        }
    }

    void CheckLookUp()
    {
        if (!enableLookUp || lookUpTriggered) return;

        // 在窗光区 + 抬头或环视 = LookUp -> Explorative
        if (zoneLightView != null && stateTracker != null)
        {
            if (stateTracker.IsLookingUp || stateTracker.IsLookingAround)
            {
                lookUpTriggered = true;
                experienceManager.SetTargetState(ExperienceState.Explorative);
                Debug.Log("[Demo] 抬头环视 -> Explorative");
                Invoke(nameof(ResetLookUp), lookUpCooldown);
            }
        }
    }

    void ResetFastPass() => fastPassTriggered = false;
    void ResetWallPause() => wallPauseTriggered = false;
    void ResetLookUp() => lookUpTriggered = false;

    // ==================== 公共方法 ====================

    /// <summary>手动触发快速穿过 -> Anxious</summary>
    public void ForceFastPass()
    {
        if (!fastPassTriggered)
        {
            fastPassTriggered = true;
            experienceManager.ForceState(ExperienceState.Anxious);
            Debug.Log("[Demo] 强制快速穿过 -> Anxious");
            Invoke(nameof(ResetFastPass), fastPassCooldown);
        }
    }

    /// <summary>手动触发墙边停留 -> Avoidant</summary>
    public void ForceWallPause()
    {
        if (!wallPauseTriggered)
        {
            wallPauseTriggered = true;
            experienceManager.ForceState(ExperienceState.Avoidant);
            Debug.Log("[Demo] 强制墙边停留 -> Avoidant");
            Invoke(nameof(ResetWallPause), wallPauseCooldown);
        }
    }

    /// <summary>手动触发抬头环视 -> Explorative</summary>
    public void ForceLookUp()
    {
        if (!lookUpTriggered)
        {
            lookUpTriggered = true;
            experienceManager.SetTargetState(ExperienceState.Explorative);
            Debug.Log("[Demo] 强制抬头环视 -> Explorative");
            Invoke(nameof(ResetLookUp), lookUpCooldown);
        }
    }

    /// <summary>打印当前记忆日志</summary>
    [ContextMenu("打印记忆日志")]
    public void PrintMemory()
    {
        if (memoryLogger != null)
        {
            Debug.Log("========== 记忆日志 ==========");
            foreach (var entry in memoryLogger.MemoryLog)
            {
                Debug.Log(entry);
            }
            Debug.Log("==============================");
        }
    }
}
