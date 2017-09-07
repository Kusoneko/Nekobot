using System;
using Discord;

namespace Nekobot.Commands.Permissions.Levels
{
    public class PermissionLevelService
    {
        private readonly Func<IUser, IMessageChannel, int> _getPermissionsFunc;

        private IDiscordClient _client;
        public IDiscordClient Client => _client;

        public PermissionLevelService(Func<IUser, IMessageChannel, int> getPermissionsFunc)
        {
            _getPermissionsFunc = getPermissionsFunc;
        }

        public void Install(IDiscordClient client)
        {
            _client = client;
        }
        public int GetPermissionLevel(IUser user, IMessageChannel channel) => _getPermissionsFunc(user, channel);
    }
}
