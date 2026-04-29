using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// 运行时记忆管理器
/// 最简实现：每轮只保存1行summary到jsonl文件
/// 不做向量库、不做embedding、不做复杂检索
/// </summary>
public class RuntimeMemoryManager : MonoBehaviour
{
    [Header("文件配置")]
    public string memoryFileName = "gap_breath_memory.jsonl";
    public int maxRecentSummaries = 6;

    [Header("调试")]
    public bool enableDebugLog = true;
    public bool loadOnStart = true;

    // 内存中的记忆
    private List<MemoryEntry> memoryEntries = new List<MemoryEntry>();

    // 文件路径
    private string MemoryFilePath => Path.Combine(Application.persistentDataPath, memoryFileName);

    public event Action<MemoryEntry> OnNewMemory;

    private void Start()
    {
        if (loadOnStart)
        {
            LoadMemories();
        }
    }

    /// <summary>
    /// 追加新的记忆summary
    /// </summary>
    /// <param name="summary">LLM生成的一行summary</param>
    /// <param name="recentEvents">关联的最近事件</param>
    public void AppendSummary(string summary, List<PerceptionEvent> recentEvents = null)
    {
        var entry = new MemoryEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            VisitorBehavior = FormatBehavior(recentEvents),
            InterpretedFeeling = InferFeeling(summary),
            SpatialResponse = summary,
            RelatedEvents = recentEvents != null
                ? recentEvents.Select(e => e.Description).ToList()
                : new List<string>()
        };

        memoryEntries.Add(entry);

        // 保持内存中的记忆数量
        while (memoryEntries.Count > maxRecentSummaries * 2)
        {
            memoryEntries.RemoveAt(0);
        }

        // 追加到文件
        AppendToFile(entry);

        OnNewMemory?.Invoke(entry);

        if (enableDebugLog)
        {
            Debug.Log($"<color=#DDA0DD>[Memory]</color> 新记忆: {entry}");
        }
    }

    /// <summary>
    /// 获取最近的N条summary
    /// </summary>
    public List<string> GetRecentSummaries(int count)
    {
        if (memoryEntries.Count == 0) return new List<string>();

        int startIndex = Math.Max(0, memoryEntries.Count - count);
        return memoryEntries
            .Skip(startIndex)
            .Take(count)
            .Select(e => e.ToSummary())
            .ToList();
    }

    /// <summary>
    /// 获取最近的N条完整记忆
    /// </summary>
    public List<MemoryEntry> GetRecentEntries(int count)
    {
        if (memoryEntries.Count == 0) return new List<MemoryEntry>();

        int startIndex = Math.Max(0, memoryEntries.Count - count);
        return memoryEntries
            .Skip(startIndex)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// 导出所有记忆为文本
    /// </summary>
    public string ExportAsText()
    {
        return string.Join("\n\n", memoryEntries.Select(e => e.ToSummary()));
    }

    /// <summary>
    /// 清除所有记忆
    /// </summary>
    public void ClearMemories()
    {
        memoryEntries.Clear();

        if (File.Exists(MemoryFilePath))
        {
            File.Delete(MemoryFilePath);
        }

        Debug.Log("[Memory] 所有记忆已清除");
    }

    /// <summary>
    /// 获取记忆统计
    /// </summary>
    public MemoryStats GetStats()
    {
        string lastFeeling = memoryEntries.Count > 0
            ? memoryEntries
                .GroupBy(e => e.InterpretedFeeling)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "无"
            : "无";

        string lastTime = memoryEntries.Count > 0
            ? memoryEntries[memoryEntries.Count - 1].Timestamp
            : "无";

        return new MemoryStats
        {
            TotalEntries = memoryEntries.Count,
            MostCommonFeeling = lastFeeling,
            LastUpdate = lastTime
        };
    }

    #region 私有方法

    string FormatBehavior(List<PerceptionEvent> events)
    {
        if (events == null || events.Count == 0)
            return "无明显行为";

        var descriptions = events
            .TakeLast(4)
            .Select(e => e.Description)
            .ToList();

        return string.Join("；", descriptions);
    }

    string InferFeeling(string summary)
    {
        string lower = summary.ToLower();

        if (lower.Contains("紧张") || lower.Contains("焦虑") || lower.Contains("快速"))
            return "焦虑";
        if (lower.Contains("共鸣") || lower.Contains("抚慰") || lower.Contains("温柔"))
            return "共鸣";
        if (lower.Contains("庇护") || lower.Contains("平静") || lower.Contains("停留"))
            return "平静";

        return "未知";
    }

    void AppendToFile(MemoryEntry entry)
    {
        try
        {
            string json = JsonUtility.ToJson(entry);
            File.AppendAllText(MemoryFilePath, json + "\n");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Memory] 写入文件失败: {e.Message}");
        }
    }

    void LoadMemories()
    {
        if (!File.Exists(MemoryFilePath))
        {
            Debug.Log($"[Memory] 记忆文件不存在，将创建新文件: {MemoryFilePath}");
            return;
        }

        try
        {
            memoryEntries.Clear();
            var lines = File.ReadAllLines(MemoryFilePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var entry = JsonUtility.FromJson<MemoryEntry>(line);
                    if (entry != null)
                    {
                        memoryEntries.Add(entry);
                    }
                }
                catch
                {
                    // 跳过格式错误的行
                }
            }

            // 只保留最近的记忆
            while (memoryEntries.Count > maxRecentSummaries * 3)
            {
                memoryEntries.RemoveAt(0);
            }

            Debug.Log($"[Memory] 加载了 {memoryEntries.Count} 条记忆");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Memory] 加载记忆失败: {e.Message}");
        }
    }

    #endregion
}

/// <summary>
/// 单条记忆条目
/// </summary>
[Serializable]
public class MemoryEntry
{
    public string Timestamp;
    public string VisitorBehavior;
    public string InterpretedFeeling;
    public string SpatialResponse;
    public List<string> RelatedEvents;

    public MemoryEntry()
    {
        Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        RelatedEvents = new List<string>();
    }

    /// <summary>格式化为一行summary</summary>
    public string ToSummary()
    {
        return $"{Timestamp} | {VisitorBehavior} | {InterpretedFeeling} | {SpatialResponse}";
    }

    public override string ToString()
    {
        return ToSummary();
    }
}

/// <summary>
/// 记忆统计
/// </summary>
[Serializable]
public class MemoryStats
{
    public int TotalEntries;
    public string MostCommonFeeling;
    public string LastUpdate;
}
