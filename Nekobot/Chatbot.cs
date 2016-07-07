using System.Collections.Concurrent;
using System.Linq;
using Discord;
using Nekobot.Commands.Permissions.Levels;
using RestSharp;

namespace Nekobot
{
    class Chatbot
    {
        class Handler
        {
            ConcurrentDictionary<ulong, Session> chatbots = new ConcurrentDictionary<ulong, Session>();

            readonly string _user;
            readonly string _key;
            class Session
            {
                static string BadResponse(IRestResponse response, string pre)
                {
                    string ret = null;
                    if (response.StatusCode != System.Net.HttpStatusCode.OK)
                        Log.Write(LogSeverity.Error, ret = (pre+response.ErrorMessage));
                    return ret;
                }

                internal Session(string nick, string user, string key)
                {
                    rc.AddDefaultParameter("user", user);
                    rc.AddDefaultParameter("key", key);
                    rc.AddDefaultParameter("nick", nick);
                    var response = rc.Execute(new RestRequest("create", Method.POST));
                    rc.AddDefaultParameter("nick", nick);
                    BadResponse(response, "Creating chatbot session failed: ");
                }

                internal string Ask(string text)
                {
                    var req = new RestRequest("ask", Method.POST);
                    req.AddParameter("text", text);
                    var response = rc.Execute(req);
                    return BadResponse(response, "Responding to chat failed: ") ?? Newtonsoft.Json.Linq.JObject.Parse(response.Content)["response"].ToString();
                }
                RestClient rc = Helpers.GetRestClient("https://cleverbot.io/1.0");
            }

            #region HasNeko
            static bool HasNeko(ref string msg, string neko)
            {
                if (msg.ToLower().IndexOf(neko.ToLower()) != -1)
                {
                    msg = msg.Replace(neko, "");
                    return true;
                }
                return false;
            }

            static bool HasNekoEmojiOrNot(ref string msg, string neko) =>
                HasNeko(ref msg, neko) || HasNeko(ref msg, Helpers.RemoveEmoji(neko));

            static bool HasNekoNick(ref string msg, string nekonick) =>
                !string.IsNullOrEmpty(nekonick) && HasNekoEmojiOrNot(ref msg, nekonick);

            static bool HasNeko(ref string msg, User user)
            {
                string neko = user.Name;
                string nekonick = user.Nickname;
                if (HasNekoEmojiOrNot(ref msg, neko)) // Have we been mentioned by our actual name?
                {
                    HasNekoNick(ref msg, nekonick); // Strip nick, too, just in case.
                    return true;
                }
                return HasNekoNick(ref msg, nekonick); // Have we been mentioned by our nick?
            }
            #endregion

            async System.Threading.Tasks.Task Do(MessageEventArgs e)
            {
                if (chatbots.Count() == 0) return; // No bot sessions
                string msg = e.Message.Text;
                if (chatbots.ContainsKey(e.Channel.Id) && (e.Channel.IsPrivate || HasNeko(ref msg, e.Server.CurrentUser)))
                {
                    string chat;
                    lock (chatbots[e.Channel.Id]) chat = chatbots[e.Channel.Id].Ask(msg); // Ask in order.
                    chat = System.Net.WebUtility.HtmlDecode(chat);
                    await e.Channel.SendIsTyping();
                    for (int i = 10; i != 0; --i) try { await (e.Message.IsTTS ? e.Channel.SendTTSMessage(chat) : e.Channel.SendMessage(chat)); break; }
                        catch (Discord.Net.HttpException ex) { if (i == 1) Log.Write(LogSeverity.Error, $"{ex.Message}\nCould not SendMessage to {(e.Channel.IsPrivate ? "private" : "public")} channel {e.Channel} in response to {e.User}'s message: {e.Message.Text}"); }
                }
            }

            internal Handler(ulong client_id, string user, string key)
            {
                // Store credentials
                _user = user;
                _key = key;
                // Load the chatbots
                var reader = SQL.ReadChannels("chatbot <> -1");
                while (reader.Read())
                    CreateBot(client_id.ToString(), reader["channel"].ToString());
                // Register the handler
                Program.commands.NonCommands += e => System.Threading.Tasks.Task.Run(() => Do(e));
            }
            internal bool HasBot(ulong id) => chatbots.ContainsKey(id);
            internal void RemoveBot(ulong id) => Helpers.Remove(chatbots, id);
            internal void CreateBot(string client_id, string chat_id) => chatbots[System.Convert.ToUInt64(chat_id)] = new Session(client_id + chat_id, _user, _key);
        }

        internal static void AddDelayedCommands(Commands.CommandGroupBuilder group)
        {
            var creds = Program.config["CleverBot"];
            if (!Helpers.FieldExists(creds, "user")) // no credentials
              return;

            // Create the handler
            var handler = new Handler(group.Service.Client.CurrentUser.Id, creds["user"].ToString(), creds["key"].ToString());

            group.CreateCommand("bot")
                .Alias("chatbot")
                .Parameter("on or off", Commands.ParameterType.Optional)
                .MinPermissions(3)
                .Description("I'll turn on/off the chatbot for this channel.\nIf no args, I'll tell you if there's a bot on for this channel.")
                .Do(e =>
                {
                    bool botstatus = handler.HasBot(e.Channel.Id);
                    if (e.Args.Any())
                    {
                        Helpers.OnOffCmd(e, on =>
                        {
                            if (botstatus == on)
                                e.Channel.SendMessage("The bot is already " + (botstatus ? "on" : "off") + $" for {e.Channel}");
                            else
                            {
                                if (botstatus)
                                    handler.RemoveBot(e.Channel.Id);
                                else
                                    handler.CreateBot(group.Service.Client.CurrentUser.Id.ToString(), e.Channel.Id.ToString());
                                e.Channel.SendMessage("The bot is now " + (!botstatus ? "on" : "off") + $" for {e.Channel}");
                                SQL.AddOrUpdateFlag(e.Channel.Id, "chatbot", botstatus ? "-1" : "0");
                            }
                        });
                    }
                    else e.Channel.SendMessage("The bot is currently " + (botstatus ? "on" : "off") + $" for {e.Channel}.");
                });
        }
    }
}
