using UnityEngine;
using System;

/// <summary>
/// 挂在触发区Collider上，检测玩家进入/离开/停留
/// </summary>
public class ZoneTrigger : MonoBehaviour
{
    [Header("区域配置")]
    public string zoneName = "Zone_Name";
    [Header("停留触发时间（秒）")]
    public float stayDuration = 1f;

    [Header("距墙检测（仅靠墙区域使用）")]
    public float wallDistanceThreshold = 0.5f;
    public float maxWallDistance = 1.5f;
    public LayerMask wallLayerMask = ~0;
    [Header("调试")]
    public Color gizmoColor = Color.yellow;
    [Tooltip("启用调试日志")]
    public bool enableDebugLog = false;

    // 调试变量
    public float currentDistanceToWall;
    public bool isNearWall;

    // 事件
    public event Action OnPlayerEnter;
    public event Action OnPlayerExit;
    public event Action<string> OnStayComplete;
    public event Action<bool> OnNearWallChanged;

    private bool isPlayerInside;
    private float stayTimer;
    private bool stayTriggered;

    private void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    [Header("碰撞配置")]
    [Tooltip("玩家位置检测：勾选则使用位置检测（更可靠），取消则使用碰撞检测")]
    public bool usePositionCheck = true;
    [Tooltip("位置检测半径（米）")]
    public float checkRadius = 0.5f;

    /// <summary>玩家是否在区域内</summary>
    public bool IsPlayerInside => isPlayerInside;

    /// <summary>当前停留时长</summary>
    public float StayDuration => stayTimer;

    private void OnTriggerEnter(Collider other)
    {
        if (usePositionCheck) return;
        TryEnterZone(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (usePositionCheck) return;
        TryExitZone(other);
    }

    private void TryEnterZone(Collider other)
    {
        if (other.GetComponent<Camera>() != null || other.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            isPlayerInside = true;
            stayTimer = 0f;
            stayTriggered = false;
            OnPlayerEnter?.Invoke();
            // Memory 相关日志已过滤
            // Debug.Log($"[Zone] 进入区域: {zoneName}");
        }
    }

    private void TryExitZone(Collider other)
    {
        if (other.GetComponent<Camera>() != null || other.CompareTag("Player") || other.CompareTag("MainCamera"))
        {
            isPlayerInside = false;
            stayTimer = 0f;
            OnPlayerExit?.Invoke();
            // Memory 相关日志已过滤
            // Debug.Log($"[Zone] 离开区域: {zoneName}");
        }
    }

    private void Update()
    {
        if (usePositionCheck)
        {
            CheckPlayerPosition();
        }
        else if (isPlayerInside && !stayTriggered)
        {
            stayTimer += Time.deltaTime;
            if (stayTimer >= stayDuration)
            {
                stayTriggered = true;
                OnStayComplete?.Invoke(zoneName);
            }
        }
        
        UpdateWallDetection();
    }
    
    private void UpdateWallDetection()
    {
        if (!zoneName.ToUpper().Contains("WALL")) return;
        
        Vector3 checkPoint = GetPlayerCheckPoint();
        currentDistanceToWall = CalculateDistanceToWall(checkPoint);
        bool wasNearWall = isNearWall;
        isNearWall = currentDistanceToWall <= wallDistanceThreshold && currentDistanceToWall > 0f;
        
        if (isNearWall != wasNearWall)
        {
            OnNearWallChanged?.Invoke(isNearWall);
            if (enableDebugLog)
                Debug.Log($"[Zone] {zoneName} 距墙距离: {currentDistanceToWall:F2}m, 靠近墙体: {isNearWall}");
        }
    }
    
    private Vector3 GetPlayerCheckPoint()
    {
        var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
            return xrOrigin.transform.position;
        else if (Camera.main != null)
            return Camera.main.transform.position;
        return transform.position;
    }
    
    private float CalculateDistanceToWall(Vector3 fromPoint)
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return float.MaxValue;
        
        Vector3 closestPoint = col.ClosestPoint(fromPoint);
        return Vector3.Distance(fromPoint, closestPoint);
    }

    private void CheckPlayerPosition()
    {
        bool wasInside = isPlayerInside;
        isPlayerInside = false;

        Collider col = GetComponent<Collider>();
        if (col == null) return;

        // 优先使用 XR Origin 位置（XR 环境中最可靠）
        Vector3 checkPoint;
        var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null)
        {
            checkPoint = xrOrigin.transform.position;
        }
        else if (Camera.main != null)
        {
            checkPoint = Camera.main.transform.position;
        }
        else
        {
            // 备用：查找任何带 Camera 组件的对象
            var cam = UnityEngine.Object.FindObjectOfType<Camera>();
            checkPoint = cam != null ? cam.transform.position : transform.position;
        }

        if (col.bounds.Contains(checkPoint))
        {
            isPlayerInside = true;
            if (!wasInside)
            {
                stayTimer = 0f;
                stayTriggered = false;
                OnPlayerEnter?.Invoke();
                if (enableDebugLog) Debug.Log($"[Zone] 进入区域: {zoneName} at {checkPoint}");
            }
        }
        else if (wasInside)
        {
            stayTimer = 0f;
            stayTriggered = false;
            OnPlayerExit?.Invoke();
            if (enableDebugLog) Debug.Log($"[Zone] 离开区域: {zoneName}");
        }

        if (isPlayerInside && !stayTriggered)
        {
            stayTimer += Time.deltaTime;
            if (stayTimer >= stayDuration)
            {
                stayTriggered = true;
                OnStayComplete?.Invoke(zoneName);
                if (enableDebugLog) Debug.Log($"[Zone] 停留完成: {zoneName}");
            }
        }
    }

    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }

    private void OnDrawGizmosSelected()
    {
        OnDrawGizmos();
        Gizmos.color = gizmoColor * 1.5f;
        Gizmos.matrix = transform.localToWorldMatrix;

        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
    }
}
