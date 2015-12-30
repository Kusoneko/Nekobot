using System;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;

namespace Nekobot
{
    partial class Program
    {
        internal static AudioService Audio => client.Audio();
    }
    class Voice
    {
        internal static AudioService NewService =>
            new AudioService(new AudioServiceConfig
            {
                Mode = AudioMode.Outgoing,
                EnableMultiserver = false,//true,
                EnableEncryption = true,
                Bitrate = 512,
            });

        internal static async Task<DiscordAudioClient> JoinServer(Channel c)
        {
            try { return await Program.Audio.Join(c); }
            catch (Exception e)
            {
                Program.log.Error("Voice", "Join Server Error: " + e.Message);
                return null;
            }
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {

        }
    }
}
