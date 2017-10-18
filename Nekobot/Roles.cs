using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nekobot.Commands;
using Newtonsoft.Json.Linq;
using Discord;

namespace Nekobot
{
    public static class Roles
    {
        internal static JToken RolesToken => Program.config["roles"];

        private static async Task PerformNotifyRemove(CommandEventArgs args, Func<User, IEnumerable<Role>, Task> perform, IEnumerable<Role> roles, string verbed)
        {
            await Task.Factory.StartNew(async () =>
            {
                await perform(args.User, roles);
                var message = await args.Channel.SendMessage($"I {verbed} role(s): {string.Join(", ", roles.Select(r => r.Mention))}");
                await Task.Delay(4000);
                await args.Channel.DeleteMessages(new[] { message, args.Message });
            });
        }

        public static async Task ListRolesAsync(CommandEventArgs args)
        {
            var str = Format.Underline("The roles you can use with `giverole` and `removerole` are as follows:\n");
            IterateRolesDo(args, role =>
            {
                var desc = role.Value["description"].ToString();
                str += $"{Format.Bold(role.Key)}{(desc.Length != 0 ? $": {desc}" : string.Empty)}\n";
            });
            await args.Channel.SendMessage(str);
        }

        public static void IterateRolesDo(CommandEventArgs args, Action<KeyValuePair<string, JToken>> action)
        {
            var serverid = args.Server.Id.ToString();
            int perms = Helpers.GetPermissions(args.User, args.Channel);
            foreach (var role in RolesToken[serverid].ToObject<JObject>())
                if (Helpers.FieldExistsSafe<int>(role.Value, "permissions") <= perms)
                    action(role);
        }

        private static bool Check(Command cmd, User user, Channel chan)
            => chan.Server != null && Helpers.FieldExists(RolesToken, chan.Server.Id.ToString());

        internal static void AddCommands(CommandGroupBuilder group)
        {
            if (!RolesToken.HasValues) return;
            group.CreateCommand("listroles")
                .Description("I'll list the roles you can use with `giverole` and `removerole`")
                .AddCheck(Check)
                .Do(args => ListRolesAsync(args));

            Action<CommandBuilder, string, string, Func<User, IEnumerable<Role>, Task>> AddRemove = (cmd, verb, verbed, perform) =>
                cmd.Parameter("role (or roles, only comma separated)", ParameterType.MultipleUnparsed)
                    .AddCheck(Check)
                    .Description($"I'll {verb} you the roles you request (see `listroles`)")
                    .Do(async args =>
                    {
                        var keys = args.Args[0].Split(',');
                        var roles = new List<Role>();
                        IterateRolesDo(args, role =>
                        {
                            if (keys.Any(key => key.Equals(role.Key, StringComparison.CurrentCultureIgnoreCase)))
                                roles.Add(args.Server.GetRole(role.Value["id"].ToObject<ulong>()));
                        });
                        await PerformNotifyRemove(args, perform, roles, verbed);
                    });

            AddRemove(group.CreateCommand("giverole")
                .Alias("addrole"), "grant", "Granted", (user, roles) => user.AddRoles(roles.ToArray()));

            AddRemove(group.CreateCommand("removerole"), "remove", "Removed", (user, roles) => user.RemoveRoles(roles.ToArray()));
        }
    }
}
