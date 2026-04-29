using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using Unity.XR.CoreUtils;

/// <summary>
/// 运行时自动为 XR Origin 添加行走组件（推动左摇杆移动，右摇杆转向）
/// </summary>
public class XRLocomotionSetup : MonoBehaviour
{
    [Header("移动设置")]
    [Tooltip("移动速度 (单位/秒)")]
    public float moveSpeed = 2f;
    
    [Header("转向设置")]
    [Tooltip("转向速度 (度/秒)")]
    public float turnSpeed = 90f;
    
    [Tooltip("是否启用连续转向")]
    public bool enableTurn = true;
    
    [Header("方向来源")]
    [Tooltip("true = 头显方向, false = 左手柄方向")]
    public bool useHeadDirection = true;

    private void Awake()
    {
        SetupLocomotion();
    }

    private void SetupLocomotion()
    {
        // 找到 XR Origin
        XROrigin xrOrigin = FindObjectOfType<XROrigin>();
        if (xrOrigin == null)
        {
            Debug.LogError("[XRLocomotion] 未找到 XR Origin！");
            return;
        }

        // 找到 Camera Offset (XR Origin 的子对象)
        Transform cameraOffsetTransform = xrOrigin.transform.Find("Camera Offset");
        if (cameraOffsetTransform == null)
        {
            Debug.LogError("[XRLocomotion] 未找到 Camera Offset！");
            return;
        }

        // 设置 PlayerStateTracker 的 playerRoot 指向 XR Origin（重要！）
        PlayerStateTracker tracker = FindObjectOfType<PlayerStateTracker>();
        if (tracker != null)
        {
            tracker.SetReferences(xrOrigin.Camera, xrOrigin.transform);
            Debug.Log("[XRLocomotion] 已同步 PlayerStateTracker 到 XR Origin");
        }

        // 添加 Continuous Move Provider
        var moveProvider = cameraOffsetTransform.gameObject.GetComponent<ContinuousMoveProvider>();
        if (moveProvider == null)
        {
            moveProvider = cameraOffsetTransform.gameObject.AddComponent<ContinuousMoveProvider>();
            
            // 配置移动参数
            moveProvider.moveSpeed = moveSpeed;
            moveProvider.enableStrafe = true;
            
            // 设置方向来源 - 使用头显方向
            if (useHeadDirection)
            {
                var headTransform = xrOrigin.Camera?.transform;
                if (headTransform != null)
                {
                    moveProvider.forwardSource = headTransform;
                }
            }
            
            Debug.Log($"[XRLocomotion] 已添加 ContinuousMoveProvider (速度: {moveSpeed})");
        }

        // 添加 Continuous Turn Provider
        if (enableTurn)
        {
            var turnProvider = cameraOffsetTransform.gameObject.GetComponent<ContinuousTurnProvider>();
            if (turnProvider == null)
            {
                turnProvider = cameraOffsetTransform.gameObject.AddComponent<ContinuousTurnProvider>();
                turnProvider.turnSpeed = turnSpeed;
                Debug.Log($"[XRLocomotion] 已添加 ContinuousTurnProvider (速度: {turnSpeed}°/s)");
            }
        }

        Debug.Log("[XRLocomotion] XR 行走系统配置完成！");
        Debug.Log("[XRLocomotion] 左摇杆 = 移动 | 右摇杆 = 转向");
    }
}
