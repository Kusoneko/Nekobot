using System;
using Discord;

namespace Nekobot.Commands
{
    public static class CommandExtensions
    {
        public static DiscordClient UsingCommands(this DiscordClient client, CommandServiceConfig config = null, Func<Channel, bool> getNsfwFlag = null, Func<User, bool> getMusicFlag = null, Func<Channel, User, bool> getIgnoredChannelFlag = null)
        {
            client.Services.Add(new CommandService(config, getNsfwFlag, getMusicFlag, getIgnoredChannelFlag));
            return client;
        }
        public static CommandService Commands(this DiscordClient client, bool required = true)
            => client.Services.Get<CommandService>(required);
    }
}
