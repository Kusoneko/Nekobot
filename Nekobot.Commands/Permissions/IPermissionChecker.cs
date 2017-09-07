using Discord;

namespace Nekobot.Commands.Permissions
{
    public interface IPermissionChecker
    {
        bool CanRun(Command command, IUser user, IMessageChannel channel, out string error);
    }
}
