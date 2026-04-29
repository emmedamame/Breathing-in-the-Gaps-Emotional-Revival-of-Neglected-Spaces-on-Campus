using UnityEngine;
using System;

/// <summary>
/// 注视方向追踪器（简化版）
/// 基于摄像机旋转判断注视方向，无需射线检测
/// </summary>
public class GazeTracker : MonoBehaviour
{
    [Header("注视方向配置")]
    public float gazeDirectionChangeThreshold = 15f; // 注视方向变化角度阈值

    [Header("注视时长配置")]
    public float longGazeThreshold = 3f;
    public float shortGazeResetTime = 0.5f;

    [Header("注视目标配置")]
    public string[] gazeTargetNames = { "Damage", "Broken", "Crack", "Corner", "破损" };

    [Header("引用")]
    public Transform gazeTargetPoint; // 可选：场景中放置一个空物体作为"注视焦点"

    [Header("调试")]
    public bool showGazeDirection = true;
    public Color gazeColor = Color.cyan;

    // 事件
    public event Action<Vector3> OnGazeDirectionChanged;  // 注视方向变化
    public event Action OnLongGazeComplete;               // 长时间凝视完成

    // 当前状态
    public Vector3 CurrentGazeDirection { get; private set; }
    public float GazeDuration { get; private set; }
    public bool IsGazing => GazeDuration > 0.1f;

    private Camera xrCamera;
    private Vector3 lastGazeDirection;
    private float gazeTimer;
    private float noGazeTimer;
    private bool longGazeTriggered;

    void Start()
    {
        xrCamera = FindXRCamera();
        if (xrCamera != null)
        {
            CurrentGazeDirection = xrCamera.transform.forward;
            lastGazeDirection = CurrentGazeDirection;
        }
    }

    Camera FindXRCamera()
    {
        var xrOrigin = FindObjectOfType<Unity.XR.CoreUtils.XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
            return xrOrigin.Camera;

        var mainCam = GameObject.FindGameObjectWithTag("MainCamera");
        return mainCam != null ? mainCam.GetComponent<Camera>() : Camera.main;
    }

    private void Update()
    {
        TrackGaze();
    }

    void TrackGaze()
    {
        if (xrCamera == null) return;

        Vector3 newDirection = xrCamera.transform.forward;
        CurrentGazeDirection = newDirection;

        // 检测注视方向是否发生显著变化
        float angleChange = Vector3.Angle(lastGazeDirection, newDirection);

        if (angleChange > gazeDirectionChangeThreshold)
        {
            // 注视方向变化超过阈值
            if (gazeTimer > 0.5f) // 只有在看了一段时间后才触发
            {
                OnGazeDirectionChanged?.Invoke(newDirection);
            }

            lastGazeDirection = newDirection;
            gazeTimer = 0f; // 重置注视计时
            longGazeTriggered = false;
        }
        else
        {
            // 注视方向基本不变，增加计时
            gazeTimer += Time.deltaTime;
            GazeDuration = gazeTimer;

            // 检测长时间凝视
            if (gazeTimer >= longGazeThreshold && !longGazeTriggered)
            {
                longGazeTriggered = true;
                OnLongGazeComplete?.Invoke();
            }
        }

        // 长时间没变化也算作注视结束
        if (angleChange > 5f)
        {
            noGazeTimer = 0f;
        }
        else
        {
            noGazeTimer += Time.deltaTime;
            if (noGazeTimer > shortGazeResetTime && gazeTimer > 0)
            {
                gazeTimer = 0f;
                GazeDuration = 0f;
            }
        }
    }

    /// <summary>检查注视方向是否朝向某个方向</summary>
    public bool IsGazingAtDirection(Vector3 worldDirection, float angleThreshold = 30f)
    {
        float angle = Vector3.Angle(CurrentGazeDirection, worldDirection);
        return angle < angleThreshold;
    }

    /// <summary>检查注视方向是否朝向某个物体</summary>
    public bool IsGazingAtObject(Transform target, float angleThreshold = 30f)
    {
        if (target == null || xrCamera == null) return false;

        Vector3 toTarget = (target.position - xrCamera.transform.position).normalized;
        float angle = Vector3.Angle(CurrentGazeDirection, toTarget);
        return angle < angleThreshold;
    }

    /// <summary>检查注视是否朝上（抬头 = pitch负值）</summary>
    public bool IsLookingUp(float pitchThreshold = -20f)
    {
        if (xrCamera == null) return false;

        float pitch = xrCamera.transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        return pitch < pitchThreshold;
    }

    /// <summary>检查注视是否朝下（低头 = pitch正值）</summary>
    public bool IsLookingDown(float pitchThreshold = 20f)
    {
        if (xrCamera == null) return false;

        float pitch = xrCamera.transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;

        return pitch > pitchThreshold;
    }

    /// <summary>获取当前俯仰角</summary>
    public float GetPitch()
    {
        if (xrCamera == null) return 0f;

        float pitch = xrCamera.transform.eulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
        return pitch;
    }

    /// <summary>获取当前偏航角</summary>
    public float GetYaw()
    {
        if (xrCamera == null) return 0f;
        return xrCamera.transform.eulerAngles.y;
    }

    /// <summary>重置注视状态</summary>
    public void Reset()
    {
        gazeTimer = 0f;
        noGazeTimer = 0f;
        GazeDuration = 0f;
        longGazeTriggered = false;
    }

    private void OnDrawGizmos()
    {
        if (!showGazeDirection || xrCamera == null) return;

        Gizmos.color = gazeColor;
        Vector3 start = xrCamera.transform.position;
        Vector3 end = start + xrCamera.transform.forward * 5f;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }
}

/// <summary>
/// 凝视角落检测器（简化版）
/// 基于注视时长判断是否凝视破损角落
/// </summary>
public class DamageCornerGazeDetector : MonoBehaviour
{
    [Header("配置")]
    public float gazeDurationThreshold = 3f;
    public string[] damageTags = { "Damage", "Broken", "Crack", "Corner", "破损" };

    [Header("引用")]
    public GazeTracker gazeTracker;

    // 事件
    public event Action OnGazeDamageCornerComplete;

    private bool gazeCompleteTriggered;

    private void Start()
    {
        if (gazeTracker == null)
            gazeTracker = FindObjectOfType<GazeTracker>();

        if (gazeTracker != null)
        {
            gazeTracker.OnLongGazeComplete += HandleLongGazeComplete;
        }
    }

    void HandleLongGazeComplete()
    {
        if (gazeCompleteTriggered) return;

        // 简化逻辑：只要长时间凝视就触发
        // 实际项目中可以在这里添加位置检测或其他条件
        gazeCompleteTriggered = true;
        OnGazeDamageCornerComplete?.Invoke();
        Debug.Log($"[Gaze] 长时间凝视完成，时长: {gazeTracker.GazeDuration:F1}s");
    }

    public void Reset()
    {
        gazeCompleteTriggered = false;
    }
}
