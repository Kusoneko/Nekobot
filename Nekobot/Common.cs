using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Nekobot
{
    partial class Program
    {
        internal static async Task<Channel> GetChannel(ulong id) => client.GetChannel(id) ?? await client.CreatePrivateChannel(id);
    }
    static class Common
    {
        static string DoPing(Message msg)
           => $" ({DateTime.Now.Millisecond - msg.Timestamp.Millisecond} milliseconds)";

        static void AddResponseCommands(Commands.CommandGroupBuilder group, string file)
        {
            var json = Helpers.GetJsonFileIfExists(file);
            if (json == null) return;
            foreach (var cmdjson in json)
            {
                var val = cmdjson.Value;
                Helpers.CreateJsonCommand(group, cmdjson.Key, val, cmd =>
                {
                    cmd.FlagNsfw(val["nsfw"].ToObject<bool>());
                    var responses = val["responses"].ToObject<string[]>();
                    if (responses.Length == 1) cmd.Do(async e => await e.Channel.SendMessage(responses[0]));
                    else cmd.Do(async e => await e.Channel.SendMessage(Helpers.Pick(responses)));
                });
            }
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("ping")
                .Description("I'll reply with 'Pong!'")
                .Do(e => e.Channel.SendMessage($"{e.User.Mention}, Pong!{DoPing(e.Message)}"));

            group.CreateCommand("pong")
                .Hide() // More of an easter egg, don't show in help.
                .Description("I'll reply with 'Ping?'")
                .Do(e => e.Channel.SendMessage($"{e.User.Mention}, Ping?{DoPing(e.Message)}"));

            group.CreateCommand("uptime")
                .Description("I'll tell you how long I've been awake~")
                .Do(e => e.Channel.SendMessage(Format.Code(Helpers.Uptime().ToString())));

            Func<Role, string> role_info = r =>
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
                    if (e.Args[0] == "" || (!e.Message.MentionedUsers.Any() && !e.Message.MentionedRoles.Any())) return;
                    string reply = "";
                    bool oniicheck = e.User.Id == 63299786798796800;
                    foreach (User u in e.Message.MentionedUsers)
                    {
                        bool onii = oniicheck && u.Id == 63296013791666176;
                        string possessive = onii ? "his" : "their";
                        reply += u.Name;
                        if (!string.IsNullOrEmpty(u.Nickname)) reply += $" (Nick: {u.Nickname})";
                        reply += $"{(onii ? " is your onii-chan <3 and his" : "'s")} id is {u.Id}, {possessive} discriminator is {u.Discriminator} and {possessive} permission level is {Helpers.GetPermissions(u, e.Channel)}.";
                        if (u.IsBot) reply += " Also, they are a bot!";
                        reply += '\n';
                    }
                    foreach (Role r in e.Message.MentionedRoles)
                        reply += role_info(r);
                    await e.Channel.SendMessage('\n' + reply);
                });

            group.CreateCommand("whois role")
                .Alias("getinfo role")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .Description("I'll give you info on particular roles by name (comma separated)")
                .Do(e =>
                {
                    string reply = "";
                    if (e.Args[0] == "")
                        reply = "You need to provide at least one role name!";
                    else Helpers.CommaSeparateRoleNames(e, (roles, str) =>
                    {
                        foreach (var r in roles)
                            reply += role_info(r);
                    });
                    e.Channel.SendMessage(reply);
                });
            
            Music.AddCommands(group);

            Image.AddCommands(group);

            group.CreateCommand("quote")
                .Description("I'll give you a random quote from https://inspiration.julxzs.website/quotes")
                .Do(async e =>
                {
                    var result = JObject.Parse(Helpers.GetRestClient("https://inspiration.julxzs.website").Execute<JObject>(new RestRequest("api/quote", Method.GET)).Content)["quote"];
                    await e.Channel.SendMessage($"\"{result["quote"]}\" - {result["author"]} {result["date"]}");
                });

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
                        await e.Channel.SendMessage("You must ask a proper question!");
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
                    await e.Channel.SendMessage($"*{Helpers.Pick(eightball)}*");
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
                    Channel channel = e.Channel;
                    string message = e.Args[0];
                    if (message == "") return; // Unparsed can be empty

                    message = e.Message.MentionedChannels.Aggregate(
                        e.Message.MentionedUsers.Aggregate(message, (m, u) => m.Replace($"@{u.Name}", u.Mention)),
                        (m, c) => m.Replace($"#{c.Name}", c.Mention));

                    bool usermention = e.Message.MentionedUsers.Count() > (e.Message.IsMentioningMe() ? 1 : 0) && message.StartsWith("<@");
                    if (usermention || (e.Message.MentionedChannels.Any() && message.StartsWith("<#")))
                    {
                        int index = message.IndexOf(">");
                        if (index+2 < message.Length)
                        {
                            ulong mentionid = Convert.ToUInt64(message.Substring(2, index-2));
                            if (mentionid != e.Server.CurrentUser.Id)
                            {
                                channel = usermention ? await e.Message.MentionedUsers.First(u => u.Id == mentionid).CreatePMChannel()
                                    : e.Message.MentionedChannels.First(c => c.Id == mentionid);
                                if (Helpers.CanSay(ref channel, e.User, e.Channel))
                                    message = message.Substring(index + 2);
                            }
                        }
                    }
                    else if (channel.IsPrivate)
                    {
                        try
                        {
                            var index = message.IndexOf(' ');
                            if (index != -1 && index+2 < message.Length)
                            {
                                channel = await Program.GetChannel(Convert.ToUInt64(message.Substring(0, index)));
                                if (Helpers.CanSay(ref channel, e.User, e.Channel))
                                    message = message.Substring(index+1);
                            }
                        } catch { }
                    }
                    if (message.TrimEnd() != "")
                        await channel.SendMessage(message);
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
                        await e.Channel.SendMessage(string.Join("", Helpers.GraphemeClusters(text).Reverse().ToArray()));
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
                        await e.Channel.SendMessage("You're in a private message with me, baka.");
                    else
                    {
                        string message = $@"You are currently in {e.Channel.Name} (id: {e.Channel.Id})
on server **{e.Server.Name}** (id: {e.Server.Id}) (region: {e.Server.Region.Name} (id: {e.Server.Region.Id}))
owned by {e.Server.Owner.Name} (id: {e.Server.Owner.Id}).";
                        if (e.Channel.Topic != "" || e.Channel.Topic != null)
                            message = message + $@"
The current topic is: {e.Channel.Topic}";
                        await e.Channel.SendMessage(message);
                    }
                });

            group.CreateCommand("avatar")
                .Parameter("[@User1] [@User2] [...]", Commands.ParameterType.Unparsed)
                .Description("I'll give you the avatars of every mentioned users.")
                .Do(async e =>
                {
                    if (e.Args[0] == "") return;
                    foreach (User u in e.Message.MentionedUsers)
                        await e.Channel.SendMessage(u.Mention + (u.AvatarUrl == null ? " has no avatar." : $"'s avatar is: {u.AvatarUrl}"));
                });

            group.CreateCommand("lastlog")
                .Parameter("few (default 4)", Commands.ParameterType.Optional)
                .Parameter("string to search for (case-sensitive)", Commands.ParameterType.Unparsed)
                .Description("I'll search for and return the last `few` messages in this channel with your search string in them (This may take a while, depending on history size and `few`)")
                .Do(async e =>
                {
                    var args = e.Args;
                    if (!Helpers.HasArg(args))
                        await e.Channel.SendMessage("Just read the last messages yourself, baka!");
                    else
                    {
                        int few = 4;
                        if (Helpers.HasArg(args, 1))
                        {
                            if (int.TryParse(args[0], out few))
                            {
                                if (few <= 0)
                                {
                                    await e.Channel.SendMessage("You're silly!");
                                    return;
                                }
                                args = args.Skip(1).ToArray();
                            }
                            else few = 4;
                        }

                        var search = string.Join(" ", args).TrimEnd();
                        var found = new List<Message>();
                        await Helpers.DoToMessages(e.Channel, few, (msgs, has_cmd_msg) =>
                        {
                            found.AddRange(has_cmd_msg ? msgs.Where(s => s.Id != e.Message.Id && s.Text.Contains(search)) : msgs.Where(s => s.Text.Contains(search)));
                            return found.Count();
                        });

                        if ((few = Math.Min(found.Count(), few)) == 0)
                            await e.Channel.SendMessage("None found...");
                        else foreach (var msg in found.Take(few))
                        {
                            var extradata = $"[{msg.Timestamp}]{msg.User.Name}:";
                            // If the message would reach the max if we add extra data, send that separate.
                            if (msg.RawText.Length + extradata.Length >= 1999)
                            {
                                await e.Channel.SendMessage(extradata);
                                await e.Channel.SendMessage(msg.RawText);
                            }
                            else await e.Channel.SendMessage($"{extradata} {msg.RawText}");
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
                    await e.Channel.SendMessage($"Your lucky numbers are **{lotto[0]}, {lotto[1]}, {lotto[2]}, {lotto[3]}, {lotto[4]}, {lotto[5]}**.");
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
