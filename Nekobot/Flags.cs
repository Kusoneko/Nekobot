using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Nekobot.Commands.Permissions.Levels;

namespace Nekobot
{
    class Flags
    {
        internal static bool GetIgnored(Channel chan, User user) => GetIgnored(chan) || GetIgnored(user);
        internal static bool GetIgnored(User user) => GetIgnored("user", "users", user.Id);
        internal static bool GetIgnored(Channel chan) => GetIgnored("channel", "flags", chan.Id);
        static bool GetIgnored(string row, string table, ulong id) => SQL.ReadBool(SQL.ReadSingle(row, table, id, "ignored"));

        internal static async Task<string> SetIgnored(string row, string table, ulong id, char symbol, int perms, int their_perms = 0)
        {
            if (symbol == '#' && perms <3)
                return $"You are not worthy of changing channel ignored status (permissions < 3).";
            if (symbol == '@')
            {
                if (id == Program.masterId)
                    return $"<@{id}> is my senpai and shall not be ignored!";
                if (perms <= their_perms)
                    return $"You are no more powerful than <@{id}>.";
            }
            bool in_table = SQL.InTable(row, table, id);
            bool isIgnored = in_table && GetIgnored(row, table, id);
            await SQL.ExecuteNonQueryAsync(SQL.AddOrUpdateCommand(row, table, id, "ignored", Convert.ToInt32(!isIgnored).ToString(), in_table));
            return $"<{symbol}{id}> is " + (!isIgnored ? "now" : "no longer") + " ignored.";
        }

        internal static bool GetMusic(User user)
        {
            var reader = SQL.ReadChannels("music = 1");
            List<ulong> streams = new List<ulong>();
            while (reader.Read())
                streams.Add(Convert.ToUInt64(reader["channel"].ToString()));
            return user.VoiceChannel != null && streams.Contains(user.VoiceChannel.Id);
        }

        internal static bool GetNsfw(Channel chan) => SQL.ReadBool(SQL.ReadChannel(chan.Id, "nsfw"));

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("nsfw status")
                .Alias("canlewd status")
                .Description("I'll tell you if this channel allows nsfw commands.")
                .Do(async e => await e.Channel.SendMessage($"This channel {(GetNsfw(e.Channel) ? "allows" : "doesn't allow")} nsfw commands."));

            // Moderator Commands
            group.CreateCommand("nsfw")
                .Alias("canlewd")
                .Parameter("on/off", Commands.ParameterType.Required)
                .MinPermissions(1)
                .Description("I'll set a channel's nsfw flag to on or off.")
                .Do(async e =>
                {
                    bool on = e.Args[0] == "on";
                    bool off = !on && e.Args[0] == "off";
                    if (on || off)
                    {
                        bool nsfw = GetNsfw(e.Channel);
                        string status = on ? "allow" : "disallow";
                        if (nsfw == on || nsfw != off)
                            await e.Channel.SendMessage($"{e.User.Mention}, this channel is already {status}ing nsfw commands.");
                        else
                        {
                            await SQL.AddOrUpdateFlagAsync(e.Channel.Id, "nsfw", off ? "0" : "1");
                            await e.Channel.SendMessage($"I've set this channel to {status} nsfw commands.");
                        }
                    }
                    else await e.Channel.SendMessage($"{e.User.Mention}, '{string.Join(" ", e.Args)}' isn't a valid argument. Please use on or off instead.");
                });

            // Administrator Commands
            group.CreateCommand("ignore")
                .Parameter("channel", Commands.ParameterType.Optional)
                .Parameter("user", Commands.ParameterType.Optional)
                .Parameter("...", Commands.ParameterType.Multiple)
                .MinPermissions(1)
                .Description("I'll ignore commands coming from a particular channel or user")
                .Do(async e =>
                {
                    if (e.Message.MentionedChannels.Count() > 0 || e.Message.MentionedUsers.Count() > 0)
                    {
                        int perms = Helpers.GetPermissions(e.User, e.Channel);
                        string reply = "";
                        foreach (Channel c in e.Message.MentionedChannels)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("channel", "flags", c.Id, '#', perms);
                        foreach (User u in e.Message.MentionedUsers)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("user", "users", u.Id, '@', perms, Helpers.GetPermissions(u, e.Channel));
                        await e.Channel.SendMessage(reply);
                    }
                    else await e.Channel.SendMessage("You need to mention at least one user or channel!");
                });
        }
    }
}
