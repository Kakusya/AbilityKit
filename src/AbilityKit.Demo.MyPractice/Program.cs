using System;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.MyPractice;

// 带有标签容器的木桩
public class TargetDummy
{
    public string Name { get; set; } = "训练木桩";
    
    // 木桩随身携带的标签贴纸盒子（GameplayTagContainer）
    public GameplayTagContainer Tags { get; } = new();

    // 尝试给木桩挂减速 Debuff
    public bool TryApplyDebuff(GameplayTag debuffTag)
    {
        Console.WriteLine($"--> 尝试给【{Name}】附加标签: 【{debuffTag.TagName}】");

        // 核心判定：查一下木桩身上有没有带 "Immune" 开头的免疫标签！
        var immuneCrowdControl = GameplayTagManager.Instance.RequestTag("Immune.CrowdControl");
        
        // HasTag 支持父子层级匹配！
        if (Tags.HasTag(immuneCrowdControl))
        {
            Console.WriteLine($"  ★ [标签拦截！] {Name} 拥有【霸体免控 (Immune.CrowdControl)】标签，免疫一切控制！\n");
            return false;
        }

        // 没霸体，成功贴上减速贴纸
        Tags.Add(debuffTag);
        Console.WriteLine($"  √ [附加成功] {Name} 成功贴上【{debuffTag.TagName}】标签！当前身上标签总数: {Tags.Count}\n");
        return true;
    }
}

internal static class Program
{
    private static void Main()
    {
        Console.WriteLine("=== 官方 GameplayTags 玩法标签系统实战 ===\n");

        var dummy = new TargetDummy();

        // 1. 获取/注册官方标签（树状层级结构，用点号 . 分隔）
        var slowTag = GameplayTagManager.Instance.RequestTag("Debuff.CrowdControl.Slow");
        var immuneTag = GameplayTagManager.Instance.RequestTag("Immune.CrowdControl");

        // -------------------------------------------------------------
        // 测试场景 1：普通状态下，吃了一发减速
        // -------------------------------------------------------------
        Console.WriteLine("【回合 1】木桩身上没有任何防御标签：");
        dummy.TryApplyDebuff(slowTag);

        // -------------------------------------------------------------
        // 测试场景 2：给木桩贴上【霸体】标签，再次尝试减速！
        // -------------------------------------------------------------
        Console.WriteLine("【回合 2】Boss 狂暴！贴上【霸体免控】标签：");
        dummy.Tags.Add(immuneTag);
        Console.WriteLine($"[Buff广播] 木桩获得了【{immuneTag.TagName}】状态！");

        // 再次尝试给它减速
        dummy.TryApplyDebuff(slowTag);

        Console.WriteLine("=== GameplayTags 演练结束 ===");
    }
}
