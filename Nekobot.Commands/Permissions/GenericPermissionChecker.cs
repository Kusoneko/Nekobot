using System;
using Discord;

namespace Nekobot.Commands.Permissions
{
    internal class GenericPermissionChecker : IPermissionChecker
    {
        private readonly Func<Command, IUser, IMessageChannel, bool> _checkFunc;
        private readonly string _error;

        public GenericPermissionChecker(Func<Command, IUser, IMessageChannel, bool> checkFunc, string error = null)
        {
            _checkFunc = checkFunc;
            _error = error;
        }

        public bool CanRun(Command command, IUser user, IMessageChannel channel, out string error)
        {
            error = _error;
            return _checkFunc(command, user, channel);
        }
    }
}
