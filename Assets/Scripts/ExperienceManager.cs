using UnityEngine;
using System;
using System.Collections.Generic;

namespace SpaceFeedback
{
    /// <summary>
    /// 体验管理器 - 核心控制器（单例）
    /// 监听 ZoneTrigger 事件，转换为体验状态并广播给所有效果系统
    /// </summary>
    public class ExperienceManager : MonoBehaviour
    {
        public static ExperienceManager Instance { get; private set; }

        [Header("调试")]
        public bool enableDebugLog = true;

        [Header("状态映射（可后续替换为AI逻辑）")]
        [Tooltip("区域A映射到的状态")]
        public ExperienceState mapping_A = ExperienceState.Anxious;
        [Tooltip("区域B映射到的状态")]
        public ExperienceState mapping_B = ExperienceState.Avoidant;
        [Tooltip("区域C映射到的状态")]
        public ExperienceState mapping_C = ExperienceState.Explorative;

        [Header("过渡设置")]
        [Tooltip("状态过渡时间（秒）")]
        public float transitionDuration = 1.5f;

        [Header("集成设置")]
        [Tooltip("是否使用 PerceptionModule 集成（勾选后使用感知模块的区域检测）")]
        public bool usePerceptionModule = true;
        [Tooltip("PerceptionModule 引用（可选，自动查找）")]
        public PerceptionModule perceptionModule;

        // 当前状态
        public ExperienceState CurrentState { get; private set; } = ExperienceState.Anxious;
        public float StateTransitionProgress { get; private set; } = 1f;

        // 事件广播
        public static Action<ExperienceState> OnStateChanged;
        public static Action<ExperienceState, ExperienceState> OnStateTransitionStart;
        public static Action<ExperienceState> OnStateFullyChanged;

        // 私有变量
        private ExperienceState _targetState = ExperienceState.Anxious;
        private float _transitionTimer;
        private ExperienceState _previousState;
        private Dictionary<string, bool> _activeZones = new Dictionary<string, bool>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // 订阅所有 ZoneTrigger 的进入/离开事件
            SubscribeToZoneTriggers();

            // 可选：订阅 PerceptionModule 事件
            if (usePerceptionModule)
            {
                SubscribeToPerceptionModule();
            }

            // 初始化状态
            BroadcastState(CurrentState, CurrentState);
        }

        private void OnDestroy()
        {
            // 取消订阅
            UnsubscribeFromZoneTriggers();
            UnsubscribeFromPerceptionModule();
        }

        private void Update()
        {
            UpdateTransition();
        }

        /// <summary>
        /// 订阅所有 ZoneTrigger 事件
        /// </summary>
        private void SubscribeToZoneTriggers()
        {
            var triggers = FindObjectsOfType<ZoneTrigger>();
            foreach (var trigger in triggers)
            {
                if (trigger != null)
                {
                    trigger.OnPlayerEnter += () => HandleZoneEnter(trigger);
                    trigger.OnPlayerExit += () => HandleZoneExit(trigger);
                }
            }
        }

        private void UnsubscribeFromZoneTriggers()
        {
            var triggers = FindObjectsOfType<ZoneTrigger>();
            foreach (var trigger in triggers)
            {
                if (trigger != null)
                {
                    trigger.OnPlayerEnter -= () => HandleZoneEnter(trigger);
                    trigger.OnPlayerExit -= () => HandleZoneExit(trigger);
                }
            }
        }

        /// <summary>
        /// 订阅 PerceptionModule 事件
        /// </summary>
        private void SubscribeToPerceptionModule()
        {
            if (perceptionModule == null)
            {
                perceptionModule = FindObjectOfType<PerceptionModule>();
            }

            if (perceptionModule != null)
            {
                perceptionModule.OnPerceptionEvent += HandlePerceptionEvent;
                LogDebug("已订阅 PerceptionModule 事件");
            }
        }

        private void UnsubscribeFromPerceptionModule()
        {
            if (perceptionModule != null)
            {
                perceptionModule.OnPerceptionEvent -= HandlePerceptionEvent;
            }
        }

        /// <summary>
        /// 处理 PerceptionModule 的感知事件
        /// </summary>
        private void HandlePerceptionEvent(PerceptionEvent perceptionEvent)
        {
            if (perceptionEvent == null) return;

            LogDebug($"感知事件: {perceptionEvent.EventType} - {perceptionEvent.Zone}");

            // 根据事件类型更新状态
            switch (perceptionEvent.EventType)
            {
                case "ZoneEnter":
                case "Lingering":
                    UpdateTargetState(perceptionEvent.Zone);
                    break;

                case "FastMove":
                    // 快速移动时增强探索感
                    if (CurrentState != ExperienceState.Explorative)
                    {
                        SetTargetState(ExperienceState.Explorative);
                    }
                    break;

                case "NearWall":
                    // 靠近墙壁时增强焦虑感
                    if (CurrentState != ExperienceState.Anxious)
                    {
                        SetTargetState(ExperienceState.Anxious);
                    }
                    break;

                case "ZoneExit":
                    // 离开区域后，根据新区域更新
                    break;
            }
        }

        /// <summary>
        /// ZoneTrigger 进入回调 - 通过 trigger 本身调用
        /// </summary>
        public void HandleZoneEnter(ZoneTrigger trigger)
        {
            if (trigger != null)
            {
                OnEnterArea(trigger.zoneName);
            }
        }

