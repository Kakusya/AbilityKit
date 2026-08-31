using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    public enum MobaRuntimeLogLevel
    {
        Off = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
        Trace = 5,
    }

    public enum MobaRuntimeLogModule
    {
        Unknown = 0,
        Bootstrap = 1,
        Config = 2,
        Session = 3,
        Input = 4,
        Gameplay = 5,
        Skill = 6,
        Buff = 7,
        Projectile = 8,
        Summon = 9,
        Area = 10,
        Combat = 11,
        Triggering = 12,
        Motion = 13,
        Snapshot = 14,
        Diagnostics = 15,
        AI = 16,
    }

    public enum MobaRuntimeLogPurpose
    {
        Runtime = 0,
        Lifecycle = 1,
        Validation = 2,
        Rejection = 3,
        Configuration = 4,
        Performance = 5,
        Exception = 6,
        RuntimeTrace = 7,
        Investigation = 8,
        AIInvestigation = 9,
        ConfiguredActionDebug = 10,
    }

    public readonly struct MobaRuntimeLogContext
    {
        public readonly MobaRuntimeLogModule Module;
        public readonly MobaRuntimeLogPurpose Purpose;
        public readonly string Owner;

        public MobaRuntimeLogContext(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner = null)
        {
            Module = module;
            Purpose = purpose;
            Owner = owner;
        }
    }

    public static class MobaRuntimeLog
    {
        private static readonly Dictionary<string, int> s_counts = new Dictionary<string, int>(64);

        public static MobaRuntimeLogLevel MinimumLevel { get; set; } = MobaRuntimeLogLevel.Info;
        public static bool EnableRuntimeTraceLogs { get; set; }
        public static bool EnableInvestigationLogs { get; set; }
        public static bool EnableAIInvestigationLogs { get; set; }
        public static bool EnableConfiguredActionDebugLogs { get; set; }
        public static bool EnableLifecycleInfoLogs { get; set; } = true;
        public static bool EnableConfigurationInfoLogs { get; set; } = true;

        public static MobaRuntimeLogContext Context(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner = null)
        {
            return new MobaRuntimeLogContext(module, purpose, owner);
        }

        public static void Info(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            Write(MobaRuntimeLogLevel.Info, module, purpose, owner, message);
        }

        public static void Info(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Info, module, purpose, owner, messageFactory);
        }

        public static void Debug(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            Write(MobaRuntimeLogLevel.Debug, module, purpose, owner, message);
        }

        public static void Debug(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Debug, module, purpose, owner, messageFactory);
        }

        public static void Trace(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            Write(MobaRuntimeLogLevel.Trace, module, purpose, owner, message);
        }

        public static void Trace(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Trace, module, purpose, owner, messageFactory);
        }

        public static bool IsEnabled(MobaRuntimeLogLevel level, MobaRuntimeLogPurpose purpose)
        {
            return ShouldLog(level, purpose);
        }

        public static bool IsEnabled(MobaRuntimeLogLevel level, in MobaRuntimeLogContext context)
        {
            return ShouldLog(level, context.Purpose);
        }

        public static void Warning(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            Write(MobaRuntimeLogLevel.Warning, module, purpose, owner, message);
        }

        public static void Warning(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Warning, module, purpose, owner, messageFactory);
        }

        public static void Error(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            Write(MobaRuntimeLogLevel.Error, module, purpose, owner, message);
        }

        public static void Error(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Error, module, purpose, owner, messageFactory);
        }

        public static void Exception(Exception exception, MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            if (exception == null) return;
            if (!ShouldLog(MobaRuntimeLogLevel.Error, purpose)) return;
            AbilityKit.Core.Logging.Log.Exception(exception, Format(module, purpose, owner, message));
        }

        public static void Info(in MobaRuntimeLogContext context, string message)
        {
            Write(MobaRuntimeLogLevel.Info, context.Module, context.Purpose, context.Owner, message);
        }

        public static void Info(in MobaRuntimeLogContext context, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Info, context.Module, context.Purpose, context.Owner, messageFactory);
        }

        public static void Warning(in MobaRuntimeLogContext context, string message)
        {
            Write(MobaRuntimeLogLevel.Warning, context.Module, context.Purpose, context.Owner, message);
        }

        public static void Warning(in MobaRuntimeLogContext context, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Warning, context.Module, context.Purpose, context.Owner, messageFactory);
        }

        public static void Error(in MobaRuntimeLogContext context, string message)
        {
            Write(MobaRuntimeLogLevel.Error, context.Module, context.Purpose, context.Owner, message);
        }

        public static void Error(in MobaRuntimeLogContext context, Func<string> messageFactory)
        {
            Write(MobaRuntimeLogLevel.Error, context.Module, context.Purpose, context.Owner, messageFactory);
        }

        public static void WarningOnce(string key, MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            if (!ShouldLogOnce(key)) return;
            Warning(module, purpose, owner, message);
        }

        public static void WarningOnce(string key, MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            if (!ShouldLogOnce(key)) return;
            Warning(module, purpose, owner, messageFactory);
        }

        public static void ResetCounters()
        {
            s_counts.Clear();
        }

        private static void Write(MobaRuntimeLogLevel level, MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, Func<string> messageFactory)
        {
            if (messageFactory == null) return;
            if (!ShouldLog(level, purpose)) return;

            WriteEnabled(level, module, purpose, owner, messageFactory());
        }

        private static void Write(MobaRuntimeLogLevel level, MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            if (!ShouldLog(level, purpose)) return;

            WriteEnabled(level, module, purpose, owner, message);
        }

        private static void WriteEnabled(MobaRuntimeLogLevel level, MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            var formatted = Format(module, purpose, owner, message);
            switch (level)
            {
                case MobaRuntimeLogLevel.Error:
                    AbilityKit.Core.Logging.Log.Error(formatted);
                    break;
                case MobaRuntimeLogLevel.Warning:
                    AbilityKit.Core.Logging.Log.Warning(formatted);
                    break;
                case MobaRuntimeLogLevel.Info:
                case MobaRuntimeLogLevel.Debug:
                case MobaRuntimeLogLevel.Trace:
                    AbilityKit.Core.Logging.Log.Info(formatted);
                    break;
            }
        }

        private static bool ShouldLog(MobaRuntimeLogLevel level, MobaRuntimeLogPurpose purpose)
        {
            if (level == MobaRuntimeLogLevel.Off || MinimumLevel == MobaRuntimeLogLevel.Off) return false;
            if (level > MinimumLevel) return false;

            if (purpose == MobaRuntimeLogPurpose.Lifecycle && level == MobaRuntimeLogLevel.Info) return EnableLifecycleInfoLogs;
            if (purpose == MobaRuntimeLogPurpose.Configuration && level == MobaRuntimeLogLevel.Info) return EnableConfigurationInfoLogs;
            if (purpose == MobaRuntimeLogPurpose.RuntimeTrace) return EnableRuntimeTraceLogs;
            if (purpose == MobaRuntimeLogPurpose.Investigation) return EnableInvestigationLogs;
            if (purpose == MobaRuntimeLogPurpose.AIInvestigation) return EnableAIInvestigationLogs;
            if (purpose == MobaRuntimeLogPurpose.ConfiguredActionDebug) return EnableConfiguredActionDebugLogs;

            return true;
        }

        private static bool ShouldLogOnce(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "default";
            s_counts.TryGetValue(key, out var count);
            if (count > 0) return false;
            s_counts[key] = count + 1;
            return true;
        }

        private static string Format(MobaRuntimeLogModule module, MobaRuntimeLogPurpose purpose, string owner, string message)
        {
            if (string.IsNullOrEmpty(owner)) owner = "Runtime";
            return $"[MobaRuntime][{module}][{purpose}][{owner}] {message}";
        }
    }
}
