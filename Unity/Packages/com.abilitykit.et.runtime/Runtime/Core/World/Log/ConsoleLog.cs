using System;

namespace ET
{
    /// <summary>
    /// 控制台日志实现，用作 Logger 未初始化时的 Fallback
    /// </summary>
    public class ConsoleLog : ILog
    {
        private static readonly object _lock = new();

        public void Trace(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"[TRACE] {message}");
                Console.ResetColor();
            }
        }

        public void Warning(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARN] {message}");
                Console.ResetColor();
            }
        }

        public void Info(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[INFO] {message}");
                Console.ResetColor();
            }
        }

        public void Debug(string message)
        {
#if DEBUG
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"[DEBUG] {message}");
                Console.ResetColor();
            }
#endif
        }

        public void Error(string message)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {message}");
                Console.ResetColor();
            }
        }

        public void Error(Exception e)
        {
            lock (_lock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {e.Message}");
                Console.WriteLine(e.StackTrace);
                Console.ResetColor();
            }
        }

#if DOTNET
        public void Trace(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            Trace(message.ToStringAndClear());
        }

        public void Warning(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            Warning(message.ToStringAndClear());
        }

        public void Info(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            Info(message.ToStringAndClear());
        }

        public void Debug(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
#if DEBUG
            Debug(message.ToStringAndClear());
#endif
        }

        public void Error(ref System.Runtime.CompilerServices.DefaultInterpolatedStringHandler message)
        {
            Error(message.ToStringAndClear());
        }
#endif
    }
}
