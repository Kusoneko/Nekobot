using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;
using RestSharp;
using Discord.WebSocket;

namespace Nekobot
{
    partial class Program
    {
        internal static async Task<IMessageChannel> GetChannel(ulong id) => (IMessageChannel)client.GetChannel(id) ?? await client.GetDMChannelAsync(id);
    }
    static class Common
    {
        static string DoPing(IMessage msg)
           => $" ({DateTime.Now.Millisecond - msg.Timestamp.Millisecond} milliseconds)";

        static void AddResponseCommands(Commands.CommandGroupBuilder group, string file)
        {
            var json = Helpers.GetJsonFileIfExists(file);
            if (json == null) return;
            foreach (var cmdjson in json)
            {
                Helpers.CreateJsonCommand(group, cmdjson, (cmd,val) =>
                {
                    cmd.FlagNsfw(val["nsfw"].ToObject<bool>());
                    var responses = val["responses"].ToObject<string[]>();
                    if (responses.Length == 1) cmd.Do(async e => await e.Channel.SendMessageAsync(responses[0]));
                    else cmd.Do(async e => await e.Channel.SendMessageAsync(Helpers.Pick(responses)));
                });
            }
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("ping")
                .Description("I'll reply with 'Pong!'")
                .Do(e => e.Channel.SendMessageAsync($"{e.User.Mention}, Pong!{DoPing(e.Message)}"));

            group.CreateCommand("pong")
                .Hide() // More of an easter egg, don't show in help.
                .Description("I'll reply with 'Ping?'")
                .Do(e => e.Channel.SendMessageAsync($"{e.User.Mention}, Ping?{DoPing(e.Message)}"));

            group.CreateCommand("uptime")
                .Description("I'll tell you how long I've been awake~")
                .Do(e => e.Channel.SendMessageAsync(Format.Code(Helpers.Uptime().ToString())));

            Func<SocketRole, string> role_info = r =>
            {
                string ret = $"{r.Name} is id {r.Id}, has {r.Members.Count()} members, color is {r.Color}, perms are {r.Permissions.RawValue}, and position is {r.Position}";
                if (r.IsManaged) ret += "; it is managed by the server";
                return $"{ret}.\n";
            };
            group.CreateCommand("whois")
                .Alias("getinfo")
                .Parameter("[@User1] [@User2] [...]", Commands.ParameterType.Unparsed)
                .Description("I'll give you information about the mentioned user(s).")
                .Do(async e =>
                {
                    if (e.Args[0].Length == 0 || (!e.Message.MentionedUserIds.Any() && !e.Message.MentionedRoleIds.Any())) return;
                    string reply = "";
                    bool oniicheck = e.User.Id == 63299786798796800;
                    foreach (var t in e.Message.Tags)
                    {
                        switch (t.Type)
                        {
                            case TagType.RoleMention:
                                reply += role_info(t.Value as SocketRole);
                                break;
                            case TagType.UserMention:
                                var u = t.Value as IGuildUser;
                                bool onii = oniicheck && u.Id == 63296013791666176;
                                string possessive = onii ? "his" : "their";
                                reply += u.Username;
                                if (!string.IsNullOrEmpty(u.Nickname)) reply += $" (Nick: {u.Nickname})";
                                reply += $"{(onii ? " is your onii-chan <3 and his" : "'s")} id is {u.Id}, {possessive} discriminator is {u.Discriminator} and {possessive} permission level is {Helpers.GetPermissions(u, e.Channel)}.";
                                if (u.IsBot) reply += "\nAlso, they are a bot!";
                                reply += '\n';
                                break;
                        }
                    }
                    await e.Channel.SendMessageAsync('\n' + reply);
                });

            group.CreateCommand("whois role")
                .Alias("getinfo role")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .Description("I'll give you info on particular roles by name (comma separated)")
                .Do(e =>
                {
                    string reply = "";
                    if (e.Args[0].Length == 0)
                        reply = "You need to provide at least one role name!";
                    else Helpers.CommaSeparateRoleNames(e, (roles, str) =>
                    {
                        foreach (var r in roles)
                            reply += role_info(r);
                    });
                    e.Channel.SendMessageAsync(reply);
                });
            
            Music.AddCommands(group);

            Image.AddCommands(group);

            Func<Commands.CommandEventArgs, Task<bool>> lookup_nothing = async e =>
            {
                var args = e.Args[0];
                if (args.Length == 0)
                {
                    await Helpers.SendEmbed(e, Helpers.EmbedDesc("I cannot lookup nothing, silly!"));
                    return true;
                }
                return false;
            };

            group.CreateCommand("urban")
                .Alias("urbandictionary")
                .Alias("ud")
                .Parameter("phrase", Commands.ParameterType.Unparsed)
                .Description("I'll give you the urban dictionary definition of a phrase.")
                .Do(async e =>
                {
                    if (await lookup_nothing(e)) return;
                    var req = new RestRequest("define", Method.GET);
                    req.AddQueryParameter("term", e.Args[0]);
                    var json = JObject.Parse(Helpers.GetRestClient("http://api.urbandictionary.com/v0").Execute(req).Content);
                    var list = json["list"];
                    if (!list.HasValues)
                    {
                        await Helpers.SendEmbed(e, Helpers.EmbedDesc("No results found."));
                        return;
                    }
                    var resp = list[0];
                    var embed = Helpers.EmbedDesc(resp["definition"].ToString())
                        .WithTitle(resp["word"].ToString())
                        .WithUrl(resp["permalink"].ToString())
                        .WithFooter($"⬆{resp["thumbs_up"]} ⬇{resp["thumbs_down"]}")
                        .WithTimestamp(DateTime.Parse(resp["written_on"].ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind))
                        .AddField("Example", resp["example"]);
                    var sounds = resp["sound_urls"];
                    if (sounds.HasValues)
                        embed.AddField(sounds.Count() > 1 ? "Sounds" : "Sound", string.Join("\n", sounds)); // I wish we could embed just one of these and have an audio player, but this works too.
                    await Helpers.SendEmbed(e, embed);
                });

            if (Helpers.FieldExists("WolframAlpha", "appid"))
            {
                group.CreateCommand("wolfram")
                    .Parameter("input", Commands.ParameterType.Unparsed)
                    .Description("I'll look something up for you on WolframAlpha")
                    .Do(async e =>
                    {
                        if (await lookup_nothing(e)) return;
                        var rc = Helpers.GetRestClient("http://api.wolframalpha.com/v2/"); // TODO: Do we want this static?
                        rc.AddDefaultParameter("appid", Program.config["WolframAlpha"]["appid"]);
                        var req = new RestRequest("query", Method.GET);
                        req.AddQueryParameter("input", e.Args[0]);
                        var json = Helpers.XmlToJson(rc.Execute(req).Content)["queryresult"];
                        if (!json["@success"].ToObject<bool>())
                        {
                            const string didyoumeans = "didyoumeans";
                            if (Helpers.FieldExists(json, didyoumeans))
                            {
                                var embed = Helpers.EmbedBuilder.WithTitle("Perhaps you meant");
                                json = json[didyoumeans];
                                int count = json["@count"].ToObject<int>();
                                string ret = "";
                                Func<JToken, string> format_suggestion = suggestion => $" `{suggestion["#text"]}`";
                                json = json["didyoumean"];
                                if (count == 1)
                                    ret += format_suggestion(json);
                                else for (int i = 0; i < count; ++i)
                                    ret += (i == 0 ? "" : i == count-1 ? ", or " : ",")+format_suggestion(json[i]);
                                ret += '?';
                                await Helpers.SendEmbed(e, embed.WithDescription(ret.TrimStart()));
                            }
                            await Helpers.SendEmbed(e, Helpers.EmbedDesc("Sorry, I couldn't find anything for your input."));
                        }
                        else
                        {
                            int show = 4; // Show the first four results
                            json = json["pod"];
                            string ret = "";
                            //var embed = Helpers.EmbedBuilder.WithTitle($"Results for {e.Args}");
                            for (int i = 0, count = json.Count(); show != 0 && i < count; ++i)
                            {
                                var pod = json[i];
                                int numsubpods = pod["@numsubpods"].ToObject<int>();
                                if (numsubpods == 1)
                                {
                                    ret += $"{pod["subpod"]["img"]["@src"]}\n";
                                    --show;
                                }
                                else for (int j =0; show != 0 && j < numsubpods; ++j, --show)
                                    ret += $"{pod["subpod"][j]["img"]["@src"]}\n";
                            }
                            await e.Channel.SendMessageAsync(ret);
                            //await Helpers.SendEmbed(e, embed.WithDescription(ret)); // I don't know how this would look good, at this point.
                        }
                    });
            }

            var quote_site = "http://bacon.mlgdoor.uk/";
            group.CreateCommand("quote")
                .Description($"I'll give you a random quote from {quote_site}quotes")
                .Do(async e =>
                {
                    var result = JObject.Parse(Helpers.GetRestClient(quote_site).Execute<JObject>(new RestRequest("api/v1/quotes/random", Method.GET)).Content)["quotes"][0];
                    await e.Channel.SendMessageAsync($"\"{result["quote"]}\" - {result["author"]} {result["year"]}");
                });

            Func<string, string, string, string> add_quote = (quote, author, year) =>
            {
                var result = JObject.Parse(Helpers.GetRestClient(quote_site)
                    .Execute<JObject>(new RestRequest("api/v1/quotes", Method.POST)
                        .AddParameter("quote", quote)
                        .AddParameter("author", author)
                        .AddParameter("year", year))
                    .Content);
                return result["success"].ToObject<bool>() ? "Quote added." : $"Adding quote failed: {result["data"]}";
            };
            group.CreateCommand("addquote")
                .Parameter("<quote>|<author>[|year]", Commands.ParameterType.MultipleUnparsed)
                .Description($"I'll add a quote to {quote_site}quotes, mentions will be garbage text in this.")
                .Do(async e =>
                {
                    // TODO: Resolve mentions?
                    var args = string.Join(" ", e.Args).Split('|');
                    if (args.Length < 2)
                    {
                        await e.Channel.SendMessageAsync("I need a quote and its author, silly!");
                        return;
                    }
                    await e.Channel.SendMessageAsync(add_quote(args[0], args[1], args.Length == 2 ? DateTime.Now.Year.ToString() : args[2]));
                });
            group.CreateCommand("quotemessage")
                .Parameter("messageid", Commands.ParameterType.Required)
                .Description($"I'll add a message from this channel as a quote on {quote_site}quotes, mentions will be resolved.")
                .Do(async e =>
                {
                    IMessage message = await e.Channel.GetMessageAsync(Convert.ToUInt64(e.Args[0]));
                    if (message == null) // It's missing, report failure.
                    {
                        await e.Channel.SendMessageAsync("Sorry, I couldn't find that message!");
                        return;
                    }
                    await e.Channel.SendMessageAsync(add_quote(Helpers.ResolveTags(message), Helpers.Nickname(message.Author as SocketGuildUser), message.Timestamp.Date.ToShortDateString()));
                });

            Google.AddCommands(group);

            group.CreateCommand("8ball")
                .Parameter("question", Commands.ParameterType.Optional)
                .Parameter("?", Commands.ParameterType.Multiple)
                .Description("The magic eightball can answer any question!")
                .Do(async e =>
                {
                    // TODO: Decide if we want to load this will all the other response commands, if so this check could be bypassed
                    // Note: We'd also need to put all responses in asterisks.
                    if (!string.Join(" ", e.Args).EndsWith("?"))
                    {
                        await e.Channel.SendMessageAsync("You must ask a proper question!");
                        return;
                    }
                    string[] eightball =
                    {
                        "It is certain.", "It is decidedly so.", "Without a doubt.",
                        "Yes, definitely.", "You may rely on it.", "As I see it, yes.", "Most likely.", "Outlook good.",
                        "Yes.", "Signs point to yes.", "Reply hazy try again...", "Ask again later...",
                        "Better not tell you now...", "Cannot predict now...", "Concentrate and ask again...",
                        "Don't count on it.", "My reply is no.", "My sources say no.", "Outlook not so good.",
                        "Very doubtful.", "Nyas.", "Why not?", "zzzzz...", "No."
                    };
                    await e.Channel.SendMessageAsync($"*{Helpers.Pick(eightball)}*");
                });

            AddResponseCommands(group, "response_commands.json");
            AddResponseCommands(group, "custom_response_commands.json");

            group.CreateCommand("say")
                .Alias("forward")
                .Alias("echo")
                .Parameter("[#channel or @User (or user/channel id in PMs)] text...", Commands.ParameterType.Unparsed)
                .Description("I'll repeat what you said. (To a given user or channel)")
                .Do(async e =>
                {
                    IMessageChannel channel = e.Channel;
                    string message = e.Args[0];
                    if (message.Length == 0) return; // Unparsed can be empty

                    /* Liru Note: I don't think we have to do this anymore.
                    message = e.Message.MentionedChannels.Aggregate(
                        e.Message.MentionedUsers.Aggregate(message, (m, u) => m.Replace($"@{u.Name}", u.Mention)),
                        (m, c) => m.Replace($"#{c.Name}", c.Mention));
                        */

                    if (message.StartsWith("<@") || message.StartsWith("<#"))
                    {
                        bool selfmention = e.Message.MentionedUserIds.Contains(Program.Self.Id);
                        var tag = e.Message.Tags.Skip(selfmention ? 1 : 0).FirstOrDefault();
                        var usermention = tag.Type == TagType.UserMention;
                        if (tag != null && (usermention || tag.Type == TagType.ChannelMention))
                        {
                            // FIXME: This will fail in some cases, like mentioning a channel way later... we should check the mention is directly after, aside from spacing
                            int index = message.IndexOf('>', selfmention ? message.IndexOf('>') : 0);
                            if (index+2 < message.Length)
                            {
                                ulong mentionid = tag.Key;
                                if (mentionid != Program.client.CurrentUser.Id)
                                {
                                    channel = usermention ? await (tag.Value as IUser).GetOrCreateDMChannelAsync() : tag.Value as IMessageChannel;
                                    if (Helpers.CanSay(ref channel, await channel.GetUserAsync(e.User.Id), e.Channel))
                                        message = message.Substring(index + 2);
                                }
                            }
                        }
                    }
                    else if (channel is IPrivateChannel)
                    {
                        try
                        {
                            var index = message.IndexOf(' ');
                            if (index != -1 && index+2 < message.Length)
                            {
                                channel = await Program.GetChannel(Convert.ToUInt64(message.Substring(0, index)));
                                if (Helpers.CanSay(ref channel, (IGuildUser)channel.GetUserAsync(e.User.Id), e.Channel))
                                    message = message.Substring(index+1);
                            }
                        } catch { }
                    }
                    if (message.TrimEnd() != "")
                        await channel.SendMessageAsync(message);
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
                        await e.Channel.SendMessageAsync(string.Join("", Helpers.GraphemeClusters(text).Reverse().ToArray()));
                });

            group.CreateCommand("whereami")
                .Alias("channelinfo")
                .Alias("channel")
                .Alias("location")
                .Alias("where")
                .Description("I'll tell you information about the channel and server you're asking me this from.")
                .Do(async e =>
                {
                    if (e.Channel is IPrivateChannel)
                        await e.Channel.SendMessageAsync("You're in a private message with me, baka.");
                    else
                    {
                        var owner = await e.Server.GetOwnerAsync();
                        var chan = e.Channel as ITextChannel;
                        string message = $@"You are currently in {e.Channel.Name} (id: {e.Channel.Id})
on server **{e.Server.Name}** (id: {e.Server.Id}) (region: {Program.client.GetVoiceRegion(e.Server.VoiceRegionId).Name} (id: {e.Server.VoiceRegionId}))
owned by {owner.Nickname ?? owner.Username} (id: {e.Server.OwnerId}).";
                        if (!string.IsNullOrEmpty(chan.Topic))
                            message = message + $@"
The current topic is: {chan.Topic}";
                        await e.Channel.SendMessageAsync(message);
                    }
                });

            group.CreateCommand("avatar")
                .Parameter("[@User1] [@User2] [...]", Commands.ParameterType.Unparsed)
                .Description("I'll give you the avatars of every mentioned users.")
                .Do(async e =>
                {
                    if (e.Args[0].Length == 0) return;
                    foreach (var t in e.Message.Tags)
                        if (t.Type == TagType.UserMention)
                        {
                            var u = t.Value as IUser;
                            var url = u.GetAvatarUrl();
                            await e.Channel.SendMessageAsync(u.Mention + (url == null ? " has no avatar." : $"'s avatar is: {url.Substring(0, url.LastIndexOf('?'))}"));
                        }
                });

            group.CreateCommand("lastlog")
                .Parameter("few (default 4)", Commands.ParameterType.Optional)
                .Parameter("string to search for (case-sensitive)", Commands.ParameterType.Unparsed)
                .Description("I'll search for and return the last `few` messages in this channel with your search string in them (This may take a while, depending on history size and `few`)")
                .Do(async e =>
                {
                    var args = e.Args;
                    if (!Helpers.HasArg(args))
                        await e.Channel.SendMessageAsync("Just read the last messages yourself, baka!");
                    else
                    {
                        int few = 4;
                        if (Helpers.HasArg(args, 1))
                        {
                            if (int.TryParse(args[0], out few))
                            {
                                if (few <= 0)
                                {
                                    await e.Channel.SendMessageAsync("You're silly!");
                                    return;
                                }
                                args = args.Skip(1).ToArray();
                            }
                            else few = 4;
                        }

                        var search = string.Join(" ", args).TrimEnd();
                        var found = new List<IMessage>();
                        await Helpers.DoToMessages((e.Channel as SocketTextChannel), few, (msgs, has_cmd_msg) =>
                        {
                            Func<IMessage, bool> find = s => Helpers.ResolveTags(s).Contains(search);
                            found.AddRange(has_cmd_msg ? msgs.Where(s => s.Id != e.Message.Id && find(s)) : msgs.Where(find));
                            return found.Count();
                        });

                        if ((few = Math.Min(found.Count(), few)) == 0)
                            await e.Channel.SendMessageAsync("None found...");
                        else foreach (var msg in found.Take(few))
                        {
                            var extradata = $"[[{msg.Timestamp}]({msg.GetJumpUrl()})]{msg.Author.Username}:";
                            //if (msg.Content.Length > EmbedBuilder.MaxDescriptionLength) // This should never happen, unless we decide to start searching embeds.
                            {
                                var builder = Helpers.EmbedBuilder;
                                builder.WithTimestamp(msg.Timestamp).WithDescription(msg.Content);
                                builder.WithTitle($"{msg.Author.ToString()}'s message").WithUrl(msg.GetJumpUrl());
                                await e.Channel.SendMessageAsync(embed: builder.Build());
                            }
                            //else await e.Channel.SendMessageAsync($"Content too long to show, message here: <{msg.GetJumpUrl()}>");
                        }
                    }
                });

            RPG.AddCommands(group);

            group.CreateCommand("lotto")
                .Description("I'll give you a set of 6 lucky numbers!")
                .Do(async e =>
                {
                    List<int> lotto = new List<int>();
                    Random rnd = new Random();
                    for (var i = 0; i != 6; ++i)
                    {
                        var number = rnd.Next(1, 60);
                        if (!lotto.Contains(number))
                            lotto.Add(number);
                    }
                    await e.Channel.SendMessageAsync($"Your lucky numbers are **{lotto[0]}, {lotto[1]}, {lotto[2]}, {lotto[3]}, {lotto[4]}, {lotto[5]}**.");
                });

            // TODO: Decide if PerformAction commands should be moved to their own json file like response_commands.json
            group.CreateCommand("pet")
                .Alias("pets")
                .Parameter("[@User1] [@User2] [...]", Commands.ParameterType.Unparsed)
                .Description("Everyone loves being pet, right!?! Pets each *@user*. Leave empty (or mention me too) to pet me!")
                .Do(e => Helpers.PerformAction(e, "pet", "*purrs*", false));

            group.CreateCommand("hug")
                .Alias("hugs")
                .Parameter("[@User1] [@User2] [...]", Commands.ParameterType.Unparsed)
                .Description("Hug someone! Hugs each *@user*. Leave empty to get a hug!")
                .Do(e => Helpers.PerformAction(e, "hug", "<3", true));
        }
    }
}
