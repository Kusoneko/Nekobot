using Discord;
using Nekobot.Commands;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Nekobot
{
    public static class Extensions
    {
        public static Task Reply(this DiscordClient client, CommandEventArgs e, string text)
            => Reply(client, e.User, e.Channel, text);
        public async static Task Reply(this DiscordClient client, User user, Channel channel, string text)
        {
            if (text != null)
            {
                if (!channel.IsPrivate)
                    await client.SendMessage(channel, $"{user.Name}: {text}");
                else
                    await client.SendMessage(channel, text);
            }
        }
        public static Task Reply<T>(this DiscordClient client, CommandEventArgs e, string prefix, T obj)
            => Reply(client, e.User, e.Channel, prefix, obj != null ? JsonConvert.SerializeObject(obj, Formatting.Indented) : "null");
        public static Task Reply<T>(this DiscordClient client, User user, Channel channel, string prefix, T obj)
            => Reply(client, user, channel, prefix, obj != null ? JsonConvert.SerializeObject(obj, Formatting.Indented) : "null");
        public static Task Reply(this DiscordClient client, CommandEventArgs e, string prefix, string text)
            => Reply(client, e.User, e.Channel, (prefix != null ? $"{Format.Bold(prefix)}:\n" : "\n") + text);
        public static Task Reply(this DiscordClient client, User user, Channel channel, string prefix, string text)
            => Reply(client, user, channel, (prefix != null ? $"{Format.Bold(prefix)}:\n" : "\n") + text);

        public static Task ReplyError(this DiscordClient client, CommandEventArgs e, string text)
            => Reply(client, e.User, e.Channel, "Error: " + text);
        public static Task ReplyError(this DiscordClient client, User user, Channel channel, string text)
            => Reply(client, user, channel, "Error: " + text);
        public static Task ReplyError(this DiscordClient client, CommandEventArgs e, Exception ex)
            => Reply(client, e.User, e.Channel, "Error: " + ex.GetBaseException().Message);
        public static Task ReplyError(this DiscordClient client, User user, Channel channel, Exception ex)
            => Reply(client, user, channel, "Error: " + ex.GetBaseException().Message);

        public static void Log(this DiscordClient client, LogMessageEventArgs e)
            => Log(client, e.Severity, e.Source, e.Message, e.Exception);
        public static void Log(this DiscordClient client, LogSeverity severity, string text, Exception ex = null)
            => Log(client, severity, null, text, ex);
        public static void Log(this DiscordClient client, LogSeverity severity, object source, string text, Exception ex = null)
        {
            char severityChar;
            ConsoleColor color;
            switch (severity)
            {
                case LogSeverity.Error:
                    severityChar = 'E';
                    color = ConsoleColor.Red;
                    break;
                case LogSeverity.Warning:
                    severityChar = 'W';
                    color = ConsoleColor.Yellow;
                    break;
                case LogSeverity.Info:
                    severityChar = 'I';
                    color = ConsoleColor.White;
                    break;
                case LogSeverity.Verbose:
                    severityChar = 'V';
                    color = ConsoleColor.Gray;
                    break;
                case LogSeverity.Debug:
                    severityChar = 'D';
                    color = ConsoleColor.DarkGray;
                    break;
                default:
                    severityChar = '?';
                    color = ConsoleColor.Gray;
                    break;
            }

            if (source != null)
                text = $"[{source}] {text}";
            if (ex != null)
                text = $"{text}: {ex.GetBaseException().Message}";
            if (severity <= LogSeverity.Info || (source != null && source is string))
            {
                LogOutput(text, color);
            }

            text = $"{severityChar} {text}";
            Debug.WriteLine(text);
        }

        static object log = new object();
        internal static void LogOutput(string text, ConsoleColor color)
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

    internal static class InternalExtensions
    {
        public static Task<User[]> FindUsers(this DiscordClient client, CommandEventArgs e, string username, string discriminator)
            => FindUsers(client, e, username, discriminator, false);
        public static async Task<User> FindUser(this DiscordClient client, CommandEventArgs e, string username, string discriminator)
            => (await FindUsers(client, e, username, discriminator, true))?[0];
        public static async Task<User[]> FindUsers(this DiscordClient client, CommandEventArgs e, string username, string discriminator, bool singleTarget)
        {
            IEnumerable<User> users;
            if (discriminator == "")
                users = client.FindUsers(e.Server, username);
            else
            {
                var user = client.GetUser(e.Server, username, ushort.Parse(discriminator));
                if (user == null)
                    users = Enumerable.Empty<User>();
                else
                    users = new User[] { user };
            }

            int count = users.Count();
            if (singleTarget)
            {
                if (count == 0)
                {
                    await client.ReplyError(e, "User was not found.");
                    return null;
                }
                else if (count > 1)
                {
                    await client.ReplyError(e, "Multiple users were found with that username.");
                    return null;
                }
            }
            else
            {
                if (count == 0)
                {
                    await client.ReplyError(e, "No user was found.");
                    return null;
                }
            }
            return users.ToArray();
        }

        public static async Task<User> GetUser(this DiscordClient client, CommandEventArgs e, ulong userId)
        {
            var user = client.GetUser(e.Server, userId);

            if (user == null)
            {
                await client.ReplyError(e, "No user was not found.");
                return null;
            }
            return user;
        }
        
        public static async Task<Channel> FindChannel(this DiscordClient client, CommandEventArgs e, string name, ChannelType type = null)
        {
            var channels = client.FindChannels(e.Server, name, type);

            int count = channels.Count();
            if (count == 0)
            {
                await client.ReplyError(e, "Channel was not found.");
                return null;
            }
            else if (count > 1)
            {
                await client.ReplyError(e, "Multiple channels were found with that name.");
                return null;
            }
            return channels.FirstOrDefault();
        }
    }
}
