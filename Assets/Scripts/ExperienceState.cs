using UnityEngine;

namespace SpaceFeedback
{
    /// <summary>
    /// 区域类型枚举 - 对应三种空间特征
    /// </summary>
    public enum AreaType
    {
        A_Wall,     // 靠墙区域（庇护）- 产生 Anxious 状态
        B_Center,   // 中心区域（暴露）- 产生 Avoidant 状态
        C_Corner    // 破损角落（共鸣）- 产生 Explorative 状态
    }

    /// <summary>
    /// 体验状态枚举 - 核心状态机
    /// </summary>
    public enum ExperienceState
    {
        Anxious,      // 焦虑状态 - 暖色、低强度、轻微模糊
        Avoidant,     // 回避状态 - 高亮、白色、去饱和
        Explorative   // 探索状态 - 高对比、高饱和、色彩丰富
    }
}
