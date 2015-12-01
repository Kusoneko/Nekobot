using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Discord;
using ChatterBotAPI;
using Nekobot.Commands.Permissions.Levels;

namespace Nekobot
{
    class Chatbot
    {
        static Dictionary<long, ChatterBotSession> chatbots = new Dictionary<long, ChatterBotSession>();

        internal static async void Do(MessageEventArgs e)
        {
            if (chatbots.Count() == 0) return; // No bot sessions
            string msg = e.Message.Text;
            // It's lame we have to do this, but our User isn't exposed by Discord.Net, so we don't know our name
            string neko = e.Channel.IsPrivate ? "" : Program.client.GetUser(e.Server, Program.client.CurrentUserId).Name;
            if (chatbots.ContainsKey(e.Channel.Id) && (e.Channel.IsPrivate || msg.ToLower().IndexOf(neko.ToLower()) != -1))
            {
                if (!e.Channel.IsPrivate)
                    msg = msg.Replace(neko, "");
                await Program.client.SendMessage(e.Channel, chatbots[e.Channel.Id].Think(msg));
            }
        }

        internal static void Load()
        {
            SQLiteDataReader reader = SQL.ExecuteReader("select channel,chatbot from flags where chatbot <> -1");
            while (reader.Read())
                chatbots[System.Convert.ToInt64(reader["channel"].ToString())] =
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
                                await Program.client.SendMessage(e.Channel, "The bot is already " + (botstatus ? "on" : "off") + $" for {e.Channel}");
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
                                await Program.client.SendMessage(e.Channel, "The bot is now " + (!botstatus ? "on" : "off") + $" for {e.Channel}");
                                SQL.ExecuteNonQuery(SQL.ExecuteScalarPos($"select count(channel) from flags where channel = '{e.Channel.Id}'")
                                    ? $"update flags set chatbot={bottype} where channel='{e.Channel.Id}'"
                                    : $"insert into flags values ('{e.Channel.Id}', 0, 0, 0, {bottype})");
                            }
                        }
                        else await Program.client.SendMessage(e.Channel, "First argument must be on or off.");
                    }
                    else await Program.client.SendMessage(e.Channel, "The bot is currently " + (botstatus ? "on" : "off") + $" for {e.Channel}.");
                });
        }
    }
}
