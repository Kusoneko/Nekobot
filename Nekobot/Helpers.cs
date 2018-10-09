using Discord;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Text;
using RestSharp;
using Nekobot.Commands;
using Nekobot.Commands.Permissions.Levels;
using Newtonsoft.Json.Linq;
using Discord.WebSocket;

namespace Nekobot
{
    internal static class Helpers
    {
        internal static RestClient GetRestClient(string baseUri)
            => new RestClient(baseUri) { UserAgent = Console.Title };

        internal static int GetPermissions(IUser user, IMessageChannel channel)
            => GetPermissions(user.Id, channel);
        internal static int GetPermissions(ulong user, IMessageChannel channel)
        {
            if (user == Program.masterId)
                return 10;
            return SQL.ExecuteScalarPos($"select count(perms) from users where user = '{user}'")
                ? SQL.ReadInt(SQL.ReadUser(user, "perms")) : 0;
        }

        internal static void OnOffCmd(CommandEventArgs e, Action<bool> action, string failmsg = null)
        {
            var arg = e.Args[0].ToLower();
            bool on = arg == "on";
            if (on || arg == "off") action(on);
            else e.Channel.SendMessageAsync(failmsg ?? $"{e.User.Mention}, '{string.Join(" ", e.Args)}' isn't a valid argument. Please use on or off instead.");
        }

        internal static bool CanSay(IMessageChannel c, IUser u)
        {
            if (c is IPrivateChannel || u.Id == Program.masterId)
                return true;
            var chan = c is IGuildChannel ? c as IGuildChannel : null;
            return chan == null ? true : chan.GetUserAsync(u.Id).Result.GetPermissions(chan).SendMessages;
        }
        internal static bool CanSay(ref IMessageChannel c, IUser u, IMessageChannel old)
        {
            if (CanSay(c, u))
                return true;
            c = old;
            return false;
        }

        internal static string ResolveTags(IMessage msg)
            => msg is SocketUserMessage && msg.Tags.Any() ? (msg as SocketUserMessage).Resolve() : msg.Content;

        internal static IEnumerable<string> GraphemeClusters(string s)
        {
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(s);
            while (enumerator.MoveNext()) yield return (string)enumerator.Current;
        }

        internal static string RemoveEmoji(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\p{Cs}", "");

        public static Task<IUserMessage> SendEmbed(CommandEventArgs args, EmbedBuilder builder)
            => args.Channel.SendMessageAsync(embed: builder.Build());

        public static Task<IUserMessage> SendEmbed(CommandEventArgs args, string description)
            => SendEmbed(args, EmbedBuilder.WithDescription(description));

        internal static object ToSHA1(string str) => SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(str));

        internal static async Task PerformAction(CommandEventArgs e, string action, string reaction, bool perform_when_empty)
        {
            bool mentions_neko = e.Message.MentionedUserIds.Contains(Program.Self.Id);
            string message = $"{e.User.Mention} {action}s ";
            bool mentions_everyone = !(e.Channel is IPrivateChannel) && e.Message.Tags.Any(t => t.Type == TagType.EveryoneMention);
            if (mentions_everyone)
                message += e.Server.EveryoneRole.Mention;
            else if (e.Channel is IPrivateChannel || (!e.Message.MentionedRoleIds.Any() && e.Message.MentionedUserIds.Count() == (mentions_neko ? 1 : 0)))
                message = perform_when_empty ? $"*{action}s {e.User.Mention}.*" : $"{message}me.";
            else
            {
                foreach (var t in e.Message.Tags)
                    if (t.Type == TagType.UserMention || t.Type == TagType.RoleMention)
                        message += (t.Value as IMentionable).Mention + ' ';
            }
            await e.Channel.SendMessageAsync(message);
            if (e.Channel is IPrivateChannel ? !perform_when_empty : (mentions_everyone || mentions_neko || (!perform_when_empty && !(e.Message.MentionedUserIds.Any() || e.Message.MentionedRoleIds.Any()))))
                await e.Channel.SendMessageAsync(reaction);
        }

