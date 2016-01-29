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
        static Task DoPing(int ms, Message msg)
           => msg.Edit($"{msg.RawText} ({msg.Timestamp.Millisecond - ms} milliseconds)");

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("ping")
                .Description("I'll reply with 'Pong!'")
                .Do(async e => await DoPing(e.Message.Timestamp.Millisecond, await e.Channel.SendMessage($"{e.User.Mention}, Pong!")));

            group.CreateCommand("pong")
                .Hide() // More of an easter egg, don't show in help.
                .Description("I'll reply with 'Ping?'")
                .Do(async e => await DoPing(e.Message.Timestamp.Millisecond, await e.Channel.SendMessage($"{e.User.Mention}, Ping?")));

            group.CreateCommand("uptime")
                .Description("I'll tell you how long I've been awake~")
                .Do(e => e.Channel.SendMessage(Format.Code(Helpers.Uptime().ToString())));

            group.CreateCommand("whois")
                .Alias("getinfo")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("I'll give you information about the mentioned user(s).")
                .Do(async e =>
                {
                    if (e.Args[0] == "" || !e.Message.MentionedUsers.Any()) return;
                    string reply = "";
                    bool oniicheck = e.User.Id == 63299786798796800;
                    foreach (User u in e.Message.MentionedUsers)
                    {
                        bool onii = oniicheck && u.Id == 63296013791666176;
                        reply += $@"
{u.Mention}{(onii ? " is your onii-chan <3 and his" : "'s")} id is {u.Id} and {(onii ? "his" : "their")} permission level is {Helpers.GetPermissions(u, e.Channel)}.
";
                    }
                    await e.Channel.SendMessage(reply);
                });

            
            Music.AddCommands(group);

            Image.AddCommands(group);

            group.CreateCommand("fortune")
                .Description("I'll give you a fortune!")
                .Do(async e =>
                {
                    string[] fortunes =
                    {
                        "Don't sleep for too long, or you'll miss naptime!",
                        "Before crying over spilt milk, remember it can still be delicious without a bowl.",
                        "A bird in the paw is worth nom nom nom...",
                        "Let no surface, no matter how high or cluttered, go unexplored.",
                        "Neko never catches the laser if neko never tries.",
                        "Our greatest glory is not in never falling, but in making sure master doesn't find the mess.",
                        "A mouse shared halves the food but doubles the happiness.",
                        "There exists nary a toy as pertinent as the box from whence that toy came.",
                        "Neko will never be fed if neko does not meow all day!",
                        "Ignore physics, and physics will ignore you.",
                        "Never bite the hand that feeds you!",
                        "Before finding the red dot, you must first find yourself.",
                        "Some see the glass half empty. Some see the glass half full. Neko sees the glass and knocks it over.",
                        "Make purrs not war.",
                        "Give a neko fish and you feed them for a day; Teach a neko to fish and... mmmm fish.",
                        "Wheresoever you go, go with all of master's things.",
                        "Live your dreams every day! Why do you think neko naps so much?",
                        "The hardest thing of all is to find a black cat in a dark room, especially if there is no cat.",
                        "Meow meow meow meow, meow meow. Meow meow meow."
                    };
                    await e.Channel.SendMessage(Helpers.Pick(fortunes));
                });

            group.CreateCommand("littany")
                .Description("His power divine, utterances of the finest of the imperium to motivate you on your way~")
                .Do(async e =>
                {
                    string[] littanies =
                    {
                        "Bless the Simpleton, for his mind has no room for doubt.",
                        "When purging the guilty do not spare the innocent, for in death you free them from their invertible corruption",
                        "Faith in the Emperor destroys all errors.",
                        "I am rather a Martyr forever than a coward for a second.",
                        "War is an act of violence to force the will of the Emperor.",
                        "In the hour of greatest need, our Emperor shall walk among us once more, and the stars themselves will hide.",
                        "To err is human. To correct is divine.",
                        "Better one hundred innocents burn than one heretic go free.",
                        "Strength does not come from physical capacity. It comes from an indomitable will.",
                        "The strong can never forgive. Forgiveness is the attribute of the weak.",
                        "The innocent man, the devout man, does not fear meeting his Emperor.",
                        "Redeem with Bolter. Cleanse with Flamer. Purify from Orbit.",
                        "The only thing to fear is corruption itself."
                    };
                    await e.Channel.SendMessage(Helpers.Pick(littanies));
                });

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

            group.CreateCommand("nya")
                .Alias("nyaa")
                .Alias("nyan")
                .Description("I'll say 'Nyaa~'")
                .Do(async e => await e.Channel.SendMessage("Nyaa~"));

            group.CreateCommand("poi")
                .Description("I'll say 'Poi!'")
                .Do(async e => await e.Channel.SendMessage("Poi!"));

            group.CreateCommand("aicrai")
                .Alias("aicraievritiem")
                .Alias("aicraievritaim")
                .Alias("sadhorn")
                .Alias("icri")
                .Description("When sad things happen...")
                .Do(async e => await e.Channel.SendMessage("https://youtu.be/0JAn8eShOo8"));

            group.CreateCommand("notnow")
                .Alias("rinpls")
                .Description("How to Rekt: Rin 101")
                .Do(async e => await e.Channel.SendMessage("https://youtu.be/2BZUzJfKFwM"));

            group.CreateCommand("uninstall")
                .Description("A great advice in any situation.")
                .Do(async e => await e.Channel.SendMessage("https://youtu.be/TJB0uCERrEQ"));

            group.CreateCommand("killyourself")
                .Alias("kys")
                .Description("Another great advice.")
                .Do(async e => await e.Channel.SendMessage("https://youtu.be/2dbR2JZmlWo"));

            group.CreateCommand("congratulations")
                .Alias("congrats")
                .Alias("grats")
                .Alias("gg")
                .Description("Congratulate someone for whatever reason.")
                .Do(async e => await e.Channel.SendMessage("https://youtu.be/oyFQVZ2h0V8"));

            group.CreateCommand("gitgud")
                .Description("A great advice in any situation.")
                .Do(async e => await e.Channel.SendMessage("https://youtu.be/xzpndHtdl9A"));

            group.CreateCommand("lenny")
                .Description("For *special* moments.")
                .Do(async e => await e.Channel.SendMessage("( ͡° ͜ʖ ͡°)"));

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
                            string chanstr = message.Split(' ').First();
                            if (chanstr.Length+1 < message.Length)
                            {
                                channel = await Program.GetChannel(Convert.ToUInt64(chanstr));
                                if (Helpers.CanSay(ref channel, e.User, e.Channel))
                                    message = message.Substring(message.IndexOf(" ")+1);
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
on server **{e.Server.Name}** (id: {e.Server.Id}) (region: {e.Server.Region})
owned by {e.Server.Owner.Name} (id {e.Server.Owner.Id}).";
                        if (e.Channel.Topic != "" || e.Channel.Topic != null)
                            message = message + $@"
The current topic is: {e.Channel.Topic}";
                        await e.Channel.SendMessage(message);
                    }
                });

            group.CreateCommand("avatar")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
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

            group.CreateCommand("pet")
                .Alias("pets")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("Everyone loves being pet, right!?! Pets each *@user*. Leave empty (or mention me too) to pet me!")
                .Do(e => Helpers.PerformAction(e, "pet", "*purrs*", false));

            group.CreateCommand("hug")
                .Alias("hugs")
                .Parameter("@User1] [@User2] [...", Commands.ParameterType.Unparsed)
                .Description("Hug someone! Hugs each *@user*. Leave empty to get a hug!")
                .Do(e => Helpers.PerformAction(e, "hug", "<3", true));
        }
    }
}
