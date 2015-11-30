using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using NAudio.Wave;
using Discord.Audio;

namespace Nekobot
{
    partial class Program
    {
        // Music-related variables
        static string musicFolder;
        static List<long> streams = new List<long>();
        public static Dictionary<long, List<Tuple<string, string, long, string>>> playlist = new Dictionary<long, List<Tuple<string, string, long, string>>>();
        public static Dictionary<long, bool> skip = new Dictionary<long, bool>();
        public static Dictionary<long, bool> reset = new Dictionary<long, bool>();
        public static Dictionary<long, List<long>> voteskip = new Dictionary<long, List<long>>();
        public static Dictionary<long, List<long>> votereset = new Dictionary<long, List<long>>();
        public static Dictionary<long, List<long>> voteencore = new Dictionary<long, List<long>>();
        public static string[] musicexts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac" };

        private static async Task StreamMusic(long cid)
        {
            Channel c = client.GetChannel(cid);
            IDiscordVoiceClient _client = null;
            try
            {
                _client = await client.JoinVoiceServer(c);
            }
            catch (Exception e)
            {
                Console.WriteLine("Join Voice Server Error: " + e.Message);
                return;
            }
            Random rnd = new Random();
            if (!playlist.ContainsKey(cid))
                playlist.Add(cid, new List<Tuple<string, string, long, string>>());
            if (!skip.ContainsKey(cid))
                skip.Add(cid, false);
            if (!reset.ContainsKey(cid))
                reset.Add(cid, false);
            while (streams.Contains(cid))
            {
                voteskip[cid] = new List<long>();
                votereset[cid] = new List<long>();
                voteencore[cid] = new List<long>();
                var files = from file in System.IO.Directory.EnumerateFiles(musicFolder, "*.*").Where(s => musicexts.Contains(System.IO.Path.GetExtension(s))) select new { File = file };
                int mp3 = 0;
                while (playlist[cid].Count() < 11)
                {
                    mp3 = rnd.Next(0, files.Count());
                    bool isAlreadyInPlaylist = false;
                    for (int i = 0; !isAlreadyInPlaylist && i < playlist[cid].Count; i++)
                        if (playlist[cid][i].Item1 == files.ElementAt(mp3).File)
                            isAlreadyInPlaylist = true;
                    if (isAlreadyInPlaylist)
                        break;
                    playlist[cid].Add(Tuple.Create<string, string, long, string>(files.ElementAt(mp3).File, "Playlist", 0, null));
                }
                await Task.Run(async () =>
                {
                    try
                    {
                        var outFormat = new WaveFormat(48000, 16, 1);
                        int blockSize = outFormat.AverageBytesPerSecond; // 1 second
                        byte[] buffer = new byte[blockSize];
                        using (var musicReader = new MediaFoundationReader(playlist[cid][0].Item1))
                        using (var resampler = new MediaFoundationResampler(musicReader, outFormat) { ResamplerQuality = 60 })
                        {
                            int byteCount;
                            while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                            {
                                if (!streams.Contains(cid) || skip[cid] || reset[cid])
                                {
                                    _client.ClearVoicePCM();
                                    await Task.Delay(1000);
                                    break;
                                }
                                _client.SendVoicePCM(buffer, blockSize);
                            }
                        }
                    }
                    catch (OperationCanceledException err) { Console.WriteLine(err.Message); }
                });
                await _client.WaitVoice(); // Prevent endless queueing which would eventually eat up all the ram
                skip[cid] = false;
                if (reset[cid])
                {
                    reset[cid] = false;
                    break;
                }
                playlist[cid].RemoveAt(0);
            }
            await client.LeaveVoiceServer(c.Server);
        }

        private static Task StartMusicStreams()
        {
            return Task.WhenAll(
              streams.Select(s =>
              {
                  if (client.GetChannel(s).Type == "voice")
                      return Task.Run(() => StreamMusic(s));
                  else
                      return null;
              })
              .Where(t => t != null)
              .ToArray());
        }

        private static void LoadStreams()
        {
            SQLiteDataReader reader = ExecuteReader("select channel from flags where music = 1");
            while (reader.Read())
                streams.Add(Convert.ToInt64(reader["channel"].ToString()));
        }

        private static int CountVoiceChannelMembers(Channel chan)
        {
            if (chan.Type != "voice") return -1;
            return chan.Members.Where(u => u.VoiceChannel == chan).Count();
        }
    }
}
