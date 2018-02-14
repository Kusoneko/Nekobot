using Discord;
using Nekobot.Commands;
using Nekobot.Commands.Permissions.Levels;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LastFM = IF.Lastfm.Core.Api;
using Discord.WebSocket;

namespace Nekobot
{
    partial class Program
    {
        public static readonly string AppName = "Nekobot";
        public static readonly string AppVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

        static string VersionCheck()
        {
            var version = AppVersion;
            var versions = version.Split('.');
            var remoteversion = JObject.Parse(Helpers.GetRestClient("https://raw.githubusercontent.com").Execute<JObject>(new RestRequest("Kusoneko/Nekobot/master/version.json", Method.GET)).Content)["version"].ToString();
            var remoteversions = remoteversion.Split('.');
            int diff;
            string section =
                (diff = int.Parse(versions[0]) - int.Parse(remoteversions[0])) != 0 ? $"major version{(Math.Abs(diff) == 1 ? "" : "s")}" :
                (diff = int.Parse(versions[1]) - int.Parse(remoteversions[1])) != 0 ? $"minor version{(Math.Abs(diff) == 1 ? "" : "s")}" :
                (diff = int.Parse(versions[2]) - int.Parse(remoteversions[2])) != 0 ? $"patch{(Math.Abs(diff) == 1 ? "" : "es")}" : null;
            return $"I'm {(section == null ? $"up to date! (Current version: {version})" : $"currently {Math.Abs(diff)} {section} {(diff > 0 ? "ahead" : "behind")}. (Current version: {version}, latest {("released ")}version: {remoteversion})")}";
        }

        // Commands first to help with adding new commands
        static void GenerateCommands(CommandGroupBuilder group)
        {
            // User commands
            group.CreateCommand("servers")
                .Description("I'll send you statistics about the servers, channels and users (can be spammy, goes to private).")
                .MinPermissions(4)
                .Do(async e =>
                {
                    var output = "";
                    foreach (var server in client.Guilds)
                    {
                        output += $"{server.Name}: {server.TextChannels.Count()} text & {server.VoiceChannels.Count()} voice channels, {server.Users.Count()} users. ID: {server.Id}";
                        if (output.Length >= 2000)
                        {
                            var index = output.Length == 2000 ? 0 : output.LastIndexOf('\n');
                            await e.User.SendMessageAsync(Format.Code(index == 0 ? output : output.Substring(0, index)));
                            output = index == 0 ? "" : output.Substring(index + 1);
                        }
                        else output += '\n';
                    }
                    if (output.Any()) await e.User.SendMessageAsync(Format.Code(output));
                });

            group.CreateCommand("status")
                .Description("I'll tell you some useful stats about myself.")
                .Do(async e => await e.Channel.SendMessageAsync($"I'm connected to {client.Guilds.Count()} servers, which have a total of {client.Guilds.SelectMany(x => x.TextChannels).Count()} text and {client.Guilds.SelectMany(x => x.VoiceChannels).Count()} voice channels, and see a total of {client.Guilds.SelectMany(x => x.Users).Distinct().Count()} different users.\n{Format.Code($"Uptime: {Helpers.Uptime()}\n{Console.Title}")}"));

            group.CreateCommand("version")
                .Description("I'll tell you the current version and check if a newer version is available.")
                .Do(async e => await e.Channel.SendMessageAsync(VersionCheck()));

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
                        await e.Channel.SendMessageAsync(s + (result["success"].ToObject<bool>() == false
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
                        var users = e.Args[0].Any() ? e.Message.MentionedUserIds.Any() ? e.Message.Tags.Where(x => x.Type == TagType.UserMention).Select(x => x.Value as IUser) : null : new[]{e.User};
                        var response = "";
                        if (users == null)
                            response = await GetLastScrobble(api, Tuple.Create(e.Args[0], e.Args[0], false));
                        else foreach (var user in (from u in users select Tuple.Create(SQL.ReadUser(u.Id, "lastfm"), u.Username, u == e.User)))
                            response += (user.Item1 != null ? await GetLastScrobble(api, user)
                                    : $"I don't know {(user.Item3 ? "your" : $"{user.Item2}'s")} lastfm yet{(user.Item3 ? ", please use the `setlastfm <username>` command" : "")}"
                                ) + ".\n";
                        await e.Channel.SendMessageAsync(response);
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
                            await e.Channel.SendMessageAsync($"I'll remember your lastfm is {lastfm} now, {e.User.Username}.");
                        }
                        else await e.Channel.SendMessageAsync($"'{lastfm}' is not a valid lastfm username.");
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
                        var content = rclient.Execute(new RestRequest($"{s}", Method.GET)).Content;
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
                    e.Channel.SendMessageAsync(message);
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
                        var content = rclient.Execute(new RestRequest($"{s}", Method.GET)).Content;
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
                        e.Channel.SendMessageAsync(message);
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
                        if (!result["success"].ToObject<bool>())
                            e.Channel.SendMessageAsync($"{s} was not found.");
                        else
                        {
                            var results = result["results"];
                            string username = results["username"].ToString();
                            string avatar = $"https:{results["avatar"]["original"]}";
                            string bio = results["short_description"].ToString();
                            DateTime date = DateTime.Parse(results["created_at"].ToString());
                            string joined = date.ToString("yyyy-MM-dd");
                            int followers = results["followers_count"].ToObject<int>();
                            int following = results["following_count"].ToObject<int>();
                            string admin = results["is_superuser"].ToObject<bool>() ? $"\n**Player.me Staff**" : string.Empty;
                            string profile = $"<https://player.me/{results["slug"]}>";
                            e.Channel.SendMessageAsync($@"
**User**: {username}{admin}
**Avatar**: {avatar}
**Bio**: {bio}
**Joined on**: {joined}
**Followers**: {followers}
**Following**: {following}
**Profile page**: {profile}");
                        }
                    }
                });

