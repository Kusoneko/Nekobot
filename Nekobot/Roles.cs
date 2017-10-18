using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nekobot.Commands;
using Newtonsoft.Json.Linq;
using Discord;
using Discord.WebSocket;

namespace Nekobot
{
    public static class Roles
    {
        internal static JToken RolesToken => Program.config["roles"];

        private static async Task PerformNotifyRemove(CommandEventArgs args, Func<SocketGuildUser, IEnumerable<IRole>, Task> perform, IEnumerable<IRole> roles, string verbed)
        {
            await Task.Factory.StartNew(async () =>
            {
                await perform(args.User as SocketGuildUser, roles);
                var message = await Helpers.SendEmbed(args, Helpers.EmbedBuilder.WithDescription($"I {verbed} role(s): {string.Join(", ", roles.Select(r => r.Mention))}"));
                await Task.Delay(4000);
                await args.TextChannel.DeleteMessagesAsync(new[] { message, args.Message });
            });
        }

        public static async Task ListRolesAsync(CommandEventArgs args)
        {
            var builder = Helpers.EmbedBuilder.WithTitle("The roles you can use with `giverole` and `removerole` are as follows:");
            var str = string.Empty;
            IterateRolesDo(args, role =>
            {
                var desc = role.Value["description"].ToString();
                str += $"{Format.Bold(role.Key)}{(desc.Length != 0 ? $": {desc}" : string.Empty)}\n";
            });
            await Helpers.SendEmbed(args, builder.WithDescription(str));
        }

        public static void IterateRolesDo(CommandEventArgs args, Action<KeyValuePair<string, JToken>> action)
        {
            var serverid = args.Server.Id.ToString();
            int perms = Helpers.GetPermissions(args.User, args.Channel);
            foreach (var role in RolesToken[serverid].ToObject<JObject>())
                if (Helpers.FieldExistsSafe<int>(role.Value, "permissions") <= perms)
                    action(role);
        }

        private static bool Check(Command cmd, IUser user, IMessageChannel chan)
            => chan is SocketGuildChannel && Helpers.FieldExists(RolesToken, (chan as SocketGuildChannel).Guild.Id.ToString());

        internal static void AddCommands(CommandGroupBuilder group)
        {
            if (!RolesToken.HasValues) return;
            group.CreateCommand("listroles")
                .Description("I'll list the roles you can use with `giverole` and `removerole`")
                .AddCheck(Check)
                .Do(args => ListRolesAsync(args));

            Action<CommandBuilder, string, string, Func<SocketGuildUser, IEnumerable<IRole>, Task>> AddRemove = (cmd, verb, verbed, perform) =>
                cmd.Parameter("role (or roles, comma separated)", ParameterType.Unparsed)
                    .AddCheck(Check)
                    .Description($"I'll {verb} you the roles you request (see `listroles`)")
                    .Do(async args =>
                    {
                        var keys = args.Args[0].Split(',').Select(k => k.Trim().ToLower());
                        var roles = new List<IRole>();
                        IterateRolesDo(args, role =>
                        {
                            if (keys.Any(key => key.Equals(role.Key.ToLower())))
                                roles.Add(args.Server.GetRole(role.Value["id"].ToObject<ulong>()));
                        });
                        await PerformNotifyRemove(args, perform, roles, verbed);
                    });

            AddRemove(group.CreateCommand("giverole")
                .Alias("addrole"), "grant", "Granted", (user, roles) => user.AddRolesAsync(roles));

            AddRemove(group.CreateCommand("removerole"), "remove", "Removed", (user, roles) => user.RemoveRolesAsync(roles));
        }
    }
}
