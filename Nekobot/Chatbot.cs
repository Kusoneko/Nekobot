using System.Collections.Concurrent;
using System.Linq;
using Discord;
using ChatterBotAPI;
using Nekobot.Commands.Permissions.Levels;

namespace Nekobot
{
    class Chatbot
    {
        static ConcurrentDictionary<ulong, ChatterBotSession> chatbots = new ConcurrentDictionary<ulong, ChatterBotSession>();

        static bool HasNeko(ref string msg, string neko)
        {
            if (msg.ToLower().IndexOf(neko.ToLower()) != -1)
            {
                msg = msg.Replace(neko, "");
                return true;
            }
            return false;
        }
        internal static async System.Threading.Tasks.Task Do(MessageEventArgs e)
        {
            if (chatbots.Count() == 0) return; // No bot sessions
            string msg = e.Message.Text;
            string neko = e.Channel.IsPrivate ? "" : e.Server.CurrentUser.Name;
            if (chatbots.ContainsKey(e.Channel.Id) && (e.Channel.IsPrivate || HasNeko(ref msg, neko) || HasNeko(ref msg, System.Text.RegularExpressions.Regex.Replace(neko, @"\p{Cs}", ""))))
            {
                string chat;
                lock (chatbots[e.Channel.Id]) chat = chatbots[e.Channel.Id].Think(msg); // Think in order.
                chat = System.Net.WebUtility.HtmlDecode(chat);
                for (int i = 10; i != 0; --i) try { await e.Channel.SendMessage(chat); break; }
                    catch (Discord.Net.HttpException ex) { if (i == 1) Log.Write(LogSeverity.Error, $"{ex.Message}\nCould not SendMessage to {(e.Channel.IsPrivate ? "private" : "public")} channel {e.Channel} in response to {e.User}'s message: {e.Message.Text}"); }
            }
        }

        internal static void Load()
        {
            var reader = SQL.ReadChannels("chatbot <> -1", "channel,chatbot");
            while (reader.Read())
                chatbots[System.Convert.ToUInt64(reader["channel"].ToString())] =
                    CreateBotSession((ChatterBotType)System.Convert.ToInt32(reader["chatbot"]));
        }

        static int GetBotType(string bottype)
            => (int)(/*bottype == "pandora" ? ChatterBotType.PANDORABOTS :*/ bottype == "jabberwacky" ? ChatterBotType.JABBERWACKY : ChatterBotType.CLEVERBOT);

        static ChatterBotSession CreateBotSession(ChatterBotType type) => new ChatterBotFactory().Create(type).CreateSession();

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("bot")
                .Alias("chatbot")
                .Parameter("on or off", Commands.ParameterType.Optional)
                .Parameter("type (clever or jabberwacky)", Commands.ParameterType.Optional)
                .MinPermissions(3)
                .Description("I'll turn on/off the chatbot for this channel.\nIf no args, I'll tell you if there's a bot on for this channel.")
                .Do(e =>
                {
                    bool botstatus = chatbots.ContainsKey(e.Channel.Id);
                    if (e.Args.Any())
                    {
                        Helpers.OnOffCmd(e, on =>
                        {
                            if (botstatus == on)
                                e.Channel.SendMessage("The bot is already " + (botstatus ? "on" : "off") + $" for {e.Channel}");
                            else
                            {
                                int bottype = -1;
                                if (botstatus)
                                    Helpers.Remove(chatbots, e.Channel.Id);
                                else
                                {
                                    bottype = GetBotType(e.Args.Count() == 1 ? "" : e.Args[0]);
                                    chatbots[e.Channel.Id] = CreateBotSession((ChatterBotType)bottype);
                                }
                                e.Channel.SendMessage("The bot is now " + (!botstatus ? "on" : "off") + $" for {e.Channel}");
                                SQL.AddOrUpdateFlag(e.Channel.Id, "chatbot", bottype.ToString());
                            }
                        }, "First argument must be on or off.");
                    }
                    else e.Channel.SendMessage("The bot is currently " + (botstatus ? "on" : "off") + $" for {e.Channel}.");
                });
        }
    }
}
