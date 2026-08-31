#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Demo.Moba.Share.Config;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor.Hero
{
    /// <summary>
    /// 分析 characters.json / 现有 SO 表，给 wizard 提供"下一个可用 HeroId / SkillId"。
    /// 规则：
    ///   * HeroId 从 1001 起、每次加 1；若已存在指定 HeroId 则抛出。
    ///   * SkillId 从 HeroId × 1000 + 1 起按顺序占用 4 个槽位（基础攻击 + 3 个主动技能）。
    ///   * 找不到 characters.json 时按基础值 (default 1001) 起步。
    /// </summary>
    public static class HeroIdAllocator
    {
        public const int FirstHeroId = 1001;
        public const int SkillsPerHero = 4; // 基础攻击 + 3 主动
        public const int DefaultBasicAttackSuffix = 11;
        public const int DefaultActiveSkillSuffixStart = 1;

        public readonly struct AllocatedIds
        {
            public readonly int HeroId;
            public readonly int BasicAttackSkillId;
            public readonly int[] ActiveSkillIds;

            public AllocatedIds(int heroId, int basicAttackSkillId, int[] activeSkillIds)
            {
                HeroId = heroId;
                BasicAttackSkillId = basicAttackSkillId;
                ActiveSkillIds = activeSkillIds;
            }
        }

        /// <summary>
        /// 返回一个可用的 HeroId：当前 max(现存 HeroId) + 1，至少 FirstHeroId。
        /// 若 charactersPath 为空或文件不存在，则返回 FirstHeroId。
        /// </summary>
        public static int ProposeNextHeroId(string charactersJsonPath)
        {
            var existing = LoadExistingHeroIds(charactersJsonPath);
            return NextHeroIdFrom(existing, FirstHeroId);
        }

        /// <summary>
        /// 给出完整的 ID 分配：HeroId / BasicAttack / Active skill ids（按 N 个索引分配）。
        /// 不依赖文件系统（可被单测调用）。
        /// </summary>
        public static AllocatedIds Allocate(int heroId, int activeSkillCount)
        {
            if (heroId <= 0) throw new ArgumentOutOfRangeException(nameof(heroId));
            if (activeSkillCount < 0 || activeSkillCount > 1024)
                throw new ArgumentOutOfRangeException(nameof(activeSkillCount), "0..1024");

            var suffixBasic = DefaultBasicAttackSuffix;
            var basicSkillId = heroId * 1000 + suffixBasic;
            var activeSkillIds = new int[activeSkillCount];
            // 默认第一个技能 slot = 1（与既有 10040101/10040102/10040103 一致）
            for (var i = 0; i < activeSkillCount; i++)
            {
                activeSkillIds[i] = heroId * 1000 + DefaultActiveSkillSuffixStart + i;
            }
            return new AllocatedIds(heroId, basicSkillId, activeSkillIds);
        }

        private static HashSet<int> LoadExistingHeroIds(string charactersJsonPath)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(charactersJsonPath) || !File.Exists(charactersJsonPath)) return set;

            try
            {
                var json = File.ReadAllText(charactersJsonPath);
                var arr = JsonConvert.DeserializeObject<CharacterDTO[]>(json);
                if (arr == null) return set;
                for (var i = 0; i < arr.Length; i++)
                {
                    if (arr[i] != null && arr[i].Id > 0) set.Add(arr[i].Id);
                }
            }
            catch (Exception)
            {
                // 文件损坏时按空集处理，避免阻塞 wizard
            }
            return set;
        }

        private static int NextHeroIdFrom(HashSet<int> existing, int baseId)
        {
            var candidate = baseId;
            while (existing.Contains(candidate))
            {
                candidate++;
                if (candidate > 99999) throw new InvalidOperationException("HeroId 超出可用范围");
            }
            return candidate;
        }
    }
}
#endif
