using System;
using System.Threading.Tasks;
using Discord;

namespace Nekobot
{
    class Voice
    {
        internal static async Task<Discord.Audio.IDiscordVoiceClient> JoinServer(Channel c)
        {
            try { return await Program.client.JoinVoiceServer(c); }
            catch (Exception e)
            {
                Console.WriteLine("Join Voice Server Error: " + e.Message);
                return null;
            }
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {

        }
    }
}
