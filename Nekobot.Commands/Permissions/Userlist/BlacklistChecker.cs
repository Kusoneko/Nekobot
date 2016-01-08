﻿using Discord;
namespace Nekobot.Commands.Permissions.Userlist
{
    public class BlacklistChecker : IPermissionChecker
    {
        private readonly BlacklistService _service;

        internal BlacklistChecker(DiscordClient client)
        {
            _service = client.Services.Get<BlacklistService>(true);
        }

        public bool CanRun(Command command, User user, Channel channel, out string error)
        {
            error = null; //Use default error text.
            return _service.CanRun(user);
        }
    }
}
