using System.Collections.Generic;
using Discord;

namespace Nekobot.Commands.Permissions.Userlist
{
    public class BlacklistService : UserlistService
    {
        public BlacklistService(IEnumerable<ulong> initialList = null)
            : base(initialList)
        {
        }

        public bool CanRun(User user)
        {
            return !_userList.ContainsKey(user.Id);
        }
    }
}