            Roles.AddCommands(group);

            // Administrator commands
            group.CreateCommand("restart")
                .Description("Restart me (if I'm misbehaving... I deserve it, sir.)")
                .MinPermissions(2)
                .Do(e =>
                {
                    e.Channel.SendMessageAsync($"Sorry, {Helpers.Nickname(e.User as SocketGuildUser)}, I'll try harder this time!");
                    Helpers.Restart();
                });

            group.CreateCommand("setpermissions")
                .Alias("setperms")
                .Alias("setauth")
                .Parameter("newPermissionLevel", Commands.ParameterType.Required)
                .Parameter("[@User1] [@User2] [...]", Commands.ParameterType.Unparsed)
                .MinPermissions(2)
                .Description("I'll set the permission level of the mentioned people to the level mentioned (cannot be higher than or equal to yours).")
                .Do(async e =>
                {
                    int eUserPerm = Helpers.GetPermissions(e.User, e.Channel);
                    if (e.Args[1].Length == 0 || !e.Message.MentionedUserIds.Any())
                        await e.Channel.SendMessageAsync("You need to at least specify a permission level and mention one user.");
                    else if (!int.TryParse(e.Args[0], out int newPermLevel))
                        await e.Channel.SendMessageAsync("The first argument needs to be the new permission level.");
                    else if (eUserPerm <= newPermLevel)
                        await e.Channel.SendMessageAsync("You can only set permission level to lower than your own.");
                    else
                    {
                        string reply = "";
                        foreach (var u in e.Message.MentionedUserIds)
                        {
                            int oldPerm = Helpers.GetPermissions(u, e.Channel);
                            if (oldPerm >= eUserPerm)
                            {
                                reply += $"<@{u}>'s permission level is no less than yours, you are not allowed to change it.";
                                continue;
                            }
                            bool change_needed = oldPerm != newPermLevel;
                            if (change_needed)
                                await SQL.AddOrUpdateUserAsync(u, "perms", newPermLevel.ToString());
                            if (reply != "")
                                reply += '\n';
                            reply += $"<@{u}>'s permission level is "+(change_needed ? "now" : "already at")+$" {newPermLevel}.";
                        }
                        await e.Channel.SendMessageAsync(reply);
                    }
                });

            // Owner commands
            group.CreateCommand("ban")
                .MinPermissions(8)
                .Parameter("id")
                .Do(e => e.Server.AddBanAsync(ulong.Parse(e.Args[0])));

