using Discord;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Nekobot
{
    class Log
    {
        static LogMessage Args(LogSeverity s, string msg, Exception e = null, string source = null) => new LogMessage(s, source, msg, e);
        public static async Task Write(LogSeverity s, string msg, Exception e = null, string source = null) => await Write(Args(s, msg, e, source));
        public static async Task Write(LogMessage e)
        {
#if !DEBUG
            if (e.Severity > LogSeverity.Info || e.Severity > Program.LogLevel) return;
#endif

            //Color
            ConsoleColor color;
            switch (e.Severity)
            {
                case LogSeverity.Error: color = ConsoleColor.Red; break;
                case LogSeverity.Warning: color = ConsoleColor.Yellow; break;
                case LogSeverity.Info: color = ConsoleColor.White; break;
#if DEBUG
                case LogSeverity.Verbose: color = ConsoleColor.Gray; break;
                case LogSeverity.Debug:
#endif
                default: color = ConsoleColor.DarkGray; break;
            }

            // Exception
            string exMessage;
            Exception ex = e.Exception;
            if (ex != null)
            {
                while (ex is AggregateException && ex.InnerException != null)
                    ex = ex.InnerException;
                exMessage = ex.Message;
            }
            else
                exMessage = null;

            //Source
            string sourceName = e.Source;

            // Text
            string text;
            if (e.Message == null)
            {
                text = exMessage ?? "";
                exMessage = null;
            }
            else
                text = e.Message;

            //Build message
            StringBuilder builder = new StringBuilder(text.Length + (sourceName?.Length ?? 0) + (exMessage?.Length ?? 0) + 5);
            if (sourceName != null)
            {
                builder.Append('[');
                builder.Append(sourceName);
                builder.Append("] ");
            }
            foreach (var c in text)
            {
                if (!char.IsControl(c)) //Strip control chars
                    builder.Append(c);
            }
            if (exMessage != null)
            {
                builder.Append(": ");
                builder.Append(exMessage);
            }

            text = builder.ToString();
#if DEBUG
            if (e.Severity <= Program.LogLevel)
#endif
            {
                Output(text, color);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine(text);
#endif
            await Task.CompletedTask;
        }

        static object log = new object();
        internal static void Output(string text, ConsoleColor color = ConsoleColor.Blue)
        {
            if (text.Length == 0) return;

            lock (log)
            {
                Console.Write($"[{DateTime.Now.TimeOfDay}] ");
                ConsoleColor c = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = c;
            }
        }
    }
}
