using Discord;
using System;

namespace Nekobot.Commands.Permissions.Levels
{
    public class PermissionLevelChecker : IPermissionChecker
    {
        private readonly PermissionLevelService _service;
        private readonly int _minPermissions;

        public PermissionLevelService Service;
        public int MinPermissions => _minPermissions;

        internal PermissionLevelChecker(PermissionLevelService permsservice, int minPermissions)
        {
            _service = permsservice;
            _minPermissions = minPermissions;
        }

        public bool CanRun(Command command, IUser user, IMessageChannel channel, out string error)
        {
            error = null; //Use default error text.
            int permissions = _service.GetPermissionLevel(user, channel);
            return permissions >= _minPermissions;
        }
    }
}
