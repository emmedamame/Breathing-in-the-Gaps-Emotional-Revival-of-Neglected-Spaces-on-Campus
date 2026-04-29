using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// LLM决策系统：基于感知事件和记忆上下文，做出空间响应决策
/// 支持批处理、记忆注入、动作JSON解析
/// </summary>
public class LLMSpaceDecision : MonoBehaviour
{
    [Header("LLM配置")]
    public string apiEndpoint = "https://open.biggi.cn/v1/chat/completions";
    public string modelName = "glm-4-flash";
    public string apiKey = ""; // 请在Inspector中填写
    public float requestTimeout = 10f;

    [Header("批处理配置")]
    public float batchInterval = 5f;
    public int maxRecentEvents = 8;
    public int maxMemorySummaries = 6;

    [Header("上下文配置")]
    [TextArea(3, 6)]
    public string systemPrompt = @"你是一个沉浸式空间的智能响应系统。
空间会感知来访者的行为，并做出回应。

【区域定义】
- 区域A（边缘墙体区）：靠墙、贴边、试探性移动，是「寻找庇护」的区域
- 区域B（中心空地）：步速、转头和停顿最明显，是「暴露/审视/被看见」的区域
- 区域C（角落残损区）：适合长时间注视或蹲下观察，是「共鸣/怜悯/抚平」的区域

【来访者行为】
- EnteredArea(area_x)：进入某区域
- Lingering(area_x)：在某区域停留
- FastMove：快速移动
- NearWall：靠近墙体
- GazeDamageCorner：凝视破损角落

【空间情感状态】
- 焦虑：冷白光照、边缘闪烁、高对比、抖动、环境紧张
- 平静：暖黄光、亮度缓升、低对比、柔和、环境舒缓
- 共鸣：局部暖光、材质流动、柔化效果

【输出格式】（必须严格JSON）
{
  ""emotion"": ""calm|anxious|resonant"",
  ""intensity"": 0.0-1.0,
  ""actions"": [
    {
      ""type"": ""light|postfx|audio|ui|object"",
      ""params"": {
        // 详见下方参数定义
      }
    }
  ],
  ""summary"": ""一句话描述本次响应""
}

【动作参数】
1. light: {""color"":""cold_white|warm_yellow|cold_blue"", ""intensity"":0-2, ""edgeFlicker"":true/false}
2. postfx: {""contrast"":0-2, ""vignette"":0-1, ""shake"":0-1, ""softness"":0-1}
3. audio: {""clip"":""electric_noise|wind|breath|ambient|silence"", ""volume"":0-1, ""fadeIn"":true/false}
4. ui: {""text"":""想说的话"", ""position"":""bottom|center|top"", ""duration"":1-5}
5. object: {""target"":""broken_corner|wall|ceiling"", ""effect"":""warm_glow|flow_texture|float_particles""}
";

    [Header("调试")]
    public bool enableDebugLog = true;
    public bool simulateMode = true; // 模拟模式，不实际请求LLM

    // 事件
    public event Action<DecisionResult> OnDecisionMade;

    // 内部状态
    private List<PerceptionEvent> recentEvents = new List<PerceptionEvent>();
    private float batchTimer;
    private bool isProcessing;
    private RuntimeMemoryManager memoryManager;

    // 响应动作集合
    private static readonly Dictionary<string, string[]> emotionActions = new Dictionary<string, string[]>
    {
        { "anxious", new[] { "冷白光照", "边缘闪烁", "高对比", "轻微抖动", "电流噪声" } },
        { "calm", new[] { "暖黄光", "亮度缓升", "低对比", "环境舒缓音" } },
        { "resonant", new[] { "局部暖光", "材质流动", "柔化效果", "低频呼吸声" } }
    };

    void Start()
    {
        memoryManager = FindObjectOfType<RuntimeMemoryManager>();
    }

    private void Update()
    {
        if (isProcessing) return;

        batchTimer += Time.deltaTime;
        if (batchTimer >= batchInterval)
        {
            batchTimer = 0f;
            ProcessBatch();
        }
    }

    /// <summary>注入感知事件</summary>
    public void InjectEvent(PerceptionEvent perceptionEvent)
    {
        recentEvents.Add(perceptionEvent);

        // 保持事件数量在限制内
        if (recentEvents.Count > maxRecentEvents * 2)
        {
            recentEvents.RemoveAt(0);
        }
    }

