using System;
using System.Diagnostics;

namespace ET
{
    public static class Log
    {
        private const int TraceLevel = 1;
        private const int DebugLevel = 2;
        private const int InfoLevel = 3;
        private const int WarningLevel = 4;

        private static ILog GetLog()
        {
            try
            {
                if (Fiber.Instance != null)
                {
                    return Fiber.Instance.Log ?? Logger.Instance.Log;
                }
                return Logger.Instance.Log;
            }
            catch
            {
                return null;
            }
        }
        
        private static bool IsLogLevelEnabled(int level)
        {
            var options = Options.Instance;
            return options == null || options.LogLevel <= level;
        }

        [Conditional("DEBUG")]
        public static void Debug(string msg)
        {
            if (!IsLogLevelEnabled(DebugLevel))
            {
                return;
            }

            var log = GetLog();
            log?.Debug(msg);
        }

        [Conditional("DEBUG")]
        public static void Trace(string msg)
        {
            if (!IsLogLevelEnabled(TraceLevel))
            {
                return;
            }
            StackTrace st = new(1, true);
            var log = GetLog();
            log?.Trace($"{msg}\n{st}");
        }

        public static void Info(string msg)
        {
            if (!IsLogLevelEnabled(InfoLevel))
            {
                return;
            }
            var log = GetLog();
            log?.Info(msg);
        }

        public static void TraceInfo(string msg)
        {
            if (!IsLogLevelEnabled(InfoLevel))
            {
                return;
            }
            StackTrace st = new(1, true);
            var log = GetLog();
            log?.Trace($"{msg}\n{st}");
        }

        public static void Warning(string msg)
        {
            if (!IsLogLevelEnabled(WarningLevel))
            {
                return;
            }
            var log = GetLog();
            log?.Warning(msg);
        }

        public static void Error(string msg)
        {
            StackTrace st = new(1, true);
            var log = GetLog();
            log?.Error($"{msg}\n{st}");
        }

        public static void Error(Exception e)
        {
            var log = GetLog();
            log?.Error(e.ToString());
        }
        
        public static void Console(string msg)
        {
            var options = Options.Instance;
            if (options != null && options.Console == 1)
            {
                System.Console.WriteLine(msg);
            }
            var log = GetLog();
            log?.Debug(msg);
        }

#if DOTNET
        [Conditional("DEBUG")]
        public static void Trace(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            if (!IsLogLevelEnabled(TraceLevel))
            {
                return;
            }
            StackTrace st = new(1, true);
            var log = GetLog();
            log?.Trace($"{message.ToStringAndClear()}\n{st.ToString()}");
        }
        [Conditional("DEBUG")]
        public static void Warning(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            if (!IsLogLevelEnabled(WarningLevel))
            {
                return;
            }
            var log = GetLog();
            log?.Warning(ref message);
        }

        public static void Info(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            if (!IsLogLevelEnabled(InfoLevel))
            {
                return;
            }
            var log = GetLog();
            log?.Info(ref message);
        }
        [Conditional("DEBUG")]
        public static void Debug(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            if (!IsLogLevelEnabled(DebugLevel))
            {
                return;
            }
            var log = GetLog();
            log?.Debug(ref message);
        }

        public static void Error(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            var log = GetLog();
            log?.Error(ref message);
        }
#endif
    }
}
