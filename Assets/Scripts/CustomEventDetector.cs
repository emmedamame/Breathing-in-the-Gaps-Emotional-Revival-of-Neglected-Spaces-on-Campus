using UnityEngine;
using System;

/// <summary>
/// 自定义事件检测器
/// 处理两种自定义事件：
/// 1. 靠近墙体（距离墙面小于阈值并持续2秒）
/// 2. 长时间凝视破损角落（注视同一目标超过3秒）
/// </summary>
public class CustomEventDetector : MonoBehaviour
{
    [Header("靠墙检测")]
    public float wallDistanceThreshold = 0.8f;
    public float wallStayDuration = 2f;
    public LayerMask wallLayerMask = ~0;

    [Header("凝视检测")]
    public float gazeDurationThreshold = 3f;

    [Header("引用")]
    public GazeTracker gazeTracker;
    public Transform playerRoot;

    [Header("调试")]
    public bool enableDebugLog = true;

    // 事件
    public event Action<float> OnNearWall;              // 靠近墙体事件：持续时长
    public event Action OnGazeDamageCorner;              // 凝视破损角落事件

    // 内部状态
    private float wallProximityTimer;
    private bool nearWallTriggered;
    private bool gazeTriggered;

    void Start()
    {
        SetupReferences();
    }

    void SetupReferences()
    {
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
        DetectNearWall();
        DetectGaze();
    }

    #region 靠墙检测

    void DetectNearWall()
    {
        if (playerRoot == null) return;

        Collider[] nearby = Physics.OverlapSphere(playerRoot.position, wallDistanceThreshold, wallLayerMask);

        bool isNearWall = false;
        foreach (var col in nearby)
        {
            if (IsWall(col.gameObject))
            {
                isNearWall = true;
                break;
            }
        }

        if (isNearWall)
        {
            wallProximityTimer += Time.deltaTime;

            if (wallProximityTimer >= wallStayDuration && !nearWallTriggered)
            {
                nearWallTriggered = true;
                OnNearWall?.Invoke(wallProximityTimer);
                LogDebug($"NearWall事件触发: 靠近墙体 {wallProximityTimer:F1}s");
            }
        }
        else
        {
            if (wallProximityTimer > 0)
            {
                wallProximityTimer = 0f;
                nearWallTriggered = false;
            }
        }
    }

    bool IsWall(GameObject obj)
    {
        if (obj.CompareTag("Wall") || obj.CompareTag("Boundary"))
            return true;

        string name = obj.name.ToLower();
        if (name.Contains("wall") || name.Contains("墙") || name.Contains("墙面"))
            return true;

        if (obj.transform.parent != null)
        {
            string parentName = obj.transform.parent.name.ToLower();
            if (parentName.Contains("wall") || parentName.Contains("墙"))
                return true;
        }

        return false;
    }

    #endregion

    #region 凝视检测（简化版）

    void DetectGaze()
    {
        if (gazeTracker == null) return;

        float gazeDuration = gazeTracker.GazeDuration;

        if (gazeDuration >= gazeDurationThreshold && !gazeTriggered)
        {
            gazeTriggered = true;
            OnGazeDamageCorner?.Invoke();
            LogDebug($"GazeDamageCorner事件触发: 凝视时长 {gazeDuration:F1}s");
        }
        else if (gazeDuration < 0.1f)
        {
            gazeTriggered = false;
        }
    }

    #endregion

    #region 公共方法

    /// <summary>重置所有检测状态</summary>
    public void Reset()
    {
        wallProximityTimer = 0f;
        nearWallTriggered = false;
        gazeTriggered = false;
    }

    /// <summary>获取当前状态</summary>
    public CustomEventState GetState()
    {
        return new CustomEventState
        {
            IsNearWall = wallProximityTimer > 0.1f,
            WallDuration = wallProximityTimer,
            NearWallTriggered = nearWallTriggered,
            GazeDuration = gazeTracker != null ? gazeTracker.GazeDuration : 0f,
            GazeTriggered = gazeTriggered
        };
    }

    #endregion

    void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"<color=#FFB6C1>[CustomEvent]</color> {message}");
        }
    }
}

/// <summary>
/// 自定义事件状态
/// </summary>
public class CustomEventState
{
    public bool IsNearWall;
    public float WallDuration;
    public bool NearWallTriggered;
    public float GazeDuration;
    public bool GazeTriggered;
}
