using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Discord;
using Nekobot.Commands.Permissions.Levels;
using RestSharp;
using Discord.WebSocket;

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
                internal static string GetJsonProperty(IRestResponse response, string property)
                    => Newtonsoft.Json.Linq.JObject.Parse(response.Content)[property].ToString();

                static async Task<string> AvoidBadResponse(Func<IRestResponse> response, string pre, Func<IRestResponse, string> final = null, int i = 3)
                {
                    var resp = response();
                    Func<bool> bad = () => resp.StatusCode != HttpStatusCode.OK;
                    Func<string> err = () => pre + (resp.StatusCode == HttpStatusCode.BadRequest ? GetJsonProperty(resp, "status") : resp.ErrorMessage);
                    while (bad() && i != 0)
                    {
                        await Log.Write(LogSeverity.Error, err());
                        if (--i != 0) await Log.Write(LogSeverity.Error, "Retrying in ten seconds");
                        await Task.Delay(10000);
                        resp = response();
                    }
                    return bad() ? err() : final?.Invoke(resp);
                }

                internal Session(string nick, string user, string key)
                {
                    rc.AddDefaultParameter("user", user);
                    rc.AddDefaultParameter("key", key);
                    rc.AddDefaultParameter("nick", nick);
                    AvoidBadResponse(() => rc.Execute(new RestRequest("create", Method.POST)),
                        "Creating chatbot session failed: ").Wait();
                }

                internal async Task<string> Ask(string text) =>
                    await AvoidBadResponse(() => rc.Execute(new RestRequest("ask", Method.POST).AddParameter("text", text)),
                        "Responding to chat failed: ",
                        response => GetJsonProperty(response, "response"));

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

            static bool HasNeko(ref string msg, IUser user)
            {
                string neko = user.Username;
                SocketGuildUser guildneko = user is SocketGuildUser ? user as SocketGuildUser : null;
                if (HasNekoEmojiOrNot(ref msg, neko)) // Have we been mentioned by our actual name?
                {
                    if (guildneko != null)
                        HasNekoNick(ref msg, guildneko.Nickname); // Strip nick, too, just in case.
                    return true;
                }
                return guildneko != null && HasNekoNick(ref msg, guildneko.Nickname); // Have we been mentioned by our nick?
            }
            #endregion

            async Task Do(IMessage e)
            {
                if (chatbots.Count() == 0) return; // No bot sessions
                string msg = Helpers.ResolveTags(e);
                IUser self = Program.Self;
                if (e.Channel is SocketGuildChannel)
                    self = (e.Channel as SocketGuildChannel).Guild.CurrentUser;
                if (chatbots.ContainsKey(e.Channel.Id) && (e.Channel is IPrivateChannel || HasNeko(ref msg, self)))
                {
                    /* Ideally, we'd ask in order, but to retry, we now await, so we can't.
                    string chat;
                    lock (chatbots[e.Channel.Id]) chat = chatbots[e.Channel.Id].Ask(msg); // Ask in order.
                    chat = WebUtility.HtmlDecode(chat);*/
                    var chat = WebUtility.HtmlDecode(await chatbots[e.Channel.Id].Ask(msg));
                    var disposable = e.Channel.EnterTypingState();
                    for (int i = 10; i != 0; --i) try { await e.Channel.SendMessageAsync(chat, e.IsTTS); break; }
                        catch (Discord.Net.HttpException ex) { if (i == 1) await Log.Write(LogSeverity.Error, $"{ex.Message}\nCould not SendMessage to {(e.Channel is IPrivateChannel ? "private" : "public")} channel {e.Channel} in response to {e.Author}'s message: {e.Content}"); disposable.Dispose(); }
                    disposable.Dispose(); // Note: We probably don't need to call this, but I'm overtired, so I'm being cautious.
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
                Program.Cmds.NonCommands += e => Task.Run(() => Do(e));
            }
            internal bool HasBot(ulong id) => chatbots.ContainsKey(id);
            internal void RemoveBot(ulong id) => Helpers.Remove(chatbots, id);
            internal void CreateBot(string client_id, string chat_id) => chatbots[Convert.ToUInt64(chat_id)] = new Session(client_id + chat_id, _user, _key);
        }

        internal static void AddDelayedCommands(Commands.CommandGroupBuilder group)
        {
            var creds = Program.config["CleverBot"];
            if (!Helpers.FieldExists(creds, "user")) // no credentials
              return;

            // Create the handler
            var handler = new Handler(Program.Self.Id, creds["user"].ToString(), creds["key"].ToString());

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
                                e.Channel.SendMessageAsync("The bot is already " + (botstatus ? "on" : "off") + $" for {e.Channel}");
                            else
                            {
                                if (botstatus)
                                    handler.RemoveBot(e.Channel.Id);
                                else
                                    handler.CreateBot(group.Service.Client.CurrentUser.Id.ToString(), e.Channel.Id.ToString());
                                e.Channel.SendMessageAsync("The bot is now " + (!botstatus ? "on" : "off") + $" for {e.Channel}");
                                SQL.AddOrUpdateFlag(e.Channel.Id, "chatbot", botstatus ? "-1" : "0");
                            }
                        });
                    }
                    else e.Channel.SendMessageAsync("The bot is currently " + (botstatus ? "on" : "off") + $" for {e.Channel}.");
                });
        }
    }
}
