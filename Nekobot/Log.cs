using Discord;
using Discord.Logging;
using System;
using System.Linq;
using System.Text;

namespace Nekobot
{
    partial class Program
    {
        internal static LogManager log => client.Log;
    }
    class Log
    {
        static LogMessageEventArgs Args(LogSeverity s, string msg, Exception e = null) => new LogMessageEventArgs(s, null, msg, e);
        public static void Write(LogSeverity s, string msg, Exception e = null) => Write(Args(s, msg, e));
        public static void Write(LogMessageEventArgs e)
        {
#if !DEBUG
            if (e.Severity > LogSeverity.Info || e.Severity > Program.Config.LogLevel) return;
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
            string sourceName = e.Source?.ToString();

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
            if (e.Severity <= Program.Config.LogLevel)
#endif
            {
                Output(text, color);
            }

#if DEBUG
            System.Diagnostics.Debug.WriteLine(text);
#endif
        }

        static object log = new object();
        internal static void Output(string text, ConsoleColor color)
        {
            lock (log)
            {
                ConsoleColor c = ConsoleColor.White;
                Console.ForegroundColor = color;
                Console.WriteLine(text);
                Console.ForegroundColor = c;
            }
        }
    }
}
