using System;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;

namespace Nekobot
{
    partial class Program
    {
        internal static AudioService Audio => client.GetService<AudioService>();
    }
    class Voice
    {
        internal static void Startup(DiscordClient c)
        {
            c.UsingAudio(x =>
            {
                x.Mode = AudioMode.Outgoing;
                x.EnableMultiserver = false; // So like... this is broken again.
                x.EnableEncryption = true;
            });

            Music.Load(c);
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