    /// <summary>强制触发批处理</summary>
    public void ForceProcess()
    {
        ProcessBatch();
    }

    async void ProcessBatch()
    {
        if (recentEvents.Count == 0) return;

        isProcessing = true;

        try
        {
            // 获取最近的事件
            var recent = GetRecentEvents(maxRecentEvents);

            // 获取记忆摘要
            var memories = memoryManager != null
                ? memoryManager.GetRecentSummaries(maxMemorySummaries)
                : new List<string>();

            // 构建上下文
            string context = BuildContext(recent, memories);

            // 请求LLM决策
            DecisionResult result;
            if (simulateMode)
            {
                result = SimulateDecision(recent);
            }
            else
            {
                result = await RequestLLM(context);
            }

            // 触发决策事件
            OnDecisionMade?.Invoke(result);

            // 记录到记忆
            if (memoryManager != null && !string.IsNullOrEmpty(result.Summary))
            {
                memoryManager.AppendSummary(result.Summary, recent);
            }

            LogDebug($"决策完成: {result.Emotion} ({result.Intensity:P0}) - {result.Summary}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[LLM Decision] 决策失败: {e.Message}");
        }
        finally
        {
            isProcessing = false;
        }
    }

    List<PerceptionEvent> GetRecentEvents(int count)
    {
        if (recentEvents.Count <= count)
            return new List<PerceptionEvent>(recentEvents);

        var result = new List<PerceptionEvent>();
        for (int i = recentEvents.Count - count; i < recentEvents.Count; i++)
        {
            result.Add(recentEvents[i]);
        }
        return result;
    }

    string BuildContext(List<PerceptionEvent> events, List<string> memories)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("【最近行为】");
        foreach (var e in events)
        {
            sb.AppendLine($"- {e}");
        }

        if (memories.Count > 0)
        {
            sb.AppendLine("\n【空间记忆】");
            foreach (var m in memories)
            {
                sb.AppendLine($"- {m}");
            }
        }

