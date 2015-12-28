using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Nekobot.Commands;
using Nekobot.Commands.Permissions.Levels;
using Newtonsoft.Json.Linq;
using RestSharp;
using System.Threading;
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
            group.CreateCommand("ping")
                .Description("I'll reply with 'Pong!'")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, $"<@{e.User.Id}>, Pong!");
                });

            group.CreateCommand("status")
                .Description("I'll give statistics about the servers, channels and users.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, $"I'm connected to {client.AllServers.Count()} servers, which have a total of {client.AllServers.SelectMany(x => x.Channels).Count()} channels, and see a total of {client.AllServers.SelectMany(x => x.Members).Distinct().Count()} different users.");
                });

            group.CreateCommand("whois")
                .Alias("getinfo")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("I'll give you information about the mentioned user(s).")
                .Do(async e =>
                {
                    if (e.Args[0] == "") return;
                    string reply = "";
                    foreach (User u in e.Message.MentionedUsers)
                    {
                        if (u.Id == 63296013791666176 && e.User.Id == 63299786798796800)
                        {
                            reply += $@"
<@{u.Id}> is your onii-chan <3 and his id is {u.Id} and his permission level is {GetPermissions(u, e.Channel)}.
";
                        }
                        else
                        {
                            reply += $@"
<@{u.Id}>'s id is {u.Id} and their permission level is {GetPermissions(u, e.Channel)}.
";
                        }
                    }
                    await client.SendMessage(e.Channel, reply);
                });

            Music.AddCommands(group);

            group.CreateCommand("quote")
                .Description("I'll give you a random quote from https://inspiration.julxzs.website/quotes")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("https://inspiration.julxzs.website");
                    var request = new RestRequest("api/quote", Method.GET);
                    JObject result = JObject.Parse(rclient.Execute<JObject>(request).Content);
                    string quote = result["quote"]["quote"].ToString();
                    string author = result["quote"]["author"].ToString();
                    string date = result["quote"]["date"].ToString();
                    await client.SendMessage(e.Channel, $"\"{quote}\" - {author} {date}");
                });

            group.CreateCommand("version")
                .Description("I'll tell you the current version and check if a newer version is available.")
                .Do(async e =>
                {
                    string[] versions = version.Split('.');
                    rclient.BaseUrl = new Uri("https://raw.githubusercontent.com");
                    var request = new RestRequest("Kusoneko/Nekobot/master/version.json", Method.GET);
                    JObject result = JObject.Parse(rclient.Execute<JObject>(request).Content);
                    string remoteversion = result["version"].ToString();
                    string[] remoteversions = remoteversion.Split('.');
                    if (int.Parse(versions[0]) < int.Parse(remoteversions[0]))
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(remoteversions[0]) - int.Parse(versions[0]))} major version(s) behind. (Current version: {version}, latest version: {remoteversion})");
                    else if (int.Parse(versions[0]) > int.Parse(remoteversions[0]))
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(versions[0]) - int.Parse(remoteversions[0]))} major version(s) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    else if (int.Parse(versions[1]) < int.Parse(remoteversions[1]))
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(remoteversions[1]) - int.Parse(versions[1]))} minor version(s) behind. (Current version: {version}, latest version: {remoteversion})");
                    else if (int.Parse(versions[1]) > int.Parse(remoteversions[1]))
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(versions[1]) - int.Parse(remoteversions[1]))} minor version(s) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    else if (int.Parse(versions[2]) < int.Parse(remoteversions[2]))
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(remoteversions[2]) - int.Parse(versions[2]))} patch(es) behind. (Current version: {version}, latest version: {remoteversion})");
                    else if (int.Parse(versions[2]) > int.Parse(remoteversions[2]))
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(versions[2]) - int.Parse(remoteversions[2]))} patch(es) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    else
                        await client.SendMessage(e.Channel, $"I'm up to date! (Current version: {version})");
                });

            Image.AddCommands(group);

            group.CreateCommand("fortune")
                .Description("I'll give you a fortune!")
                .Do(async e =>
                {
                    string[] fortunes = new string[] { "Don't sleep for too long, or you'll miss naptime!", "Before crying over spilt milk, remember it can still be delicious without a bowl.", "A bird in the paw is worth nom nom nom...", "Let no surface, no matter how high or cluttered, go unexplored.", "Neko never catches the laser if neko never tries.", "Our greatest glory is not in never falling, but in making sure master doesn't find the mess.", "A mouse shared halves the food but doubles the happiness.", "There exists nary a toy as pertinent as the box from whence that toy came.", "Neko will never be fed if neko does not meow all day!", "Ignore physics, and physics will ignore you.", "Never bite the hand that feeds you!", "Before finding the red dot, you must first find yourself.", "Some see the glass half empty. Some see the glass half full. Neko sees the glass and knocks it over.", "Make purrs not war.", "Give a neko fish and you feed them for a day; Teach a neko to fish and... mmmm fish.", "Wheresoever you go, go with all of master's things.", "Live your dreams every day! Why do you think neko naps so much?", "The hardest thing of all is to find a black cat in a dark room, especially if there is no cat.", "Meow meow meow meow, meow meow. Meow meow meow." };
                    Random rnd = new Random();
                    await client.SendMessage(e.Channel, fortunes[rnd.Next(0, fortunes.Count())]);
                });

            group.CreateCommand("playeravatar")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll get you the avatar of each Player.me username provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("https://player.me/api/v1/auth");
                    var request = new RestRequest("pre-login", Method.POST);
                    foreach (string s in e.Args)
                    {
                        request.AddQueryParameter("login", s);
                        JObject result = JObject.Parse(rclient.Execute(request).Content);
                        if (Convert.ToBoolean(result["success"]) == false)
                            await client.SendMessage(e.Channel, $"{s} was not found.");
                        else
                        {
                            string avatar = result["results"]["avatar"]["original"].ToString();
                            await client.SendMessage(e.Channel, $"{s}'s avatar: https:{avatar}");
                        }
                    }
                });

            if (config["LastFM"].HasValues)
            {
                lfclient = new LastFM.LastfmClient(config["LastFM"]["apikey"].ToString(), config["LastFM"]["apisecret"].ToString());
                group.CreateCommand("lastfm")
                    .Parameter("username", Commands.ParameterType.Unparsed)
                    .Description("I'll tell you the last thing a lastfm user listened to.")
                    .Do(async e =>
                    {
                        var api = new LastFM.UserApi(lfclient.Auth, lfclient.HttpClient);
                        var user = e.Args[0];
                        if (user == "") user = SQL.ReadUser(e.User.Id, "lastfm");
                        if (user != null)
                        {
                            var d = (await api.GetRecentScrobbles(user, count: 1)).Single();
                            await client.SendMessage(e.Channel, $"{(e.Args[0] == "" ? e.User.Name : user)} last listened to {d.Name} by {d.ArtistName}");
                        }
                        else await client.SendMessage(e.Channel, $"I don't know your lastfm yet, please use the `setlastfm <username>` command.");
                    });

                group.CreateCommand("setlastfm")
                    .Parameter("username", Commands.ParameterType.Unparsed)
                    .Description("I'll remember your lastfm username.")
                    .Do(async e =>
                    {
                        var lastfm = $"'{e.Args[0]}'";
                        if (lastfm.Length > 2 && lastfm.Length < 18)
                        {
                            await SQL.AddOrUpdateUserAsync(e.User.Id, "lastfm", lastfm);
                            await client.SendMessage(e.Channel, $"I'll remember your lastfm is {lastfm} now, {e.User.Name}.");
                        }
                        else await client.SendMessage(e.Channel, $"{lastfm} is not a valid lastfm username.");
                    });
            }

            group.CreateCommand("nya")
                .Alias("nyaa")
                .Alias("nyan")
                .Description("I'll say 'Nyaa~'")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "Nyaa~");
                });

            group.CreateCommand("poi")
                .Description("I'll say 'Poi!'")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "Poi!");
                });

            group.CreateCommand("aicrai")
                .Alias("aicraievritiem")
                .Alias("aicraievritaim")
                .Alias("sadhorn")
                .Alias("icri")
                .Description("When sad things happen...")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=0JAn8eShOo8");
                });

            group.CreateCommand("notnow")
                .Alias("rinpls")
                .Description("How to Rekt: Rin 101")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=2BZUzJfKFwM");
                });

            group.CreateCommand("uninstall")
                .Description("A great advice in any situation.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=5sQzi0dn_dA");
                });

            group.CreateCommand("killyourself")
                .Alias("kys")
                .Description("Another great advice.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=2dbR2JZmlWo");
                });

            group.CreateCommand("congratulations")
                .Alias("congrats")
                .Alias("grats")
                .Alias("gg")
                .Description("Congratulate someone for whatever reason.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=oyFQVZ2h0V8");
                });

            group.CreateCommand("say")
                .Alias("forward")
                .Alias("echo")
                .Parameter("#channel or @User (or user/channel id in PMs)] [...", Commands.ParameterType.Unparsed)
                .Description("I'll repeat what you said. (To a given user or channel)")
                .Do(async e =>
                {
                    Channel channel = e.Channel;
                    string message = e.Args[0];
                    if (message == "") return; // Unparsed can be empty

                    foreach (User user in e.Message.MentionedUsers)
                        message = message.Replace($"@{user.Name}", $"<@{user.Id}>");
                    foreach (Channel chan in e.Message.MentionedChannels)
                        message = message.Replace($"#{chan.Name}", $"<#{chan.Id}>");

                    bool usermention = e.Message.MentionedUsers.Count() > (e.Message.IsMentioningMe ? 1 : 0) && message.StartsWith("<@");
                    if (usermention || (e.Message.MentionedChannels.Count() > 0 && message.StartsWith("<#")))
                    {
                        int index = message.IndexOf(">");
                        if (index+2 < message.Length)
                        {
                            long mentionid = Convert.ToInt64(message.Substring(2, index-2));
                            if (mentionid != client.CurrentUserId)
                            {
                                channel = usermention ? await client.CreatePMChannel(e.Message.MentionedUsers.Where(u => u.Id == mentionid).Single())
                                    : e.Message.MentionedChannels.Where(c => c.Id == mentionid).Single();
                                if (CanSay(ref channel, e.User, e.Channel))
                                    message = message.Substring(index + 2);
                            }
                        }
                    }
                    else if (channel.IsPrivate)
                    {
                        try
                        {
                            string chanstr = message.Split(' ').First();
                            if (chanstr.Length+1 < message.Length)
                            {
                                long id = Convert.ToInt64(chanstr);
                                channel = client.GetChannel(id) ?? await client.CreatePMChannel(client.AllServers.Select(x => client.GetUser(x, id)).FirstOrDefault());
                                if (CanSay(ref channel, e.User, e.Channel))
                                    message = message.Substring(message.IndexOf(" ")+1);
                            }
                        } catch (Exception) { }
                    }
                    if (message.TrimEnd() != "")
                        await client.SendMessage(channel, message);
                });

            group.CreateCommand("reverse")
                .Alias("backward")
                .Alias("flip")
                .Parameter("text...", Commands.ParameterType.Unparsed)
                .Description("I'll repeat what you said, in reverse!")
                .Do(async e =>
                {
                    var text = e.Args[0];
                    if (text.Length != 0)
                        await client.SendMessage(e.Channel, String.Join("", GraphemeClusters(text).Reverse().ToArray()));
                });

            group.CreateCommand("whereami")
                .Alias("channelinfo")
                .Alias("channel")
                .Alias("location")
                .Alias("where")
                .Description("I'll tell you information about the channel and server you're asking me this from.")
                .Do(async e =>
                {
                    if (e.Channel.IsPrivate)
                        await client.SendMessage(e.Channel, "You're in a private message with me, baka.");
                    else
                    {
                        string message = $@"You are currently in {e.Channel.Name} (id: {e.Channel.Id})
on server **{e.Server.Name}** (id: {e.Server.Id}) (region: {e.Server.Region})
owned by {e.Server.Owner.Name} (id {e.Server.Owner.Id}).";
                        if (e.Channel.Topic != "" || e.Channel.Topic != null)
                            message = message + $@"
The current topic is: {e.Channel.Topic}";
                        await client.SendMessage(e.Channel, message);
                    }
                });

            group.CreateCommand("avatar")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("I'll give you the avatars of every mentioned users.")
                .Do(async e =>
                {
                    if (e.Args[0] == "") return;
                    foreach (User u in e.Message.MentionedUsers)
                    {
                        if (u.AvatarUrl == null)
                            await client.SendMessage(e.Channel, $"<@{u.Id}> has no avatar.");
                        else
                            await client.SendMessage(e.Channel, $"<@{u.Id}>'s avatar is: https://discordapp.com/api/{u.AvatarUrl}");
                    }
                });

            group.CreateCommand("rand")
                .Parameter("min", Commands.ParameterType.Optional)
                .Parameter("max", Commands.ParameterType.Optional)
                .Description("I'll give you a random number between *min* and *max*. Both are optional. If only one is given, it's *max*. (defaults: 1-100)")
                .Do(async e =>
                {
                    foreach (string s in e.Args)
                    {
                        int dummy = 0;
                        if (!int.TryParse(s, out dummy))
                        {
                            await client.SendMessage(e.Channel, $"{s} is not a number!");
                            return;
                        }
                    }
                    int min = e.Args.Length > 1 ? int.Parse(e.Args[0]) : 1;
                    int max = e.Args.Length > 0 ? int.Parse(e.Args[e.Args.Length == 1 ? 0 : 1]) : 100;
                    if (min == max)
                    {
                        await client.SendMessage(e.Channel, $"You're joking right? It's {min}.");
                        return;
                    }
                    if (min > max)
                    {
                        int z = min;
                        min = max;
                        max = z;
                    }
                    ++max;
                    await client.SendMessage(e.Channel, $"Your number is **{new Random().Next(min,max)}**.");
                });

            group.CreateCommand("roll")
                .Parameter("dice", Commands.ParameterType.Optional)
                .Parameter("sides", Commands.ParameterType.Optional)
                .Parameter("times", Commands.ParameterType.Optional)
                .Description("I'll roll a few sided dice for a given number of times. All params are optional. (defaults: 1 *dice*, 6 *sides*, 1 *times*)")
                .Do(async e =>
                {
                    bool rick = false;
                    bool valid = true;
                    foreach (string s in e.Args)
                    {
                        int dummy = 0;
                        if (!int.TryParse(s, out dummy))
                            valid = false;
                        if (s == "rick")
                            rick = true;
                        if (rick || !valid)
                            break;
                    }
                    if (!rick)
                    {
                        if (valid)
                        {
                            int dice = e.Args.Count() >= 1 ? int.Parse(e.Args[0]): 1;
                            int sides = e.Args.Count() >= 2 ? int.Parse(e.Args[1]): 6;
                            int times = e.Args.Count() >= 3 ? int.Parse(e.Args[2]): 1;

                            int roll = 0;
                            Random rnd = new Random();
                            for (int i = times; i > 0; i--)
                                for (int j = dice; j > 0; j--)
                                    roll += rnd.Next(1, sides + 1);
                            await client.SendMessage(e.Channel, $"You rolled {dice} different {sides}-sided dice {times} times... Result: **{roll}**");
                        }
                        else
                            await client.SendMessage(e.Channel, $"Arguments are not all numbers!");
                    }
                    else
                        await client.SendMessage(e.Channel, $"https://www.youtube.com/watch?v=dQw4w9WgXcQ");
                });

            group.CreateCommand("lotto")
                .Description("I'll give you a set of 6 lucky numbers!")
                .Do(async e =>
                {
                    List<int> lotto = new List<int>();
                    Random rnd = new Random();
                    while (lotto.Count() < 6)
                    {
                        int number = rnd.Next(1, 60);
                        for (int i = 0; i < lotto.Count(); i++)
                        {
                            if (lotto[i] == number)
                            {
                                lotto.Remove(number);
                                break;
                            }
                        }
                        lotto.Add(number);
                    }
                    await client.SendMessage(e.Channel, $"Your lucky numbers are **{lotto[0]}, {lotto[1]}, {lotto[2]}, {lotto[3]}, {lotto[4]}, {lotto[5]}**.");
                });

            group.CreateCommand("pet")
                .Alias("pets")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("Everyone loves being pet, right!?! Pets each *@user*. Leave empty (or mention me too) to pet me!")
                .Do(async e =>
                {
                    await PerformAction(e, "pet", "*purrs*", false);
                });

            group.CreateCommand("hug")
                .Alias("hugs")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("Hug someone! Hugs each *@user*. Leave empty to get a hug!")
                .Do(async e =>
                {
                    await PerformAction(e, "hug", "<3", true);
                });

            group.CreateCommand("8ball")
                .Parameter("question", Commands.ParameterType.Optional)
                .Parameter("?", Commands.ParameterType.Multiple)
                .Description("The magic eightball can answer any question!")
                .Do(async e =>
                {
                    string[] eightball = new string[] { "It is certain.", "It is decidedly so.", "Without a doubt.", "Yes, definitely.", "You may rely on it.", "As I see it, yes.", "Most likely.", "Outlook good.", "Yes.", "Signs point to yes.", "Reply hazy try again...", "Ask again later...", "Better not tell you now...", "Cannot predict now...", "Concentrate and ask again...", "Don't count on it.", "My reply is no.", "My sources say no.", "Outlook not so good.", "Very doubtful.", "Nyas.", "Why not?", "zzzzz...", "No." };
                    Random rnd = new Random();
                    if (String.Join(" ", e.Args)[String.Join(" ", e.Args).Length - 1] != '?')
                        await client.SendMessage(e.Channel, "You must ask a proper question!");
                    else
                        await client.SendMessage(e.Channel, $"*{eightball[rnd.Next(eightball.Length)]}*");
                });

            group.CreateCommand("hbavatar")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you the hummingbird avatar of the usernames provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("http://hummingbird.me/api/v1/users");
                    string message = "";
                    foreach (string s in e.Args)
                    {
                        var request = new RestRequest($"{s}", Method.GET);
                        if (rclient.Execute(request).Content[0] == '<')
                        {
                            message += $@"
{s} doesn't exist.";
                        }
                        else
                        {
                            JObject result = JObject.Parse(rclient.Execute(request).Content);
                            string username = result["name"].ToString();
                            string avatar = result["avatar"].ToString();
                            message += $@"
{username}'s avatar: {avatar}";
                        }
                    }
                    await client.SendMessage(e.Channel, message);
                });

            group.CreateCommand("hb")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you information on the hummingbird accounts of the usernames provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("http://hummingbird.me/api/v1/users");
                    foreach (string s in e.Args)
                    {
                        string message = "";
                        var request = new RestRequest($"{s}", Method.GET);
                        if (rclient.Execute(request).Content[0] == '<')
                        {
                            message += $@"{s} doesn't exist.";
                        }
                        else
                        {
                            JObject result = JObject.Parse(rclient.Execute(request).Content);
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
                            if (!String.IsNullOrWhiteSpace(location))
                                message += $@"
**Location**: {location}";
                            if (!String.IsNullOrWhiteSpace(website))
                                message += $@"
**Website**: {website}";
                            message += $@"
**Hummingbird page**: {userurl}";
                        }
                        await client.SendMessage(e.Channel, message);
                    }
                });

            group.CreateCommand("player")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you information on the Player.me of each usernames provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new System.Uri("https://player.me/api/v1/auth");
                    var request = new RestRequest("pre-login", Method.POST);
                    foreach (string s in e.Args)
                    {
                        request.AddQueryParameter("login", s);
                        JObject result = JObject.Parse(rclient.Execute(request).Content);
                        if (Convert.ToBoolean(result["success"]) == false)
                            await client.SendMessage(e.Channel, $"{s} was not found.");
                        else
                        {
                            string username = result["results"]["username"].ToString();
                            string avatar = "https:" + result["results"]["avatar"]["original"].ToString();
                            string bio = result["results"]["short_description"].ToString();
                            DateTime date = DateTime.Parse(result["results"]["created_at"].ToString());
                            string joined = date.ToString("yyyy-MM-dd");
                            int followers = Convert.ToInt32(result["results"]["followers_count"]);
                            int following = Convert.ToInt32(result["results"]["following_count"]);
                            await client.SendMessage(e.Channel, $@"
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
                .Do(async e =>
                {
                    await client.AcceptInvite(client.GetInvite(e.Args[0]).Result);
                });

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
                    int newPermLevel = 0;
                    int eUserPerm = GetPermissions(e.User, e.Channel);
                    if (e.Args[1] == "" || e.Message.MentionedUsers.Count() < 1)
                        await client.SendMessage(e.Channel, "You need to at least specify a permission level and mention one user.");
                    else if (!int.TryParse(e.Args[0], out newPermLevel))
                        await client.SendMessage(e.Channel, "The first argument needs to be the new permission level.");
                    else if (eUserPerm <= newPermLevel)
                        await client.SendMessage(e.Channel, "You can only set permission level to lower than your own.");
                    else
                    {
                        string reply = "";
                        foreach (User u in e.Message.MentionedUsers)
                        {
                            int oldPerm = GetPermissions(u, e.Channel);
                            if (oldPerm >= eUserPerm)
                            {
                                reply += $"<@{u.Id}>'s permission level is no less than yours, you are not allowed to change it.";
                                continue;
                            }
                            bool change_needed = oldPerm != newPermLevel;
                            if (change_needed)
                                await SQL.AddOrUpdateUserAsync(u.Id, "perms", newPermLevel.ToString());
                            if (reply != "")
                                reply += '\n';
                            reply += $"<@{u.Id}>'s permission level is "+(change_needed ? "now" : "already at")+$" {newPermLevel}.";
                        }
                        await client.SendMessage(e.Channel, reply);
                    }
                });

            // Owner commands

            group.CreateCommand("leave")
                .MinPermissions(3)
                .Description("I'll leave the server this command was used in.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "Bye bye!");
                    await Music.StopStreams(e.Server);
                    await client.LeaveServer(e.Server);
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
                        Role role = client.FindRoles(e.Server, e.Args[0]).FirstOrDefault(); // TODO: In the future, we will probably use MentionedRoles for this.
                        await client.EditRole(role, color: new Color(red, green, blue));
                        await client.SendMessage(e.Channel, $"Role {role.Name}'s color has been changed.");
                    }
                    else
                        await client.SendMessage(e.Channel, "The parameters are invalid.");
                });

            Flags.AddCommands(group);

            Chatbot.AddCommands(group);
        }

        // Variables
        internal static DiscordClient client;
        static CommandService commands;
        internal static RestClient rclient = new RestClient();
        static LastFM.LastfmClient lfclient;
        internal static JObject config;
        internal static long masterId;
        static string version;

	internal static User GetNeko(Server s) => s.CurrentUser;

        static void InputThread()
        {
            for (;;)
            {
                string input = Console.ReadLine();
            }
        }

        static void Main(string[] args)
        {
            // Load up the DB, or create it if it doesn't exist
            SQL.LoadDB();
            // Load up the config file
            LoadConfig();

            Console.Title = $"Nekobot v{version}";
            // Load the stream channels
            Music.LoadStreams();
            // Initialize rest client
            RCInit();

            client = new DiscordClient(new DiscordClientConfig
            {
                AckMessages = true,
                LogLevel = LogMessageSeverity.Verbose,
                TrackActivity = true,
                UseMessageQueue = false,
                UseLargeThreshold = true,
                EnableVoiceMultiserver = true,
                VoiceMode = DiscordVoiceMode.Outgoing,
            });

            // Set up the events and enforce use of the command prefix
            commands.CommandError += CommandError;
            client.Connected += Connected;
            client.Disconnected += Disconnected;
            client.UserJoined += UserJoined;
            client.LogMessage += LogMessage;
            client.AddService(commands);
            client.AddService(new PermissionLevelService(GetPermissions));
            commands.CreateGroup("", group => GenerateCommands(group));
            commands.NonCommands += Chatbot.Do;
            // Load the chatbots
            Chatbot.Load();
            // Keep the window open in case of crashes elsewhere... (hopefully)
            Thread input = new Thread(InputThread);
            input.Start();
            // Connection, join server if there is one in config, and start music streams
            client.Run(async() =>
            {
                try
                {
                    await client.Connect(config["email"].ToString(), config["password"].ToString());
                    if (config["server"].ToString() != "")
                    {
                        await client.AcceptInvite(client.GetInvite(config["server"].ToString()).Result);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.GetBaseException().Message}");
                }
                await Music.StartStreams();
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

        private static void RCInit()
        {
            rclient.UserAgent = $"Nekobot {version}";
        }

        private static void LogMessage(object sender, LogMessageEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[{e.Severity}] {e.Source} : {e.Message}");
        }

        private static void LoadConfig()
        {
            if (System.IO.File.Exists("config.json"))
                config = JObject.Parse(System.IO.File.ReadAllText(@"config.json"));
            else
            {
                Console.WriteLine("config.json file not found! Unable to initialize Nekobot!");
                SQL.CloseAndDispose();
                Console.ReadKey();
                Environment.Exit(0);
            }
            masterId = config["master"].ToObject<long>();
            Music.Folder = config["musicFolder"].ToString();
            Music.UseSubdirs = config["musicUseSubfolders"].ToObject<bool>();

            string helpmode = config["helpmode"].ToString();
            commands = new CommandService(new CommandServiceConfig
            {
                CommandChars = config["prefix"].ToString().ToCharArray(),
                RequireCommandCharInPrivate = config["prefixprivate"].ToObject<bool>(),
                RequireCommandCharInPublic = config["prefixpublic"].ToObject<bool>(),
                MentionCommandChar = config["mentioncommand"].ToObject<short>(),
                HelpMode = helpmode.Equals("public") ? HelpMode.Public : helpmode.Equals("private") ? HelpMode.Private : HelpMode.Disable
            }, Flags.GetNsfw, Flags.GetMusic, Flags.GetIgnored);

            version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private static void UserJoined(object sender, UserEventArgs e)
        {
            if (!Flags.GetIgnored(e.User))
                client.SendMessage(e.Server.DefaultChannel, $"Welcome to {e.Server.Name}, <@{e.User.Id}>!");
        }

        private static void Disconnected(object sender, DisconnectedEventArgs e)
        {
            Console.WriteLine("Disconnected");
        }

        private static void Connected(object sender, EventArgs e)
        {
            Console.WriteLine("Connected.");
        }

        private static void CommandError(object sender, CommandErrorEventArgs e)
        {
            string msg = e.Exception?.GetBaseException().Message;
            if (msg == null) //No mxception - show a generic message
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
                client.SendMessage(e.Channel, "Command Error: " + msg);
                //Console.WriteLine(msg);
            }
        }

        internal static int GetPermissions(User user, Channel channel)
        {
            if (user.Id == masterId)
                return 10;
            if (SQL.ExecuteScalarPos($"select count(perms) from users where user = '{user.Id}'"))
                return SQL.ReadInt(SQL.ReadUser(user.Id, "perms"));
            return 0;
        }

        internal static bool CanSay(Channel c, User u) => c.IsPrivate || c.Members.Where(m => m.Id == u.Id).SingleOrDefault().GetPermissions(c).SendMessages;
        internal static bool CanSay(ref Channel c, User u, Channel old)
        {
            if (CanSay(c, u))
                return true;
            c = old;
            return false;
        }

        private static IEnumerable<string> GraphemeClusters(string s)
        {
            var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(s);
            while (enumerator.MoveNext()) yield return (string)enumerator.Current;
        }

        private static async Task PerformAction(CommandEventArgs e, string action, string reaction, bool perform_when_empty)
        {
            User neko = GetNeko(e.Server);
            bool mentions_neko = e.Message.IsMentioningMe && string.Join(" ", e.Args).IndexOf($"@{neko.Name}") != -1;
            string message = $"<@{e.User.Id}> {action}s ";
            bool mentions_everyone = e.Message.MentionedRoles.Contains(e.Server.EveryoneRole);
            if (mentions_everyone)
                await client.SendMessage(e.Channel, $"{message}{Mention.Everyone()}");
            else
            {
                if (e.Message.MentionedUsers.Count() == (!mentions_neko && e.Message.IsMentioningMe ? 1 : 0))
                    message = perform_when_empty ? $"*{action}s <@{e.User.Id}>.*" : message + $"<@{client.CurrentUserId}>";
                else
                    foreach (User u in e.Message.MentionedUsers)
                        if (u != neko || mentions_neko)
                            message += $"<@{u.Id}> ";
                await client.SendMessage(e.Channel, message);
            }
            if (mentions_everyone || mentions_neko || (!perform_when_empty && e.Message.MentionedUsers.Count() == (e.Message.IsMentioningMe ? 1 : 0)))
                await client.SendMessage(e.Channel, $"{reaction}");
        }
    }
}
