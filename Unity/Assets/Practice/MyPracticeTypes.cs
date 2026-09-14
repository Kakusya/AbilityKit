using System;
using System.Collections.Generic;
using AbilityKit.Modifiers;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.MyPractice
{
    // 假人木桩
    public class TargetDummy
    {
        public string Name { get; set; } = "训练木桩";
        public float Hp { get; set; } = 1000f;
        public float MaxHp { get; set; } = 1000f;
        public float BaseMoveSpeed { get; set; } = 100f;
        public List<ModifierData> Modifiers { get; } = new();

        public void TakeDamage(float damage)
        {
            Hp = Math.Max(0, Hp - damage);
        }
    }

    // 伤害事件
    public readonly struct DamageEvent
    {
        public readonly float Amount;
        public DamageEvent(float amount) => Amount = amount;
    }

    // 技能背包
    public class MySkillContext : IAbilityPipelineContext
    {
        public AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        public EAbilityPipelineState PipelineState { get; set; }
        public bool IsAborted { get; set; }
        public bool IsPaused { get; set; }
        public float StartTime { get; set; }
        public float ElapsedTime { get; set; }
        public object AbilityInstance { get; set; }
        public Dictionary<string, object> SharedData { get; } = new();

        public T GetData<T>(string key, T defaultValue = default) => SharedData.TryGetValue(key, out var v) && v is T t ? t : defaultValue;
        public void SetData<T>(string key, T value) => SharedData[key] = value;
        public bool TryGetData<T>(string key, out T value)
        {
            if (SharedData.TryGetValue(key, out var v) && v is T t) { value = t; return true; }
            value = default;
            return false;
        }
        public bool RemoveData(string key) => SharedData.Remove(key);
        public void ClearData() => SharedData.Clear();
        public void Reset() => SharedData.Clear();
    }

    // 管线与配置
    public class MySkillPipeline : AbilityPipeline<MySkillContext>
    {
        protected override void ReleaseContext(MySkillContext context) { }
    }

    public class MySkillConfig : IAbilityPipelineConfig
    {
        public int ConfigId => 1;
        public string ConfigName => "Fireball";
        public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => Array.Empty<IAbilityPhaseConfig>();
        public bool AllowInterrupt => true;
        public bool AllowPause => true;
    }
}