            group.CreateCommand("leave")
                .MinPermissions(3)
                .Description("I'll leave the server this command was used in.")
                .Do(async e =>
                {
                    await e.Channel.SendMessageAsync("Bye bye!");
                    await Music.Stop(e.Server);
                    await e.Server.LeaveAsync();
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
                    var argcount = e.Args.Count();
                    if (argcount == 2 || argcount == 4)
                    {
                        bool rgb = argcount == 4; // assume hex code was provided when 2, rgb when 4
                        byte red = Convert.ToByte(rgb ? int.Parse(e.Args[1]) : Convert.ToInt32(e.Args[1].Substring(0, 2), 16));
                        byte green = Convert.ToByte(rgb ? int.Parse(e.Args[2]) : Convert.ToInt32(e.Args[1].Substring(2, 2), 16));
                        byte blue = Convert.ToByte(rgb ? int.Parse(e.Args[3]) : Convert.ToInt32(e.Args[1].Substring(4, 2), 16));
                        SocketRole role = (SocketRole)e.Server.Roles.FirstOrDefault(r => r.Name.Contains(e.Args[0])); // TODO: In the future, we will probably use MentionedRoles for this.
                        await role.ModifyAsync(x => x.Color = new Color(red, green, blue));
                        await e.Channel.SendMessageAsync($"Role {role.Name}'s color has been changed.");
                    }
                    else
                        await e.Channel.SendMessageAsync("The parameters are invalid.");
                });

            Action<bool> make_nick_cmd = not_self =>
            {
                group.CreateCommand(not_self ? "nickname" : "robotnick")
                    .MinPermissions(not_self ? 6 : 10)
                    .Parameter("nick", Commands.ParameterType.Unparsed)
                    .Do(async e =>
                    {
                        var nickname = string.Join(" ", e.Args);
                        await (not_self ? e.User as SocketGuildUser : (e.Server as SocketGuild).CurrentUser).ModifyAsync(x => x.Nickname = nickname.Length == 0 ? null : nickname); // TODO: Test if this works, I think it works different in Discord.Net 1.0
                    });
            };
            make_nick_cmd(true);
            make_nick_cmd(false);
            // Higher level admin commands
            group.CreateCommand("setgame")
                .Parameter("Game", Commands.ParameterType.Unparsed)
                .Description("I'll set my current game to something else (empty for no game).")
                .MinPermissions(4)
                .Do(e => client.SetGameAsync(e.Args[0])); // TODO: Store current game in database(varchar(128)) instead of config?

            group.CreateCommand("setavatar")
                .Parameter("Avatar Link")
                .Description("I'll set my current avatar to something else.")
                .MinPermissions(4)
                .Do(e => client.CurrentUser.ModifyAsync(x => x.Avatar = new Discord.Image(new System.IO.MemoryStream(new System.Net.WebClient().DownloadData(e.Args[0])))));

            Action<string, Action<IEnumerable<IMessage>, CommandEventArgs>> delcmd = (s,a) => group.CreateCommand(s)
                .MinPermissions(4)
                .Parameter("few", Commands.ParameterType.Required)
                .Parameter("force", Commands.ParameterType.Optional)
                .Parameter("mentions", Commands.ParameterType.MultipleUnparsed)
                .Description("I'll delete the last `few` messages, and the command message.\nAdd `force` to delete pinned messages.")
                .Do(async e =>
                {
                    var few = int.Parse(e.Args[0]);
                    if (few <= 0)
                    {
                        await e.Channel.SendMessageAsync("You're silly!");
                        return;
                    }
                    bool force = e.Args.Length > 1 && e.Args.Any(arg => arg.Equals("force", StringComparison.CurrentCultureIgnoreCase));

                    if (e.Channel is IPrivateChannel || !(e.Server as SocketGuild).CurrentUser.GetPermissions(e.TextChannel).ManageMessages)
                    {
                        await e.Channel.SendMessageAsync("I can't even do that here.");
                        return;
                    }
                    var users = e.Message.MentionedUserIds;
                    if (Cmds.Config.MentionCommandChar != 0 && !Cmds.Config.CommandChars.Contains(e.Message.Content[0]) && users.Count(id => id == client.CurrentUser.Id) == 1) // Don't include the mention that triggered us...
                        users.Where(id => id != client.CurrentUser.Id);
                    if (!users.Any()) users = null;
                    else
                    {
                        users = users?.Distinct().ToArray();
                        // Delete the command message too, for this case.
                        await e.Message.DeleteAsync();
                    }

                    await Helpers.DoToMessages(e.Channel as SocketTextChannel, few, (msgs, has_cmd_msg) =>
                    {
                        int bonus = (users == null && has_cmd_msg) ? 1 : 0;
                        if (!force || users != null)
                            msgs = msgs.Where(m => (force || !m.IsPinned) && (users == null || users.Contains(m.Author.Id)));
                        if (few < msgs.Count()) // Cut to size
                            msgs = msgs.Take(few + bonus);
                        int removed = msgs.Count() - bonus;
                        few -= removed;
                        Task.Run(() => a(msgs, e));
                        return removed;
                    });
                });
            delcmd("deletelast", async (msgs,e) => await e.TextChannel.DeleteMessagesAsync(msgs.ToArray()));
            delcmd("deletelastold", (msgs, e) => { foreach (var m in msgs) m.DeleteAsync(); });

