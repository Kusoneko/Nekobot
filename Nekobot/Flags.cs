using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Nekobot.Commands.Permissions.Levels;
using Discord.WebSocket;

namespace Nekobot
{
    class Flags
    {
        internal static bool GetIgnored(IChannel chan, IUser user) => GetIgnored(chan) || GetIgnored(user);
        internal static bool GetIgnored(IUser user) => GetIgnored("user", "users", user.Id) || (user is IGuildUser && GetIgnored((user as IGuildUser).RoleIds));
        internal static bool GetIgnored(IChannel chan) => GetIgnored("channel", "flags", chan.Id);
        static bool GetIgnored(string row, string table, ulong id) => SQL.ReadBool(SQL.ReadSingle(row, table, id, "ignored"));
        internal static bool GetIgnored(IEnumerable<ulong> roles)
        {
            var reader = SQL.ReadRoles("ignored=1");
            var ignored = new List<ulong>();
            while (reader.Read())
                ignored.Add(ulong.Parse(reader["role"].ToString()));
            if (ignored.Any())
                foreach(var role in roles)
                    if (ignored.Contains(role)) return true;
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

        internal static bool GetMusic(IVoiceState user) => Music.Get(user.VoiceChannel);

        internal static bool GetNsfw(IChannel chan) => SQL.ReadBool(SQL.ReadChannel(chan.Id, "nsfw"));

        private static string GetAnnounceChan(IGuild s, string id) => SQL.ReadServer(s.Id, id);
        private static bool GetServerAnnounce(IGuild s, string b) => SQL.ReadBool(SQL.ReadServer(s.Id, b), true);
        internal static string GetWelcomeChan(IGuild s) => GetAnnounceChan(s, "welcomechannel");
        internal static bool GetWelcome(IGuild s, IChannel c = null) => GetServerAnnounce(s, "welcome") && (c == null || GetWelcomeChan(s) == c.Id.ToString());
        internal static string GetLeftChan(IGuild s) => GetAnnounceChan(s, "leftchannel");
        internal static bool GetLeft(IGuild s, IChannel c = null) => GetServerAnnounce(s, "sayleft") && (c == null || GetLeftChan(s) == c.Id.ToString());
        internal static IEnumerable<ulong> GetDefaultRoles(IGuild server)
        {
            var arr = SQL.ReadServer(server.Id, "default_roles").Split(',');
            return arr.Length == 1 && arr[0].Length == 0 ? new ulong[0] : arr.Select(id => ulong.Parse(id));
        }
        internal static void SetDefaultRoles<T>(IGuild server, IEnumerable<T> roles) => SQL.AddOrUpdateServer(server.Id, "default_roles", string.Join(",", roles));

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("nsfw status")
                .Alias("canlewd status")
                .Description("I'll tell you if this channel allows nsfw commands.")
                .Do(async e => await e.Channel.SendMessageAsync($"This channel {(GetNsfw(e.Channel) ? "allows" : "doesn't allow")} nsfw commands."));

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
                            e.Channel.SendMessageAsync($"{e.User.Mention}, this channel is already {status}ing nsfw commands.");
                        else
                        {
                            SQL.AddOrUpdateFlag(e.Channel.Id, "nsfw", on ? "1" : "0");
                            e.Channel.SendMessageAsync($"I've set this channel to {status} nsfw commands.");
                        }
                    });
                });

            // TODO: clean up welcome and sayleft to be the same function via strings and lambdas.
            group.CreateCommand("welcome")
                .Parameter("on/off", Commands.ParameterType.Required)
                .Parameter("channel", Commands.ParameterType.Optional)
                .MinPermissions(2)
                .Description("I'll turn welcomes on this server off or on (in a given channel).")
                .Do(e => Helpers.OnOffCmd(e, on =>
                {
                    string status = on ? "en" : "dis";
                    ITextChannel c = (ITextChannel)e.Server.GetChannelAsync(e.Message.MentionedChannelIds.FirstOrDefault()) ?? e.Channel;
                    if (GetWelcome(e.Server, c) == on)
                        e.Channel.SendMessageAsync($"{e.User.Mention}, Welcoming is already {status}abled, here.");
                    else
                    {
                        SQL.AddOrUpdateServer(e.Server.Id, "welcome", on ? "1" : "0");
                        e.Channel.SendMessageAsync($"I will no{(on ? "w" : " longer")} welcome people to this server{(on ? $" in {c.Mention}" : "")}.");
                    }
                }));

            group.CreateCommand("sayleft")
                .Parameter("on/off", Commands.ParameterType.Required)
                .Parameter("channel", Commands.ParameterType.Optional)
                .MinPermissions(2)
                .Description("I'll turn leave announcements on this server off or on (in a given channel).")
                .Do(e => Helpers.OnOffCmd(e, on =>
                {
                    string status = on ? "en" : "dis";
                    ITextChannel c = e.Message.Tags.FirstOrDefault(t => t.Type == TagType.ChannelMention)?.Value as ITextChannel ?? e.Channel;
                    if (GetLeft(e.Server, c) == on)
                        e.Channel.SendMessageAsync($"{e.User.Mention}, Announcing people who leave is already {status}abled, here.");
                    else
                    {
                        SQL.AddOrUpdateServer(e.Server.Id, "sayleft", on ? "1" : "0");
                        SQL.AddOrUpdateServer(e.Server.Id, "leftchannel", on ? c.Id.ToString() : "");
                        e.Channel.SendMessageAsync($"I will no{(on ? "w" : " longer")} announce when people leave this server{(on ? $" in {c.Mention}" : "")}.");
                    }
                }));

