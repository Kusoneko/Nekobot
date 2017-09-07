using System;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace Nekobot
{
    class Voice
    {
        internal static async Task Startup(DiscordSocketClient c)
        {
            await Music.Load(c);
        }

        internal static async Task<IAudioClient> JoinServer(IVoiceChannel c)
        {
            try { return await c.ConnectAsync(); }
            catch (Exception e)
            {
                await Log.Write(LogSeverity.Error, "Join Server Error", e, "Voice");
                return null;
            }
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {

        }
    }
}
