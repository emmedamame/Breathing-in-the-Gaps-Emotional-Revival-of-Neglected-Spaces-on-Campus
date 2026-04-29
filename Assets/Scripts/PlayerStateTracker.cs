using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// 追踪玩家状态：位置、速度、头部角度、转头幅度
/// </summary>
public class PlayerStateTracker : MonoBehaviour
{
    [Header("XR引用")]
    public Camera xrCamera;
    public Transform playerRoot;

    [Header("速度检测")]
    public float speedThreshold = 1.2f;
    public float speedHoldTime = 1.2f;

    [Header("抬头检测")]
    public float lookUpAngleThreshold = -20f;
    public float horizontalLookThreshold = 120f;
    public float lookAroundTime = 3f;

    [Header("调试变量 (Public，供Inspector调整)")]
    public Vector3 cameraPosition;
    public Vector3 cameraRotation;
    public float currentSpeed;
    public float currentStayDuration;
    public float distanceToWall;

    // 内部状态（只读，运行时显示）
    public float CurrentSpeed { get; private set; }
    public float CurrentPitch { get; private set; }
    public float HorizontalLookAccumulated { get; private set; }
    public bool IsFastMoving { get; private set; }
    public bool IsLookingUp { get; private set; }
    public bool IsLookingAround { get; private set; }
    public bool IsLookingDown { get; private set; }

    private Vector3 lastPosition;
    private float speedTimer;
    private float lookAroundTimer;
    private float lastYaw;
    private float accumulatedYawDelta;

    void Start()
    {
        // 优先查找 XR 相机，而不是普通的 Camera.main
        xrCamera = FindXRCamera();
        if (xrCamera == null)
            xrCamera = Camera.main;
            
        if (playerRoot == null)
            playerRoot = transform;

        lastPosition = playerRoot.position;
        lastYaw = xrCamera != null ? xrCamera.transform.eulerAngles.y : 0f;
    }

    Camera FindXRCamera()
    {
        // 1. 尝试从 XROrigin 获取相机
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin != null && xrOrigin.Camera != null)
            return xrOrigin.Camera;

        // 2. 查找带 MainCamera 标签的相机
        GameObject mainCamObj = GameObject.FindGameObjectWithTag("MainCamera");
        if (mainCamObj != null)
            return mainCamObj.GetComponent<Camera>();

        // 3. 返回 Camera.main
        return Camera.main;
    }

    /// <summary>外部设置引用（会被XRLocomotionSetup调用）</summary>
    public void SetReferences(Camera cam, Transform root)
    {
        if (cam != null) xrCamera = cam;
        if (root != null) playerRoot = root;
        lastPosition = playerRoot.position;
        lastYaw = xrCamera != null ? xrCamera.transform.eulerAngles.y : 0f;
    }

    private void Update()
    {
        TrackSpeed();
        TrackHeadRotation();
    }

    void TrackSpeed()
    {
        Vector3 currentPos = playerRoot.position;
        float distance = Vector3.Distance(currentPos, lastPosition);
        CurrentSpeed = distance / Time.deltaTime;
        lastPosition = currentPos;

        if (CurrentSpeed > speedThreshold)
        {
            speedTimer += Time.deltaTime;
            IsFastMoving = speedTimer >= speedHoldTime;
        }
        else
        {
            speedTimer = 0f;
            IsFastMoving = false;
        }
    }

    void TrackHeadRotation()
    {
        if (xrCamera == null) return;

        Transform camTransform = xrCamera.transform;
        Vector3 euler = camTransform.eulerAngles;
        
        // 俯仰角（抬头为正）
        CurrentPitch = euler.x;
        if (CurrentPitch > 180f) CurrentPitch -= 360f;
        
        // 抬头检测（抬头时 pitch 为负）
        IsLookingUp = CurrentPitch < lookUpAngleThreshold;
        
        // 低头检测（低头时 pitch 为正，大于30度）
        IsLookingDown = CurrentPitch > 30f;

        // 水平转头累计
        float currentYaw = euler.y;
        float yawDelta = Mathf.Abs(currentYaw - lastYaw);
        if (yawDelta > 180f) yawDelta = 360f - yawDelta;
        
        accumulatedYawDelta += yawDelta;
        lookAroundTimer += Time.deltaTime;

        if (lookAroundTimer >= lookAroundTime)
        {
            HorizontalLookAccumulated = accumulatedYawDelta;
            IsLookingAround = accumulatedYawDelta > horizontalLookThreshold;
            
            // 重置计时器
            accumulatedYawDelta = 0f;
            lookAroundTimer = 0f;
        }

        lastYaw = currentYaw;
    }
    
    /// <summary>更新调试变量（由DemoController每帧调用）</summary>
    public void UpdateDebugVariables(Vector3 camPos, Vector3 camRot, float speed, float stayDuration, float wallDist)
    {
        cameraPosition = camPos;
        cameraRotation = camRot;
        currentSpeed = speed;
        currentStayDuration = stayDuration;
        distanceToWall = wallDist;
    }

    /// <summary>重置速度追踪（进入新区域时调用）</summary>
    public void ResetSpeedTracking()
    {
        speedTimer = 0f;
        IsFastMoving = false;
    }
}
