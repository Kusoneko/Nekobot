using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Discord;
using ChatterBotAPI;
using Nekobot.Commands.Permissions.Levels;

namespace Nekobot
{
    partial class Program
    {
        public static Dictionary<long, ChatterBotSession> chatbots = new Dictionary<long, ChatterBotSession>();

        private static async void DoChatBot(MessageEventArgs e)
        {
            if (chatbots.Count() == 0) return; // No bot sessions
            string msg = e.Message.Text;
            // It's lame we have to do this, but our User isn't exposed by Discord.Net, so we don't know our name
            string neko = e.Channel.IsPrivate ? "" : client.GetUser(e.Server, client.CurrentUserId).Name;
            if (chatbots.ContainsKey(e.Channel.Id) && (e.Channel.IsPrivate || msg.ToLower().IndexOf(neko.ToLower()) != -1))
            {
                if (!e.Channel.IsPrivate)
                    msg = msg.Replace(neko, "");
                await client.SendMessage(e.Channel, chatbots[e.Channel.Id].Think(msg));
            }
        }

        private static void LoadChatBots()
        {
            SQLiteDataReader reader = ExecuteReader("select channel,chatbot from flags where chatbot <> -1");
            while (reader.Read())
                chatbots[System.Convert.ToInt64(reader["channel"].ToString())] =
                    CreateBotSession((ChatterBotType)System.Convert.ToInt32(reader["chatbot"]));
        }

        static int GetBotType(string bottype)
        {
            return (int)(/*bottype == "pandora" ? ChatterBotType.PANDORABOTS :*/ bottype == "jabberwacky" ? ChatterBotType.JABBERWACKY : ChatterBotType.CLEVERBOT);
        }

        static ChatterBotSession CreateBotSession(ChatterBotType type)
        {
            return new ChatterBotFactory().Create(type).CreateSession();
        }

        public static void AddChatbotCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("bot")
                .Alias("chatbot")
                .Parameter("on or off", Commands.ParameterType.Optional)
                .Parameter("type (clever or jabberwacky)", Commands.ParameterType.Optional)
                .MinPermissions(3)
                .Description("I'll turn on/off the chatbot for this channel.\nIf no args, I'll tell you if there's a bot on for this channel.")
                .Do(async e =>
                {
                    bool botstatus = chatbots.ContainsKey(e.Channel.Id);
                    if (e.Args.Count() != 0)
                    {
                        bool on = e.Args[0] == "on";
                        bool off = !on && e.Args[0] == "off";
                        if (on || off)
                        {
                            if (botstatus == on || botstatus != off)
                                await client.SendMessage(e.Channel, "The bot is already " + (botstatus ? "on" : "off") + $" for {e.Channel}");
                            else
                            {
                                int bottype = -1;
                                if (botstatus)
                                    chatbots.Remove(e.Channel.Id);
                                else
                                {
                                    bottype = GetBotType(e.Args.Count() == 1 ? "" : e.Args[0]);
                                    chatbots[e.Channel.Id] = CreateBotSession((ChatterBotType)bottype);
                                }
                                await client.SendMessage(e.Channel, "The bot is now " + (!botstatus ? "on" : "off") + $" for {e.Channel}");
                                ExecuteNonQuery(ExecuteScalarPos($"select count(channel) from flags where channel = '{e.Channel.Id}'")
                                    ? $"update flags set chatbot={bottype} where channel='{e.Channel.Id}'"
                                    : $"insert into flags values ('{e.Channel.Id}', 0, 0, 0, {bottype})");
                            }
                        }
                        else await client.SendMessage(e.Channel, "First argument must be on or off.");
                    }
                    else await client.SendMessage(e.Channel, "The bot is currently " + (botstatus ? "on" : "off") + $" for {e.Channel}.");
                });
        }
    }
}