            Func<IRole, EMentionType> mention_type = r => r.IsMentionable ? EMentionType.role : EMentionType.unmentionableRole;
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
                    if (e.Message.MentionedChannelIds.Any() || e.Message.MentionedUserIds.Any() || e.Message.MentionedRoleIds.Any())
                    {
                        int perms = Helpers.GetPermissions(e.User, e.Channel);
                        string reply = "";
                        foreach (var c in e.Message.MentionedChannelIds)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("channel", "flags", c, EMentionType.channel, perms);
                        foreach (var u in e.Message.MentionedUserIds)
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("user", "users", u, EMentionType.user, perms, Helpers.GetPermissions(u, e.Channel));
                        var senpai = (SocketGuildUser)await e.Server.GetUserAsync(Program.masterId);
                        foreach (var r in e.Message.MentionedRoleIds)
                        {
                            var role = e.Server.GetRole(r);
                            reply += (reply != "" ? "\n" : "") + await SetIgnored("role", "roles", r, mention_type(role), perms, (senpai != null && senpai.Roles.Contains(role)) ? -2 : (e.User as SocketGuildUser).Roles.Contains(role) ? -1 : perms);
                        }
                        await e.Channel.SendMessageAsync(reply);
                    }
                    else await e.Channel.SendMessageAsync("You need to mention at least one user, channel or role!");
                });

            Action<IEnumerable<ulong>, IGuild> add_roles = (roles,server) => SetDefaultRoles(server, roles.Union(GetDefaultRoles(server)));
            group.CreateCommand("adddefaultroles")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .MinPermissions(3)
                .Description("I'll automatically add anyone who joins the server to the roles you tell me with this command.")
                .Do(async e =>
                {
                    var roles = e.Message.MentionedRoleIds;
                    if (roles.Any())
                    {
                        add_roles(roles, e.Server);
                        await e.Channel.SendMessageAsync("Roles added.");
                    }
                    else await e.Channel.SendMessageAsync("You need to mention at least one role.");
                });

            Action<IEnumerable<ulong>, IGuild> rem_roles = (roles, server) => SetDefaultRoles(server, roles.Except(GetDefaultRoles(server)));
            group.CreateCommand("remdefaultroles")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .MinPermissions(3)
                .Description("I'll remove roles from those automatically assigned to anyone who joins the server.")
                .Do(async e =>
                {
                    var roles = e.Message.MentionedRoleIds;
                    if (roles.Any())
                    {
                        rem_roles(roles, e.Server);
                        await e.Channel.SendMessageAsync("Roles removed.");
                    }
                    else await e.Channel.SendMessageAsync("You need to mention at least one role.");
                });

            Func<Commands.CommandEventArgs, Func<IRole, Task<string>>, Task> rolenames_command = async (e,func) =>
            {
                string reply = "";
                if (e.Args[0].Length == 0)
                    reply = "You need to provide at least one role name!";
                else
                {
                    Helpers.CommaSeparateRoleNames(e, async (roles, str) =>
                    {
                        var count = roles.Count();
                        if (reply != "") reply += '\n';
                        reply += count == 1 ? await func(roles.Single()) : $"{(count == 0 ? "No" : count.ToString())} roles found for {str}";
                    });
                }
                await e.Channel.SendMessageAsync(reply);
            };
            group.CreateCommand("ignore role")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .MinPermissions(3)
                .Description("I'll ignore particular roles by name (comma separated)")
                .Do(async e =>
                {
                    int perms = Helpers.GetPermissions(e.User, e.Channel);
                    var senpai = (SocketGuildUser)await e.Server.GetUserAsync(Program.masterId);
                    await rolenames_command(e, (r) => SetIgnored("role", "roles", r.Id, mention_type(r), perms, (senpai != null && senpai.Roles.Contains(r)) ? -2 : (e.User as SocketGuildUser).Roles.Contains(r) ? -1 : perms));
                });

            group.CreateCommand("adddefaultrolesbyname")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .MinPermissions(3)
                .Description("I'll automatically add anyone who joins the server to these roles (names must be comma separated).")
                .Do(async e =>
                {
                    var roles = new List<IRole>();
                    await rolenames_command(e, (r) =>
                    {
                        roles.Add(r);
                        return Task.FromResult(roles.Count == 1 ? "Adding default role(s)." : string.Empty);
                    });
                    add_roles(roles.Select(r => r.Id), e.Server);
                });

            group.CreateCommand("remdefaultrolesbyname")
                .Parameter("role(s)", Commands.ParameterType.Unparsed)
                .MinPermissions(3)
                .Description("I'll remove roles from those automatically assigned to anyone who joins the server. (names must be comma separated).")
                .Do(async e =>
                {
                    var roles = new List<IRole>();
                    await rolenames_command(e, (r) =>
                    {
                        roles.Add(r);
                        return Task.FromResult(roles.Count == 1 ? "Removing default role(s)." : string.Empty);
                    });
                    rem_roles(roles.Select(r => r.Id), e.Server);
                });
        }
    }
}