            group.CreateCommand("setname")
                .Parameter("New name", Commands.ParameterType.Unparsed)
                .Description("I'll change my name to whatever you wish.")
                .MinPermissions(4)
                .Do(e => client.CurrentUser.ModifyAsync(x => x.Username = e.Args[0]));

            Flags.AddCommands(group);
        }
        // Commands that need to be done after login go in here
        static void GenerateDelayedCommands(CommandGroupBuilder group)
        {
            Chatbot.AddDelayedCommands(group);
        }

        // Variables
        internal static DiscordSocketClient client;
        internal static CommandService Cmds;
        static LastFM.LastfmClient lfclient;
        internal static JObject config;
        internal static ulong masterId;
        internal static LogSeverity LogLevel;

        internal static ISelfUser Self => client.CurrentUser;
        internal static Dictionary<string, Action<string>> ConsoleCommands = new Dictionary<string, Action<string>>
        {
            {"songlist", s => Music.SongList()},
            {"restart", s => Helpers.Restart()},
            {"uptime", s => Log.Output($"Uptime: {Helpers.Uptime()}")},
            {"version", s => Log.Output(Console.Title)},
        };

        static void InputThread()
        {
            ConsoleCommands["commands"] = s => Log.Output(string.Join(", ", ConsoleCommands.Keys));
            for (;;)
            {
                string input = Console.ReadLine();
                if (input.Length != 0)
                {
                    var command = input.ToLower().Split(' ');
                    if (ConsoleCommands.TryGetValue(command[0], out Action<string> action))
                        action(string.Join(" ", command.Skip(1)));
                }
                Thread.Sleep(500);
            }
        }

        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult();
        static async Task MainAsync(string[] args)
        {
            // Load up the DB, or create it if it doesn't exist
            SQL.LoadDB();
            // Load up the config file
            LoadConfig();

            // Set up the events and enforce use of the command prefix
            client.Ready += Ready;
            //client.LoggedOut += LoggedOut;
            client.UserJoined += UserJoined;
            client.UserLeft += UserLeft;
            client.Log += Log.Write;
            //Display errors that occur when a user tries to run a command
            Cmds.PermsService = new PermissionLevelService(Helpers.GetPermissions);
            Cmds.CommandErrored += CommandErrored;

            //Log to the console whenever someone uses a command
            Cmds.CommandExecuted += async (s, e) => await Log.Write(new LogMessage(LogSeverity.Info, "Command", $"{e.User.Username}: {e.Command.Text}"));

            Cmds.CreateGroup("", group => GenerateCommands(group));

            // Keep the window open in case of crashes elsewhere... (hopefully)
            new Thread(InputThread).Start();

            //DiscordClient will automatically reconnect once we've established a connection, until then we loop on our end
            Log.Output("Ohayou, Master-san!", ConsoleColor.Cyan);
            Log.Output(VersionCheck(), ConsoleColor.Cyan);
            await client.LoginAsync(TokenType.Bot, config["token"].ToString()).ConfigureAwait(false);
            client.Connected += async () =>
            {
                // Connection, start music streams
                await Voice.Startup(client);
                // Add delayed commands
                Cmds.CreateGroup("", group => GenerateDelayedCommands(group));
            };
            await client.StartAsync().ConfigureAwait(false);

            // Wait infinitely so your bot actually stays connected.
            await Task.Delay(Timeout.Infinite);
        }

