using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// 记录每次触发状态的内存日志，方便后续扩展为AI记忆
/// </summary>
public class DemoMemoryLogger : MonoBehaviour
{
    public static DemoMemoryLogger Instance { get; private set; }

    [Header("日志配置")]
    public bool logToConsole = true;

    private List<string> memoryLog = new List<string>();

    // 用于AI后续读取
    public IReadOnlyList<string> MemoryLog => memoryLog;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 记录行为日志
    /// </summary>
    /// <param name="behavior">用户行为</param>
    /// <param name="spaceInterpretation">空间解释</param>
    /// <param name="spaceResponse">空间响应</param>
    public void Log(string behavior, string spaceInterpretation, string spaceResponse)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string entry = $"[{timestamp}] {behavior} -> {spaceInterpretation} -> {spaceResponse}";
        
        memoryLog.Add(entry);

        if (logToConsole)
        {
            Debug.Log($"<color=#88CCFF>[Memory]</color> {entry}");
        }
    }

    /// <summary>
    /// 获取最近的N条记录
    /// </summary>
    public List<string> GetRecent(int count)
    {
        if (count >= memoryLog.Count)
            return new List<string>(memoryLog);

        List<string> recent = new List<string>();
        int start = memoryLog.Count - count;
        for (int i = start; i < memoryLog.Count; i++)
        {
            recent.Add(memoryLog[i]);
        }
        return recent;
    }

    /// <summary>
    /// 导出为单个字符串（方便AI读取）
    /// </summary>
    public string ExportAsText()
    {
        return string.Join("\n", memoryLog);
    }

    /// <summary>
    /// 清除所有记忆（调试用）
    /// </summary>
    public void Clear()
    {
        memoryLog.Clear();
        Debug.Log("[Memory] 记忆已清除");
    }
}
