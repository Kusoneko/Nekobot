using Discord;
using System;
using System.Linq;
using System.Threading.Tasks;
using RestSharp;

namespace Nekobot
{
    internal static class Helpers
    {
        internal static RestClient GetRestClient(string baseUri)
            => new RestClient(baseUri) { UserAgent = Console.Title };

        internal static int GetPermissions(User user, Channel channel)
        {
            if (user.Id == Program.masterId)
                return 10;
            return SQL.ExecuteScalarPos($"select count(perms) from users where user = '{user.Id}'")
                ? SQL.ReadInt(SQL.ReadUser(user.Id, "perms")) : 0;
        }

        internal static void OnOffCmd(Commands.CommandEventArgs e, Action<bool> action, string failmsg = null)
        {
            var arg = e.Args[0].ToLower();
            bool on = arg == "on";
            if (on || arg == "off") action(on);
            else e.Channel.SendMessage(failmsg ?? $"{e.User.Mention}, '{string.Join(" ", e.Args)}' isn't a valid argument. Please use on or off instead.");
        }

        internal static bool CanSay(Channel c, User u) => c.IsPrivate || u.GetPermissions(c).SendMessages;
        internal static bool CanSay(ref Channel c, User u, Channel old)
        {
            if (CanSay(c, u))
                return true;
            c = old;
            return false;
        }

        internal static System.Collections.Generic.IEnumerable<string> GraphemeClusters(string s)
        {
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(s);
            while (enumerator.MoveNext()) yield return (string)enumerator.Current;
        }

        internal static async Task PerformAction(Commands.CommandEventArgs e, string action, string reaction, bool perform_when_empty)
        {
            bool mentions_neko = e.Message.IsMentioningMe();
            string message = $"{e.User.Mention} {action}s ";
            bool mentions_everyone = e.Message.MentionedRoles.Contains(e.Server.EveryoneRole);
            if (mentions_everyone)
                await e.Channel.SendMessage(message + e.Server.EveryoneRole.Mention);
            else
            {
                if (e.Message.MentionedUsers.Count() == (mentions_neko ? 1 : 0))
                    message = perform_when_empty ? $"*{action}s {e.User.Mention}.*" : message + e.Server.CurrentUser.Mention;
                else
                    foreach (User u in e.Message.MentionedUsers)
                        message += u.Mention + ' ';
                await e.Channel.SendMessage(message);
            }
            if (mentions_everyone || mentions_neko || (!perform_when_empty && !e.Message.MentionedUsers.Any()))
                await e.Channel.SendMessage(reaction);
        }

        internal static Func<Message, DateTime> MsgTime => msg => msg.Timestamp;

        internal static async Task DoToMessages(Channel c, int few, Func<IOrderedEnumerable<Message>, bool, int> perform)
        {
            var msgs = c.Messages.OrderByDescending(MsgTime);
            var donecount = perform(msgs, true); // Let them know this contains this message.
            while (donecount < few)
            {
                msgs = (await c.DownloadMessages(relativeMessageId: msgs.Last().Id)).OrderByDescending(MsgTime);
                donecount += perform(msgs, false);
                if (msgs.Count() < 100) break; // We must be at the end.
            }
        }
        internal static string ZeroPadding(float count)
        {
            string ret = "";
            while ((count/=10) >= 1)
                ret += '0';
            return ret;
        }
        internal static string ZeroPaddingAt(int i, ref string padding)
            => (i % 10) == 0 ? padding = padding.Substring(1) : padding;

        internal static bool HasArg(string[] args, int index = 0)
            => args.Length > index && args[index] != "";

        internal static void Remove<T, V>(System.Collections.Concurrent.ConcurrentDictionary<T, V> tv, T t)
        {
            V v;
            tv.TryRemove(t, out v);
        }

        internal static string Pick(string[] quotes) => quotes[new Random().Next(0, quotes.Count())];
    }
}
