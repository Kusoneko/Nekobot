using Discord;
using Discord.WebSocket;
using Nekobot.Commands;
using System.Threading.Tasks;

namespace Nekobot
{
    public static class Extensions
    {
        public async static Task ReplyError(this DiscordSocketClient client, CommandEventArgs e, string text)
        {
            if (text != null)
                await e.Channel.SendMessageAsync($"Error: {(!(e.Channel is IPrivateChannel) ? $"{e.User.Username}: " : "")}{text}");
        }
    }
}
