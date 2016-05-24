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
        internal static bool GetIgnored(User user) => GetIgnored("user", "users", user.Id) || (user.Server != null && GetIgnored(user.Roles));
        internal static bool GetIgnored(Channel chan) => GetIgnored("channel", "flags", chan.Id);
        static bool GetIgnored(string row, string table, ulong id) => SQL.ReadBool(SQL.ReadSingle(row, table, id, "ignored"));
        internal static bool GetIgnored(IEnumerable<Role> roles)
        {
            var reader = SQL.ReadRoles("ignored=1");
            var ignored = new List<ulong>();
            while (reader.Read())
                ignored.Add(ulong.Parse(reader["role"].ToString()));
            if (ignored.Any())
                foreach(var role in roles)
                    if (ignored.Contains(role.Id)) return true;
            return false;
        }

        internal enum EMentionType
        {
            user,
            channel,
            role,
            everyoneRole,
            unmentionableRole
        }
        static readonly Dictionary<EMentionType, string> mention_syms = new Dictionary<EMentionType, string>
        {
            { EMentionType.user, "@" },
            { EMentionType.channel, "#" },
            { EMentionType.role, "@&" },
            { EMentionType.unmentionableRole, "" }
        };
        internal static async Task<string> SetIgnored(string row, string table, ulong id, EMentionType mention_type, int perms, int their_perms = 0)
        {
            if (mention_type == EMentionType.everyoneRole) return "I can't ignore everyone!";
            if (mention_type == EMentionType.role || mention_type == EMentionType.unmentionableRole)
            {
                if (their_perms == -1)
                    return $"You cannot change the ignored status of your own roles.";
                if (their_perms == -2)
                    return $"I shall not ignore the roles of my senpai!";
            }
            if (mention_type != EMentionType.user && perms <3)
                return $"You are not worthy of changing {mention_type} ignored status (permissions < 3).";
            if (mention_type == EMentionType.user)
            {
                if (id == Program.masterId)
                    return $"<@{id}> is my senpai and shall not be ignored!";
                if (perms <= their_perms)
                    return $"You are no more powerful than <@{id}>.";
            }
            bool in_table = SQL.InTable(row, table, id);
            bool isIgnored = in_table && GetIgnored(row, table, id);
            await SQL.ExecuteNonQueryAsync(SQL.AddOrUpdateCommand(row, table, id, "ignored", Convert.ToInt32(!isIgnored).ToString(), in_table));
            return $"<{mention_syms[mention_type]}{id}> is " + (!isIgnored ? "now" : "no longer") + " ignored.";
        }

        internal static bool GetMusic(User user) => Music.Get(user.VoiceChannel);

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
                .Do(e =>
                {
                    Helpers.OnOffCmd(e, on =>
                    {
                        string status = on ? "allow" : "disallow";
                        if (GetNsfw(e.Channel) == on)
                            e.Channel.SendMessage($"{e.User.Mention}, this channel is already {status}ing nsfw commands.");
                        else
                        {
                            SQL.AddOrUpdateFlag(e.Channel.Id, "nsfw", on ? "1" : "0");
                            e.Channel.SendMessage($"I've set this channel to {status} nsfw commands.");
                        }
                    });
                });

            Func<Role, EMentionType> mention_type = r => r.IsEveryone ? EMentionType.everyoneRole : r.IsMentionable ? EMentionType.role : EMentionType.unmentionableRole;
            // Administrator Commands
            group.CreateCommand("ignore")
                .Parameter("channel", Commands.ParameterType.Optional)
                .Parameter("user", Commands.ParameterType.Optional)
                .Parameter("role", Commands.ParameterType.Optional)
                .Parameter("...", Commands.ParameterType.Multiple)
                .MinPermissions(1)
                .Description("I'll ignore a particular channel, user or role")
                .Do(async e =>
                {
                    if (e.Message.MentionedChannels.Any() || e.Message.MentionedUsers.Any() || e.Message.MentionedRoles.Any())
                    {
                        int perms = Helpers.GetPermissions(e.User, e.Channel);
                        string reply = "";
                        foreach (Channel c in e.Message.MentionedChannels)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("channel", "flags", c.Id, EMentionType.channel, perms);
                        foreach (User u in e.Message.MentionedUsers)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("user", "users", u.Id, EMentionType.user, perms, Helpers.GetPermissions(u, e.Channel));
                        var senpai = e.Server.GetUser(Program.masterId);
                        foreach (Role r in e.Message.MentionedRoles)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("role", "roles", r.Id, mention_type(r), perms, senpai.Roles.Contains(r) ? -2 : e.User.Roles.Contains(r) ? -1 : perms);
                        await e.Channel.SendMessage(reply);
                    }
                    else await e.Channel.SendMessage("You need to mention at least one user, channel or role!");
                });

            group.CreateCommand("ignore role")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .MinPermissions(3)
                .Description("I'll ignore particular roles by name (comma separated)")
                .Do(async e =>
                {
                    string reply = "";
                    if (e.Args[0].Length == 0)
                        reply = "You need to provide at least one role name!";
                    else
                    {
                        int perms = Helpers.GetPermissions(e.User, e.Channel);
                        var senpai = e.Server.GetUser(Program.masterId);
                        Helpers.CommaSeparateRoleNames(e, async (roles, str) =>
                        {
                            var count = roles.Count();
                            if (reply != "") reply += '\n';
                            if (count != 1)
                                reply += $"{(count == 0 ? "No" : count.ToString())} roles found for {str}";
                            else
                            {
                                var r = roles.Single();
                                reply += await SetIgnored("role", "roles", r.Id, mention_type(r), perms, senpai.Roles.Contains(r) ? -2 : e.User.Roles.Contains(r) ? -1 : perms);
                            }
                        });
                    }
                    await e.Channel.SendMessage(reply);
                });
        }
    }
}