        internal static void CommaSeparateRoleNames(CommandEventArgs e, Action<IEnumerable<SocketRole>, string> perform)
        {
            foreach (var str in e.Args[0].Split(','))
                perform((e.Server as SocketGuild).Roles.Where(r => r.Name.Contains(str)), str);
        }

        internal static string FileWithoutPath(string fullpath) => fullpath.Substring(fullpath.LastIndexOf('\\') + 1);

        internal static void CreateJsonCommand(CommandGroupBuilder group, KeyValuePair<string, JToken> cmdjson, Action<CommandBuilder, JToken> cmd_specific)
        {
            var cmd = group.CreateCommand(cmdjson.Key);
            var val = cmdjson.Value;
            if (FieldExists(val, "aliases")) foreach (var alias in val["aliases"]) cmd.Alias(alias.ToString());
            if (FieldExists(val, "description")) cmd.Description(val["description"].ToString());
            if (FieldExists(val, "permissions")) cmd.MinPermissions(val["permissions"].ToObject<int>());
            cmd_specific(cmd, val);
        }
        internal static JObject GetJsonFileIfExists(string file)
            => System.IO.File.Exists(file) ? JObject.Parse(System.IO.File.ReadAllText(file)) : null;

        internal static bool FieldExists(JToken map, string property)
            => map.ToObject<JObject>().Property(property) != null;
        internal static bool FieldExists(string map, string property)
            => FieldExists(Program.config[map], property);
        internal static T FieldExistsSafe<T>(JToken map, string property, T default_value = default(T))
            => FieldExists(map, property) ? map[property].ToObject<T>() : default_value;
        internal static T FieldExistsSafe<T>(string map, string property, T default_value = default(T))
            => FieldExistsSafe(Program.config, property, default_value);

        internal static JObject XmlToJson(string xml)
            => JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeXmlNode(new System.Xml.XmlDocument() { InnerXml = xml }));

        internal static TimeSpan Uptime() => DateTime.Now - Process.GetCurrentProcess().StartTime;

        internal static void Restart()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                WindowStyle = ProcessWindowStyle.Minimized,
                UseShellExecute = true
            });
            Environment.Exit(0);
        }

        internal static string Nickname(SocketGuildUser u) => string.IsNullOrEmpty(u.Nickname) ? u.Username : u.Nickname;

        internal static EmbedBuilder EmbedBuilder => Program.Cmds.EmbedBuilder();

        internal static EmbedBuilder EmbedDesc(string str) => EmbedBuilder.WithDescription(str);

        internal static async Task SendEmbed(IMessageChannel c, EmbedBuilder b)
        {
            if (b.Fields.Count != 0) await c.SendMessageAsync(embed: b.Build());
        }
        internal static void SendEmbedEarly(IMessageChannel c, ref EmbedBuilder b)
        {
            if (b.Fields.Count == EmbedBuilder.MaxFieldCount)
            {
                c.SendMessageAsync(embed: b.Build());
                b = EmbedBuilder;
            }
        }

        internal static async Task DoToMessages(SocketTextChannel c, int few, Func<IEnumerable<IMessage>, bool, int> perform)
        {
            var cachedmsgs = c.CachedMessages.OrderByDescending(msg => msg.Timestamp);
            var donecount = perform(cachedmsgs, true); // Let them know this contains this message.
            IMessage last = cachedmsgs.Last();
            while (donecount < few)
            {
                var msgs = (await c.GetMessagesAsync(last, Direction.Before).FlattenAsync()).OrderByDescending(msg => msg.Timestamp);
                donecount += perform(msgs, false);
                last = msgs.Last();
                if (msgs.Count() < DiscordConfig.MaxMessagesPerBatch) break; // We must be at the end.
            }
        }
        internal static string ZeroPadding(float count)
        {
            string ret = "";
            while ((count/=10) >= 1)
                ret += '0';
            return ret;
        }

        internal static bool HasArg(string[] args, int index = 0)
            => args.Length > index && args[index] != "";

        internal static void Remove<T, V>(System.Collections.Concurrent.ConcurrentDictionary<T, V> tv, T t)
            => tv.TryRemove(t, out V v);

        internal static string Pick(string[] quotes) => quotes[new Random().Next(0, quotes.Count())];
    }
}
