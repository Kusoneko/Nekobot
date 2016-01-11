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
        internal static void Startup(DiscordClient c)
        {
            c.UsingAudio(new AudioServiceConfig
            {
                Mode = AudioMode.Outgoing,
                EnableMultiserver = false,//true,
                EnableEncryption = true,
                Bitrate = 512,
            });

            // Load the stream channels
            Music.LoadStreams();
        }

        internal static async Task<IAudioClient> JoinServer(Channel c)
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
