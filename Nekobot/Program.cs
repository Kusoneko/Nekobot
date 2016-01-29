using Discord;
using Nekobot.Commands;
using Nekobot.Commands.Permissions.Levels;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LastFM = IF.Lastfm.Core.Api;

namespace Nekobot
{
    partial class Program
    {
        // Commands first to help with adding new commands
        static void GenerateCommands(CommandGroupBuilder group)
        {
            group.DefaultMusicFlag(false);
            group.DefaultNsfwFlag(false);

            // User commands
            group.CreateCommand("servers")
                .Description("I'll send you statistics about the servers, channels and users (can be spammy, goes to private).")
                .MinPermissions(4)
                .Do(async e =>
                {
                    var output = "";
                    foreach (var server in client.Servers)
                    {
                        output += $"{server.Name}: {server.TextChannels.Count()} text & {server.VoiceChannels.Count()} voice channels, {server.Users.Count()} users. ID: {server.Id}";
                        if (output.Length >= 2000)
                        {
                            var index = output.Length == 2000 ? 0 : output.LastIndexOf('\n');
                            await e.User.SendMessage(Format.Code(index == 0 ? output : output.Substring(0, index)));
                            output = index == 0 ? "" : output.Substring(index+1);
                        }
                        else output += '\n';
                    }
                    if (output.Any()) await e.User.SendMessage(Format.Code(output));
                });

            group.CreateCommand("status")
                .Description("I'll tell you some useful stats about myself.")
                .Do(async e => await e.Channel.SendMessage($"I'm connected to {client.Servers.Count()} servers, which have a total of {client.Servers.SelectMany(x => x.TextChannels).Count()} text and {client.Servers.SelectMany(x => x.VoiceChannels).Count()} voice channels, and see a total of {client.Servers.SelectMany(x => x.Users).Distinct().Count()} different users.\n{Format.Code($"Uptime: {Helpers.Uptime()}\n{Console.Title}")}"));

            group.CreateCommand("version")
                .Description("I'll tell you the current version and check if a newer version is available.")
                .Do(async e =>
                {
                    var version = Config.AppVersion;
                    string[] versions = version.Split('.');
                    var rclient = Helpers.GetRestClient("https://raw.githubusercontent.com");
                    var request = new RestRequest("Kusoneko/Nekobot/master/version.json", Method.GET);
                    JObject result = JObject.Parse(rclient.Execute<JObject>(request).Content);
                    string remoteversion = result["version"].ToString();
                    string[] remoteversions = remoteversion.Split('.');
                    if (int.Parse(versions[0]) < int.Parse(remoteversions[0]))
                        await e.Channel.SendMessage($"I'm currently {(int.Parse(remoteversions[0]) - int.Parse(versions[0]))} major version(s) behind. (Current version: {version}, latest version: {remoteversion})");
                    else if (int.Parse(versions[0]) > int.Parse(remoteversions[0]))
                        await e.Channel.SendMessage($"I'm currently {(int.Parse(versions[0]) - int.Parse(remoteversions[0]))} major version(s) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    else if (int.Parse(versions[1]) < int.Parse(remoteversions[1]))
                        await e.Channel.SendMessage($"I'm currently {(int.Parse(remoteversions[1]) - int.Parse(versions[1]))} minor version(s) behind. (Current version: {version}, latest version: {remoteversion})");
                    else if (int.Parse(versions[1]) > int.Parse(remoteversions[1]))
                        await e.Channel.SendMessage($"I'm currently {(int.Parse(versions[1]) - int.Parse(remoteversions[1]))} minor version(s) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    else if (int.Parse(versions[2]) < int.Parse(remoteversions[2]))
                        await e.Channel.SendMessage($"I'm currently {(int.Parse(remoteversions[2]) - int.Parse(versions[2]))} patch(es) behind. (Current version: {version}, latest version: {remoteversion})");
                    else if (int.Parse(versions[2]) > int.Parse(remoteversions[2]))
                        await e.Channel.SendMessage($"I'm currently {(int.Parse(versions[2]) - int.Parse(remoteversions[2]))} patch(es) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    else
                        await e.Channel.SendMessage($"I'm up to date! (Current version: {version})");
                });

            Common.AddCommands(group);

            group.CreateCommand("playeravatar")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll get you the avatar of each Player.me username provided.")
                .Do(async e =>
                {
                    var rclient = Helpers.GetRestClient("https://player.me/api/v1/auth");
                    var request = new RestRequest("pre-login", Method.POST);
                    foreach (string s in e.Args)
                    {
                        request.AddQueryParameter("login", s);
                        JObject result = JObject.Parse(rclient.Execute(request).Content);
                        await e.Channel.SendMessage(s + (result["success"].ToObject<bool>() == false
                            ? " was not found." : $"'s avatar: https:{result["results"]["avatar"]["original"]}"));
                    }
                });

            if (config["LastFM"].HasValues)
            {
                lfclient = new LastFM.LastfmClient(config["LastFM"]["apikey"].ToString(), config["LastFM"]["apisecret"].ToString());
                group.CreateCommand("lastfm")
                    .Parameter("username(s)", Commands.ParameterType.Unparsed)
                    .Description("I'll tell you the last thing you, a lastfm user, or users on this server (if I know their lastfm) listened to.")
                    .Do(async e =>
                    {
                        var api = new LastFM.UserApi(lfclient.Auth, lfclient.HttpClient);
                        var users = e.Args[0].Any() ? e.Message.MentionedUsers.Any() ? e.Message.MentionedUsers : null : new[]{e.User};
                        var response = "";
                        if (users == null)
                            response = await GetLastScrobble(api, Tuple.Create(e.Args[0], e.Args[0], false));
                        else foreach (var user in (from u in users select Tuple.Create(SQL.ReadUser(u.Id, "lastfm"), u.Name, u == e.User)))
                            response += (user.Item1 != null ? await GetLastScrobble(api, user)
                                    : $"I don't know {(user.Item3 ? "your" : $"{user.Item2}'s")} lastfm yet{(user.Item3 ? ", please use the `setlastfm <username>` command" : "")}"
                                ) + ".\n";
                        await e.Channel.SendMessage(response);
                    });

                group.CreateCommand("setlastfm")
                    .Parameter("username", Commands.ParameterType.Unparsed)
                    .Description("I'll remember your lastfm username.")
                    .Do(async e =>
                    {
                        var lastfm = e.Args[0];
                        if (lastfm.Any() && lastfm.Length < 16)
                        {
                            lastfm = $"'{lastfm}'";
                            await SQL.AddOrUpdateUserAsync(e.User.Id, "lastfm", lastfm);
                            await e.Channel.SendMessage($"I'll remember your lastfm is {lastfm} now, {e.User.Name}.");
                        }
                        else await e.Channel.SendMessage($"'{lastfm}' is not a valid lastfm username.");
                    });
            }

            group.CreateCommand("hbavatar")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you the hummingbird avatar of the usernames provided.")
                .Do(e =>
                {
                    var rclient = Helpers.GetRestClient("http://hummingbird.me/api/v1/users");
                    string message = "";
                    foreach (string s in e.Args)
                    {
                        var request = new RestRequest($"{s}", Method.GET);
                        var content = rclient.Execute(request).Content;
                        if (content[0] == '<')
                        {
                            message += $@"
{s} doesn't exist.";
                        }
                        else
                        {
                            JObject result = JObject.Parse(content);
                            string username = result["name"].ToString();
                            string avatar = result["avatar"].ToString();
                            message += $@"
{username}'s avatar: {avatar}";
                        }
                    }
                    e.Channel.SendMessage(message);
                });

            group.CreateCommand("hb")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you information on the hummingbird accounts of the usernames provided.")
                .Do(e =>
                {
                    var rclient = Helpers.GetRestClient("http://hummingbird.me/api/v1/users");
                    foreach (string s in e.Args)
                    {
                        string message = "";
                        var request = new RestRequest($"{s}", Method.GET);
                        var content = rclient.Execute(request).Content;
                        if (content[0] == '<')
                        {
                            message += $@"{s} doesn't exist.";
                        }
                        else
                        {
                            JObject result = JObject.Parse(content);
                            var username = result["name"].ToString();
                            var avatar = result["avatar"].ToString();
                            var userurl = $"http://hummingbird.me/users/{username}";
                            var waifu = result["waifu"].ToString();
                            var waifu_prefix = result["waifu_or_husbando"].ToString();
                            var bio = result["bio"].ToString();
                            var location = result["location"].ToString();
                            var website = result["website"].ToString();
                            var life_spent_on_anime = int.Parse(result["life_spent_on_anime"].ToString());

                            string lifeAnime = CalculateTime(life_spent_on_anime);

                            message += $@"
**User**: {username}
**Avatar**: {avatar} 
**{waifu_prefix}**: {waifu}
**Bio**: {bio}
**Time wasted on Anime**: {lifeAnime}";
                            if (!string.IsNullOrWhiteSpace(location))
                                message += $@"
**Location**: {location}";
                            if (!string.IsNullOrWhiteSpace(website))
                                message += $@"
**Website**: {website}";
                            message += $@"
**Hummingbird page**: {userurl}";
                        }
                        e.Channel.SendMessage(message);
                    }
                });

            group.CreateCommand("player")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you information on the Player.me of each usernames provided.")
                .Do(e =>
                {
                    var rclient = Helpers.GetRestClient("https://player.me/api/v1/auth");
                    var request = new RestRequest("pre-login", Method.POST);
                    foreach (string s in e.Args)
                    {
                        request.AddQueryParameter("login", s);
                        JObject result = JObject.Parse(rclient.Execute(request).Content);
                        if (Convert.ToBoolean(result["success"]) == false)
                            e.Channel.SendMessage($"{s} was not found.");
                        else
                        {
                            string username = result["results"]["username"].ToString();
                            string avatar = "https:" + result["results"]["avatar"]["original"].ToString();
                            string bio = result["results"]["short_description"].ToString();
                            DateTime date = DateTime.Parse(result["results"]["created_at"].ToString());
                            string joined = date.ToString("yyyy-MM-dd");
                            int followers = Convert.ToInt32(result["results"]["followers_count"]);
                            int following = Convert.ToInt32(result["results"]["following_count"]);
                            e.Channel.SendMessage($@"
**User**: {username}
**Avatar**: {avatar}
**Bio**: {bio}
**Joined on**: {joined}
**Followers**: {followers}
**Following**: {following}");
                        }
                    }
                });

            // Moderator commands
            group.CreateCommand("invite")
                .Parameter("invite code or link", Commands.ParameterType.Required)
                .MinPermissions(1)
                .Description("I'll join a new server using the provided invite code or link.")
                .Do(e => client.GetInvite(e.Args[0])?.Result.Accept());

            // Administrator commands
            group.CreateCommand("setpermissions")
                .Alias("setperms")
                .Alias("setauth")
                .Parameter("newPermissionLevel", Commands.ParameterType.Required)
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .MinPermissions(2)
                .Description("I'll set the permission level of the mentioned people to the level mentioned (cannot be higher than or equal to yours).")
                .Do(async e =>
                {
                    int newPermLevel;
                    int eUserPerm = Helpers.GetPermissions(e.User, e.Channel);
                    if (e.Args[1] == "" || !e.Message.MentionedUsers.Any())
                        await e.Channel.SendMessage("You need to at least specify a permission level and mention one user.");
                    else if (!int.TryParse(e.Args[0], out newPermLevel))
                        await e.Channel.SendMessage("The first argument needs to be the new permission level.");
                    else if (eUserPerm <= newPermLevel)
                        await e.Channel.SendMessage("You can only set permission level to lower than your own.");
                    else
                    {
                        string reply = "";
                        foreach (User u in e.Message.MentionedUsers)
                        {
                            int oldPerm = Helpers.GetPermissions(u, e.Channel);
                            if (oldPerm >= eUserPerm)
                            {
                                reply += $"{u.Mention}'s permission level is no less than yours, you are not allowed to change it.";
                                continue;
                            }
                            bool change_needed = oldPerm != newPermLevel;
                            if (change_needed)
                                await SQL.AddOrUpdateUserAsync(u.Id, "perms", newPermLevel.ToString());
                            if (reply != "")
                                reply += '\n';
                            reply += $"{u.Mention}'s permission level is "+(change_needed ? "now" : "already at")+$" {newPermLevel}.";
                        }
                        await e.Channel.SendMessage(reply);
                    }
                });

            // Owner commands

            group.CreateCommand("leave")
                .MinPermissions(3)
                .Description("I'll leave the server this command was used in.")
                .Do(async e =>
                {
                    await e.Channel.SendMessage("Bye bye!");
                    await Music.Stop(e.Server);
                    await e.Server.Leave();
                });

            group.CreateCommand("color")
                .Parameter("Rolename", Commands.ParameterType.Required)
                .Parameter("hex", Commands.ParameterType.Optional)
                .Parameter("r", Commands.ParameterType.Optional)
                .Parameter("g", Commands.ParameterType.Optional)
                .Parameter("b", Commands.ParameterType.Optional)
                .MinPermissions(3)
                .Description("I'll set a role's color to the hex(000000-FFFFFF) or rgb(0-255 0-255 0-255) color value provided.")
                .Do(async e =>
                {
                    if (e.Args.Count() == 2 || e.Args.Count() == 4)
                    {
                        bool rgb = e.Args.Count() == 4; // assume hex code was provided when 2, rgb when 4
                        byte red = Convert.ToByte(rgb ? int.Parse(e.Args[1]) : Convert.ToInt32(e.Args[1].Substring(0, 2), 16));
                        byte green = Convert.ToByte(rgb ? int.Parse(e.Args[2]) : Convert.ToInt32(e.Args[1].Substring(2, 2), 16));
                        byte blue = Convert.ToByte(rgb ? int.Parse(e.Args[3]) : Convert.ToInt32(e.Args[1].Substring(4, 2), 16));
                        Role role = e.Server.FindRoles(e.Args[0]).FirstOrDefault(); // TODO: In the future, we will probably use MentionedRoles for this.
                        await role.Edit(color: new Color(red, green, blue));
                        await e.Channel.SendMessage($"Role {role.Name}'s color has been changed.");
                    }
                    else
                        await e.Channel.SendMessage("The parameters are invalid.");
                });

            // Higher level admin commands
            group.CreateCommand("setgame")
                .Parameter("Game", Commands.ParameterType.Unparsed)
                .Description("I'll set my current game to something else (empty for no game).")
                .MinPermissions(4)
                .Do(e => client.SetGame(e.Args[0])); // TODO: Store current game in database(varchar(128)) instead of config?

            group.CreateCommand("deletelast")
                .MinPermissions(4)
                .Parameter("few", Commands.ParameterType.Required)
                .Description("I'll delete the last `few` messages, and the command message.")
                .Do(async e =>
                {
                    var few = int.Parse(e.Args[0]);
                    if (few <= 0)
                    {
                        await e.Channel.SendMessage("You're silly!");
                        return;
                    }

                    if (e.Channel.IsPrivate || !e.Server.CurrentUser.GetPermissions(e.Channel).ManageMessages)
                    {
                        await e.Channel.SendMessage("I can't even do that here.");
                        return;
                    }

                    await Helpers.DoToMessages(e.Channel, few, (msgs, has_cmd_msg) =>
                    {
                        foreach (var msg in msgs)
                        {
                            Task t = msg.Delete();
                            if ((!has_cmd_msg || e.Message.Id != msg.Id) && --few == 0) break;
                        }
                        return msgs.Count() - (has_cmd_msg ? 1 : 0);
                    });
                });

            group.CreateCommand("setname")
                .Parameter("New name", Commands.ParameterType.Unparsed)
                .Description("I'll change my name to whatever you wish.")
                .MinPermissions(4)
                .Do(e => client.CurrentUser.Edit(config["password"].ToString(), e.Args[0]));

            Flags.AddCommands(group);
            Chatbot.AddCommands(group);
        }

        // Variables
        static DiscordClient client;
        static LastFM.LastfmClient lfclient;
        internal static JObject config;
        internal static ulong masterId;

        internal static DiscordConfig Config => client.Config;

        static void InputThread()
        {
            for (;;)
            {
                /*string input = */Console.ReadLine();
            }
        }

        static void Main(string[] args)
        {
            // Load up the DB, or create it if it doesn't exist
            SQL.LoadDB();
            // Load up the config file
            LoadConfig();

            // Set up the events and enforce use of the command prefix
            client.Connected += Connected;
            client.Disconnected += Disconnected;
            client.UserJoined += UserJoined;
            client.Log.Message += (s, e) => Log.Write(e);
            client.UsingPermissionLevels(Helpers.GetPermissions);
            //Display errors that occur when a user tries to run a command
            var commands = client.Commands();
            commands.CommandErrored += CommandErrored;

            //Log to the console whenever someone uses a command
            commands.CommandExecuted += (s, e) => client.Log.Info("Command", $"{e.User.Name}: {e.Command.Text}");

            commands.CreateGroup("", group => GenerateCommands(group));
            commands.NonCommands = e => Task.Run(()=>Chatbot.Do(e));
            // Load the chatbots
            Chatbot.Load();

            // Keep the window open in case of crashes elsewhere... (hopefully)
            new Thread(InputThread).Start();

            //DiscordClient will automatically reconnect once we've established a connection, until then we loop on our end
            client.ExecuteAndWait(async() =>
            {
                while (true)
                {
                    try
                    {
                        await client.Connect(config["email"].ToString(), config["password"].ToString());
                        break;
                    }
                    catch (Exception ex)
                    {
                        client.Log.Error($"Login Failed", ex);
                        await Task.Delay(client.Config.FailedReconnectDelay);
                    }
                }
                // Connection, join server if there is one in config, and start music streams
                if (config["server"].ToString() != "")
                    (await client.GetInvite(config["server"].ToString()))?.Accept();
                Voice.Startup(client);
            });
        }

        protected static string CalculateTime(int minutes)
        {
            if (minutes == 0)
                return "No time.";

            int years, months, days, hours = 0;

            hours = minutes / 60;
            minutes %= 60;
            days = hours / 24;
            hours %= 24;
            months = days / 30;
            days %= 30;
            years = months / 12;
            months %= 12;

            string animeWatched = "";

            if (years > 0)
            {
                animeWatched += years;
                if (years == 1)
                    animeWatched += " **year**";
                else
                    animeWatched += " **years**";
            }

            if (months > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += ", ";
                animeWatched += months;
                if (months == 1)
                    animeWatched += " **month**";
                else
                    animeWatched += " **months**";
            }

            if (days > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += ", ";
                animeWatched += days;
                if (days == 1)
                    animeWatched += " **day**";
                else
                    animeWatched += " **days**";
            }

            if (hours > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += ", ";
                animeWatched += hours;
                if (hours == 1)
                    animeWatched += " **hour**";
                else
                    animeWatched += " **hours**";
            }

            if (minutes > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += " and ";
                animeWatched += minutes;
                if (minutes == 1)
                    animeWatched += " **minute**";
                else
                    animeWatched += " **minutes**";
            }

            return animeWatched;
        }

        static async Task<string> GetLastScrobble(LastFM.UserApi api, Tuple<string, string, bool> user)
        {
            var d = (await api.GetRecentScrobbles(user.Item1, count: 1)).FirstOrDefault();
            return $"{user.Item2} {(d != null ? "last" : "hasn't")} listened to {(d != null ? $"**{d.Name}** by **{d.ArtistName}**" : "anything")}";
        }

        private static void LoadConfig()
        {
            if (System.IO.File.Exists("config.json"))
                config = JObject.Parse(System.IO.File.ReadAllText(@"config.json"));
            else
            {
                Log.Output("config.json file not found! Unable to initialize Nekobot!", ConsoleColor.Red);
                SQL.CloseAndDispose();
                Console.ReadKey();
                Environment.Exit(0);
            }
            masterId = config["master"].ToObject<ulong>();
            string musicFolder = config["musicFolder"].ToString();
            Music.Folder = musicFolder;
            Music.UseSubdirs = config["musicUseSubfolders"].ToObject<bool>();

            client = new DiscordClient(new DiscordConfig
            {
                AppName = "Nekobot",
                AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                AppUrl = "https://github.com/Kusoneko/Nekobot",
                LogLevel = config["loglevel"].ToObject<LogSeverity>(),
                UseMessageQueue = false,
            });

            string helpmode = config["helpmode"].ToString();
            client.UsingCommands(new CommandServiceConfig
            {
                CommandChars = config["prefix"].ToString().ToCharArray(),
                RequireCommandCharInPrivate = config["prefixprivate"].ToObject<bool>(),
                RequireCommandCharInPublic = config["prefixpublic"].ToObject<bool>(),
                MentionCommandChar = config["mentioncommand"].ToObject<short>(),
                HelpMode = helpmode.Equals("public") ? HelpMode.Public : helpmode.Equals("private") ? HelpMode.Private : HelpMode.Disable
            }, Flags.GetNsfw, Flags.GetMusic, Flags.GetIgnored);

            Console.Title = $"{Config.AppName} v{Config.AppVersion} (Discord.Net v{DiscordConfig.LibVersion})";
        }

        private static void UserJoined(object sender, UserEventArgs e)
        {
            if (!Flags.GetIgnored(e.User))
                e.Server.DefaultChannel.SendMessage($"Welcome to {e.Server.Name}, {e.User.Mention}! :hearts:");
        }

        private static void Disconnected(object sender, DisconnectedEventArgs e)
        {
            Log.Write(LogSeverity.Warning, "Disconnected");
        }

        private static void Connected(object sender, EventArgs e)
        {
            Log.Write(LogSeverity.Warning, "Connected.");
            client.SetGame(config["game"].ToString());
        }

        private static void CommandErrored(object sender, CommandErrorEventArgs e)
        {
            string msg = e.Exception?.GetBaseException().Message;
            if (msg == null) //No exception - show a generic message
            {
                switch (e.ErrorType)
                {
                    case CommandErrorType.Exception:
                        msg = "Unknown Error";
                        break;
                    case CommandErrorType.BadPermissions:
                        msg = "You do not have permission to run this command.";
                        break;
                    case CommandErrorType.BadArgCount:
                        msg = "You provided the incorrect number of arguments for this command.";
                        break;
                    case CommandErrorType.InvalidInput:
                        msg = "Unable to parse your command, please check your input.";
                        break;
                    case CommandErrorType.UnknownCommand:
                        /* This command just wasn't for Neko, don't interrupt!
                        msg = "Unknown command.";
                        break;
                        */
                        return;
                }
            }
            if (msg != null)
            {
                client.ReplyError(e, "Command Error: " + msg);
                client.Log.Error("Command", msg);
            }
        }
    }
}
