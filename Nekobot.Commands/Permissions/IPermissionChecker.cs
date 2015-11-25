using Discord;

namespace Nekobot.Commands.Permissions
{
    public interface IPermissionChecker
    {
        bool CanRun(Command command, User user, Channel channel, out string error);
    }
}
