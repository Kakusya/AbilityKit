using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

public sealed class MobaHeroSpecificRuntimeArchitectureTests
{
    private static readonly string[] ProductionRoots =
    {
        "Packages/com.abilitykit.demo.moba.runtime/Runtime",
        "Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle",
    };

    private static readonly HashSet<string> AllowedConfigurationFiles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Packages/com.abilitykit.demo.moba.view.runtime/Runtime/Game/Battle/Bootstrap/Config/BattlePlayersConfigSO.cs",
        };

    private static readonly Regex HeroNamePattern = new Regex(
        "LianPo|XiaoQiao|ZhaoYun|Mozi|Daji|YingZheng|Yingzheng|SunShangXiang|SunShangxiang|"
        + "廉颇|小乔|赵云|墨子|妲己|嬴政|孙尚香",
        RegexOptions.CultureInvariant);

    private static readonly Regex HeroIdPattern = new Regex(
        @"(?<!\d)(?:100[1-7]|100[1-7]\d{4})(?!\d)",
        RegexOptions.CultureInvariant);

    [Test]
    public void ProductionRuntime_DoesNotContainHeroSpecificNamesOrIds()
    {
        var violations = EnumerateProductionFiles()
            .SelectMany(FindViolations)
            .ToArray();

        Assert.IsEmpty(
            violations,
            "Hero-specific production runtime logic is forbidden. Move hero identity, skill IDs, "
            + "trigger IDs, and presentation choices into configuration.\n"
            + string.Join("\n", violations));
    }

    private static IEnumerable<string> EnumerateProductionFiles()
    {
        return ProductionRoots
            .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            .Select(NormalizePath)
            .Where(path => path.IndexOf("/Test/", StringComparison.OrdinalIgnoreCase) < 0)
            .Where(path => !AllowedConfigurationFiles.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal);
    }

    private static IEnumerable<string> FindViolations(string path)
    {
        var lines = File.ReadAllLines(path);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!HeroNamePattern.IsMatch(line) && !HeroIdPattern.IsMatch(line)) continue;

            yield return path + ":" + (i + 1) + ": " + line.Trim();
        }
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