        return sb.ToString();
    }

    async Task<DecisionResult> RequestLLM(string context)
    {
        string fullPrompt = $"{systemPrompt}\n\n{context}\n\n请根据以上上下文输出JSON决策：";

        // 使用UnityWebRequest进行API调用
        var request = new UnityEngine.Networking.UnityWebRequest(apiEndpoint, "POST");
        request.timeout = (int)requestTimeout;

        var requestBody = new RequestBody
        {
            model = modelName,
            messages = new[]
            {
                new Message { role = "system", content = systemPrompt },
                new Message { role = "user", content = context }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(apiKey))
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

        var operation = request.SendWebRequest();
        float startTime = Time.time;

        while (!operation.isDone && Time.time - startTime < requestTimeout)
        {
            await Task.Delay(100);
        }

        if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            throw new Exception($"API请求失败: {request.error}");
        }

        string response = request.downloadHandler.text;
        return ParseResponse(response);
    }

    DecisionResult ParseResponse(string response)
    {
        // 简单解析JSON响应
        try
        {
            var wrapper = JsonUtility.FromJson<ResponseWrapper>(response);
            if (wrapper.choices != null && wrapper.choices.Length > 0)
            {
                string content = wrapper.choices[0].message.content;
                // 提取JSON部分（可能包含在markdown代码块中）
                int jsonStart = content.IndexOf('{');
                int jsonEnd = content.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd >= jsonStart)
                {
                    string json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return JsonUtility.FromJson<DecisionResult>(json);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLM] 解析响应失败: {e.Message}");
        }

        // 返回默认决策
        return new DecisionResult
        {
            Emotion = "calm",
            Intensity = 0.3f,
            Summary = "默认响应"
        };
    }

    DecisionResult SimulateDecision(List<PerceptionEvent> events)
    {
        // 模拟决策逻辑
        bool hasFastMove = false;
        bool hasNearWall = false;
        bool hasGazeDamage = false;
        bool hasLinger = false;
        string lastZone = "";

        foreach (var e in events)
        {
            switch (e.EventType)
            {
                case "FastMove": hasFastMove = true; break;
                case "NearWall": hasNearWall = true; break;
                case "GazeDamageCorner": hasGazeDamage = true; break;
                case "Lingering": hasLinger = true; break;
            }
            if (!string.IsNullOrEmpty(e.Zone))
                lastZone = e.Zone;
        }

        DecisionResult result = new DecisionResult();

        if (hasGazeDamage)
        {
            // 共鸣状态
            result.Emotion = "resonant";
            result.Intensity = 0.8f;
            result.Actions = new List<SpaceAction>
            {
                new SpaceAction { Type = "light", Params = new ActionParams { color = "warm_yellow", intensity = 0.8f, edgeFlicker = false } },
                new SpaceAction { Type = "postfx", Params = new ActionParams { softness = 0.7f, contrast = 0.9f } },
                new SpaceAction { Type = "audio", Params = new ActionParams { clip = "breath", volume = 0.4f, fadeIn = true } },
                new SpaceAction { Type = "ui", Params = new ActionParams { text = "我感觉你没有想立刻离开", position = "bottom", duration = 3f } }
            };
            result.Summary = "检测到共鸣，场景进入抚慰状态";
        }
        else if (hasFastMove)
        {
            // 焦虑状态
            result.Emotion = "anxious";
            result.Intensity = 0.7f;
            result.Actions = new List<SpaceAction>
            {
                new SpaceAction { Type = "light", Params = new ActionParams { color = "cold_white", intensity = 1.2f, edgeFlicker = true } },
                new SpaceAction { Type = "postfx", Params = new ActionParams { contrast = 1.3f, vignette = 0.6f, shake = 0.3f } },
                new SpaceAction { Type = "audio", Params = new ActionParams { clip = "electric_noise", volume = 0.5f, fadeIn = true } }
            };
            result.Summary = "检测到快速移动，场景进入紧张状态";
        }
        else if (hasNearWall)
        {
            // 平静但警惕
            result.Emotion = "calm";
            result.Intensity = 0.5f;
            result.Actions = new List<SpaceAction>
            {
                new SpaceAction { Type = "light", Params = new ActionParams { color = "warm_yellow", intensity = 0.9f, edgeFlicker = false } },
                new SpaceAction { Type = "object", Params = new ActionParams { target = "wall", effect = "warm_glow" } }
            };
            result.Summary = "检测到靠墙，场景进入庇护状态";
        }
        else
        {
            // 默认平静
            result.Emotion = "calm";
            result.Intensity = 0.3f;
            result.Actions = new List<SpaceAction>
            {
                new SpaceAction { Type = "light", Params = new ActionParams { color = "warm_yellow", intensity = 0.7f, edgeFlicker = false } }
            };
            result.Summary = "默认状态，保持温和氛围";
        }

        return result;
    }

    void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"<color=#87CEEB>[LLM]</color> {message}");
        }
    }
}

#region 数据结构

[Serializable]
class RequestBody
{
    public string model;
    public Message[] messages;
}

[Serializable]
class Message
{
    public string role;
    public string content;
}

[Serializable]
class ResponseWrapper
{
    public Choice[] choices;
}

[Serializable]
class Choice
{
    public ResponseMessage message;
}

[Serializable]
class ResponseMessage
{
    public string content;
}

[Serializable]
public class DecisionResult
{
    public string Emotion = "calm";
    public float Intensity = 0.5f;
    public List<SpaceAction> Actions = new List<SpaceAction>();
    public string Summary = "";

    public bool IsCalm => Emotion == "calm";
    public bool IsAnxious => Emotion == "anxious";
    public bool IsResonant => Emotion == "resonant";
}

[Serializable]
public class SpaceAction
{
    public string Type; // light, postfx, audio, ui, object
    public ActionParams Params = new ActionParams();
}

[Serializable]
public class ActionParams
{
    // Light
    public string color;
    public float intensity = 1f;
    public bool edgeFlicker;

    // PostFX
    public float contrast = 1f;
    public float vignette;
    public float shake;
    public float softness;

    // Audio
    public string clip;
    public float volume = 1f;
    public bool fadeIn;

    // UI
    public string text;
    public string position = "bottom";
    public float duration = 3f;

    // Object
    public string target;
    public string effect;
}

#endregion