        /// <summary>
        /// ZoneTrigger 离开回调 - 通过 trigger 本身调用
        /// </summary>
        public void HandleZoneExit(ZoneTrigger trigger)
        {
            if (trigger != null)
            {
                OnExitArea(trigger.zoneName);
            }
        }

        /// <summary>
        /// 玩家进入区域（通过名称）
        /// </summary>
        public void OnEnterArea(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName)) return;

            _activeZones[zoneName] = true;
            LogDebug($"玩家进入区域: {zoneName}");

            // 计算当前应该进入什么状态
            UpdateTargetState(zoneName);
        }

        /// <summary>
        /// 玩家离开区域（通过名称）
        /// </summary>
        public void OnExitArea(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName)) return;

            _activeZones[zoneName] = false;
            LogDebug($"玩家离开区域: {zoneName}");

            // 如果还有活跃区域，更新目标状态
            UpdateTargetStateBasedOnActiveZones();
        }

        /// <summary>
        /// 根据区域名称更新目标状态
        /// </summary>
        private void UpdateTargetState(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName)) return;

            string zone = zoneName.ToUpper().Trim();

            ExperienceState newTarget;

            // 精确匹配场景中的区域名称
            if (zone.Contains("DOORNOTE"))
            {
                // doornote → A → Anxious (回避感)
                newTarget = mapping_A;
            }
            else if (zone.Contains("LIGHTVIEW"))
            {
                // lightview → B → Avoidant (回避感)
                newTarget = mapping_B;
            }
            else if (zone.Contains("WALLTRACE"))
            {
                // walltrace → C → Explorative (探索感)
                newTarget = mapping_C;
            }
            // 备用：通用的 A/B/C 匹配
            else if (zone.Contains("A") || zone.Contains("墙") || zone.Contains("WALL") || zone.Contains("边缘"))
            {
                newTarget = mapping_A;
            }
            else if (zone.Contains("B") || zone.Contains("中心") || zone.Contains("CENTER"))
            {
                newTarget = mapping_B;
            }
            else if (zone.Contains("C") || zone.Contains("角落") || zone.Contains("CORNER"))
            {
                newTarget = mapping_C;
            }
            else
            {
                newTarget = mapping_A; // 默认
            }

            SetTargetState(newTarget);
        }

        private void UpdateTargetStateBasedOnActiveZones()
        {
            foreach (var kvp in _activeZones)
            {
                if (kvp.Value)
                {
                    UpdateTargetState(kvp.Key);
                    return;
                }
            }
            // 没有活跃区域，保持当前状态
        }

        /// <summary>
        /// 设置目标状态（触发过渡）
        /// </summary>
        public void SetTargetState(ExperienceState target)
        {
            if (target == _targetState) return;

            _previousState = CurrentState;
            _targetState = target;
            _transitionTimer = 0f;
            StateTransitionProgress = 0f;

            LogDebug($"状态切换开始: {_previousState} → {_targetState}");
            OnStateTransitionStart?.Invoke(_previousState, _targetState);
        }

        /// <summary>
        /// 过渡到指定状态（TransitionTo 别名，兼容外部调用）
        /// </summary>
        public void TransitionTo(ExperienceState targetState)
        {
            SetTargetState(targetState);
        }

        /// <summary>
        /// 平滑过渡更新
        /// </summary>
        private void UpdateTransition()
        {
            if (StateTransitionProgress >= 1f) return;

            _transitionTimer += Time.deltaTime;
            StateTransitionProgress = Mathf.Clamp01(_transitionTimer / transitionDuration);

            // 当过渡完成时，广播最终状态
            if (StateTransitionProgress >= 1f)
            {
                CurrentState = _targetState;
                LogDebug($"状态完全切换: {CurrentState}");
                OnStateFullyChanged?.Invoke(CurrentState);
                BroadcastState(_previousState, CurrentState);
            }
        }

        /// <summary>
        /// 广播状态变化（供外部系统使用）
        /// </summary>
        private void BroadcastState(ExperienceState from, ExperienceState to)
        {
            CurrentState = to;
            OnStateChanged?.Invoke(to);
            LogDebug($"广播状态: {to}");
        }

        /// <summary>
        /// 直接设置状态（跳过区域逻辑）
        /// </summary>
        public void ForceState(ExperienceState state)
        {
            _previousState = CurrentState;
            _targetState = state;
            CurrentState = state;
            StateTransitionProgress = 1f;
            BroadcastState(_previousState, state);
        }

        /// <summary>
        /// 获取当前状态的过渡进度（0-1）
        /// </summary>
        public float GetTransitionProgress()
        {
            return StateTransitionProgress;
        }

        /// <summary>
        /// 获取当前状态对应的区域类型
        /// </summary>
        public AreaType GetAreaTypeForState(ExperienceState state)
        {
            if (state == mapping_A) return AreaType.A_Wall;
            if (state == mapping_B) return AreaType.B_Center;
            return AreaType.C_Corner;
        }

        private void LogDebug(string message)
        {
            if (enableDebugLog)
            {
                Debug.Log($"<color=#FFB366>[ExperienceManager]</color> {message}");
            }
        }
    }
}