        protected static string CalculateTime(int minutes)
        {
            if (minutes == 0)
                return "No time.";

            int hours = minutes / 60;
            minutes %= 60;
            int days = hours / 24;
            hours %= 24;
            int months = days / 30;
            days %= 30;
            int years = months / 12;
            months %= 12;

            string animeWatched = "";
            Action<int, string, Func<string>> add_type = (num, type, func) => animeWatched += num > 0 ? $"{func()}{num} **{type}**{(num == 1 ? "" : "s")}" : "";
            add_type(years, "year", () => "");

            Func<string, string> empty_if_empty = s => animeWatched.Length == 0 ? "" : s;
            Func<string> add_comma = () => empty_if_empty(", ");
            add_type(months, "month", add_comma);
            add_type(days, "day", add_comma);
            add_type(hours, "hour", add_comma);
            add_type(minutes, "minute", () => empty_if_empty(" and "));

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
            LogLevel = config["loglevel"].ToObject<LogSeverity>();

            var conf = new DiscordSocketConfig
            {
                LogLevel = LogLevel,
                MessageCacheSize = 1024,
                AlwaysDownloadUsers = true,
            };
            client = new DiscordSocketClient(conf);

            string helpmode = config["helpmode"].ToString();
            var color = config["color"].ToObject<List<byte>>();
            Cmds = new CommandService(new CommandServiceConfig
            {
                CommandChars = config["prefix"].ToString().ToCharArray(),
                EmbedColor = new Color(color[0], color[1], color[2]),
                RequireCommandCharInPrivate = config["prefixprivate"].ToObject<bool>(),
                RequireCommandCharInPublic = config["prefixpublic"].ToObject<bool>(),
                MentionCommandChar = config["mentioncommand"].ToObject<short>(),
                HelpMode = helpmode.Equals("public") ? HelpMode.Public : helpmode.Equals("private") ? HelpMode.Private : HelpMode.Disabled
            }, Flags.GetNsfw, Flags.GetMusic, Flags.GetIgnored);
            Cmds.Install(client);

            Console.Title = $"{AppName}/{AppVersion} (https://github.com/Kusoneko/Nekobot) {DiscordConfig.UserAgent}";
        }

        private static async Task UserLeft(SocketGuildUser u)
        {
            if (Flags.GetLeft(u.Guild))
            {
                var c_str = Flags.GetLeftChan(u.Guild);
                var c = c_str.Length == 0 ? u.Guild.DefaultChannel : u.Guild.GetChannel(ulong.Parse(c_str));
                await (c as IMessageChannel).SendMessageAsync($"{u.Username}#{u.Discriminator} has left. ({(string.IsNullOrEmpty(u.Nickname) ? "" : $"nick: {u.Nickname}, ")}id: {u.Id})");
            }
        }

        private static async Task UserJoined(SocketGuildUser u)
        {
            if (Flags.GetWelcome(u.Guild) && !Flags.GetIgnored(u))
            {
                var c_str = Flags.GetWelcomeChan(u.Guild);
                var c = c_str.Length == 0 ? u.Guild.DefaultChannel : u.Guild.GetChannel(ulong.Parse(c_str));
                await (c as IMessageChannel).SendMessageAsync($"Welcome to {u.Guild.Name}, {u.Mention}! :hearts:");
            }
            var default_roles = Flags.GetDefaultRoles(u.Guild).Select(r => u.Guild.GetRole(r)).ToArray();
            if (default_roles.Length > 0)
                await u.AddRolesAsync(default_roles);
        }

        /*private static void LoggedOut(object sender, DisconnectedEventArgs e)
        {
            Log.Write(LogSeverity.Warning, "Logged Out.");
        }*/

        private static async Task Ready()
        {
            await Log.Write(LogSeverity.Warning, "Logged in and ready!");
            await client.SetGameAsync(config["game"].ToString());
        }

        private static async Task CommandErrored(CommandErrorEventArgs e)
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
                await client.ReplyError(e, "Command Error: " + msg);
                await Log.Write(new LogMessage(LogSeverity.Error, "Command", msg));
            }
        }
    }
}
