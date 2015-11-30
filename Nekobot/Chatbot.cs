using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Discord;
using ChatterBotAPI;

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
    }
}
