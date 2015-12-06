using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using NAudio.Wave;
using TagLib;
using VideoLibrary;
using Nekobot.Commands.Permissions.Levels;

namespace Nekobot
{
    class Music
    {
        // Music-related variables
        internal static string musicFolder;
        static List<long> streams = new List<long>();
        static Dictionary<long, List<Tuple<string, string, long, string>>> playlist = new Dictionary<long, List<Tuple<string, string, long, string>>>();
        static Dictionary<long, bool> skip = new Dictionary<long, bool>();
        static Dictionary<long, bool> reset = new Dictionary<long, bool>();
        static Dictionary<long, List<long>> voteskip = new Dictionary<long, List<long>>();
        static Dictionary<long, List<long>> votereset = new Dictionary<long, List<long>>();
        static Dictionary<long, List<long>> voteencore = new Dictionary<long, List<long>>();
        static string[] musicexts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac" };

        static async Task Stream(long cid)
        {
            Channel c = Program.client.GetChannel(cid);
            IDiscordVoiceClient _client = null;
            try
            {
                _client = await Program.client.JoinVoiceServer(c);
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
            await Program.client.LeaveVoiceServer(c.Server);
        }

        internal static Task StartStreams()
        {
            return Task.WhenAll(
              streams.Select(s =>
              {
                  if (Program.client.GetChannel(s).Type == "voice")
                      return Task.Run(() => Stream(s));
                  else
                      return null;
              })
              .Where(t => t != null)
              .ToArray());
        }

        internal static void LoadStreams()
        {
            SQLiteDataReader reader = SQL.ExecuteReader("select channel from flags where music = 1");
            while (reader.Read())
                streams.Add(Convert.ToInt64(reader["channel"].ToString()));
        }

        static int CountVoiceChannelMembers(Channel chan)
        {
            if (chan.Type != "voice") return -1;
            return chan.Members.Where(u => u.VoiceChannel == chan).Count();
        }

        internal static string GetTitle(Tuple<string,string,long,string> t)
        {
            if (t.Item2 == "Youtube")
                return t.Item4;
            File song = File.Create(t.Item1);
            if (song.Tag.Title != null && song.Tag.Title != "")
            {
                string title = "";
                if (song.Tag.Performers != null)
                    foreach (string p in song.Tag.Performers)
                        title += $", {p}";
                if (title != "")
                    title = title.Substring(2) + " **-** ";
                return title + song.Tag.Title;
            }
            return System.IO.Path.GetFileNameWithoutExtension(t.Item1);
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("playlist")
                .Description("I'll give you the list of songs in the playlist.")
                .FlagMusic(true)
                .Do(async e =>
                {
                    string reply = $"Currently playing: {GetTitle(playlist[e.User.VoiceChannel.Id][0])}.\nNext songs:";
                    for(int i = 1; i < 11; i++)
                    {
                        var t = playlist[e.User.VoiceChannel.Id][i];
                        string ext = "";
                        bool youtube = t.Item2 == "Youtube";
                        if (t.Item2 == "Request" || youtube)
                            ext = $"{(!youtube ? " request" : "")} by <@{t.Item3}>";
                        string title = GetTitle(t);
                        if (youtube) title = $"**{title}**";
                        reply += $"\n{i} - **[{t.Item2}{ext}]** {title}";
                    }
                    await Program.client.SendMessage(e.Channel, reply);
                });

            group.CreateCommand("song")
                .Description("I'll tell you the song I'm currently playing.")
                .FlagMusic(true)
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, $"Currently playing: {GetTitle(playlist[e.User.VoiceChannel.Id][0])}.");
                });

            group.CreateCommand("ytrequest")
                .Parameter("youtube video link", Commands.ParameterType.Required)
                .Description("I'll add a youtube video to the playlist")
                .FlagMusic(true)
                .Do(async e =>
                {
                    Regex re = new Regex(@"(?:https?:\/\/)?(?:youtu\.be\/|(?:www\.)?youtube\.com\/watch(?:\.php)?\?.*v=)([a-zA-Z0-9\-_]+)");
                    if (re.IsMatch(e.Args[0]))
                    {
                        var youtube = YouTube.Default;
                        var video = await youtube.GetVideoAsync(e.Args[0]);
                        //if (video.FileExtension != ".webm")
                        //{
                            int index = 1;
                            for (int i = 1; i < playlist[e.User.VoiceChannel.Id].Count; i++)
                            {
                                if (playlist[e.User.VoiceChannel.Id][i].Item2 == "Encore" || playlist[e.User.VoiceChannel.Id][i].Item2 == "Request" || playlist[e.User.VoiceChannel.Id][i].Item2 == "Youtube")
                                {
                                    index++;
                                }
                            }
                            bool isAlreadyInPlaylist = false;
                            int songindex = 1;
                            for (int z = 1; z < playlist[e.User.VoiceChannel.Id].Count; z++)
                            {
                                if (playlist[e.User.VoiceChannel.Id][z].Item1 == video.Uri)
                                {
                                    isAlreadyInPlaylist = true;
                                    songindex = z;
                                }
                            }
                            if (isAlreadyInPlaylist)
                            {
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist at position {songindex}.");
                                return;
                            }
                            playlist[e.User.VoiceChannel.Id].Insert(index, Tuple.Create<string, string, long, string>(video.Uri, "Youtube", e.User.Id, e.Args[0]));
                            await Program.client.SendMessage(e.Channel, $"{video.Title} added to the playlist.");
                        //}
                        //else
                        //{
                        //    await Program.client.SendMessage(e.Channel, $"{video.Title} couldn't be added to the playlist because of unsupported fileformat: {video.FileExtension}.");
                        //}
                    }
                    else
                    {
                        await Program.client.SendMessage(e.Channel, $"{e.Args[0]} couldn't be added to playlist because it's not a valid youtube link.");
                    }
                });

            group.CreateCommand("request")
                .Parameter("song to find", Commands.ParameterType.Required)
                .Parameter("...", Commands.ParameterType.Multiple)
                .Description("I'll try to add your request to the playlist!")
                .FlagMusic(true)
                .Do(async e =>
                {
                    bool requestfound = false;
                    var files = from file in System.IO.Directory.EnumerateFiles($"{musicFolder}", "*.*").Where(s => musicexts.Contains(System.IO.Path.GetExtension(s))) select new { File = file };
                    for (int j = 0; j < files.Count(); j++)
                    {
                        if (System.IO.Path.GetFileNameWithoutExtension(files.ElementAt(j).File).ToLower().Contains(String.Join(" ", e.Args).ToLower()))
                        {
                            int index = 1;
                            for (int i = 1; i < playlist[e.User.VoiceChannel.Id].Count; i++)
                            {
                                if (playlist[e.User.VoiceChannel.Id][i].Item2 == "Encore" || playlist[e.User.VoiceChannel.Id][i].Item2 == "Request" || playlist[e.User.VoiceChannel.Id][i].Item2 == "Youtube")
                                {
                                    index++;
                                }
                            }
                            bool isAlreadyInPlaylist = false;
                            int songindex = 1;
                            for (int z = 1; z < playlist[e.User.VoiceChannel.Id].Count; z++)
                            {
                                if (playlist[e.User.VoiceChannel.Id][z].Item1 == files.ElementAt(j).File)
                                {
                                    isAlreadyInPlaylist = true;
                                    songindex = z;
                                }
                            }
                            if (isAlreadyInPlaylist)
                            {
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist at position {songindex}.");
                                return;
                            }
                            playlist[e.User.VoiceChannel.Id].Insert(index, Tuple.Create<string, string, long, string>(files.ElementAt(j).File, "Request", e.User.Id, null));
                            await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request has been added to the list.");
                            requestfound = true;
                            break;
                        }
                    }
                    if (!requestfound)
                    {
                        await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request was not found.");
                    }
                });

            group.CreateCommand("skip")
                .Description("Vote to skip the current song. (Will skip at 50% or more)")
                .FlagMusic(true)
                .Do(async e =>
                {
                    if (!voteskip[e.User.VoiceChannel.Id].Contains(e.User.Id))
                    {
                        voteskip[e.User.VoiceChannel.Id].Add(e.User.Id);
                        if (voteskip[e.User.VoiceChannel.Id].Count >= Math.Ceiling((decimal)CountVoiceChannelMembers(e.User.VoiceChannel) / 2))
                        {
                            await Program.client.SendMessage(e.Channel, $"{voteskip[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to skip current song. 50%+ achieved, skipping song...");
                            skip[e.User.VoiceChannel.Id] = true;
                        }
                        else
                        {
                            await Program.client.SendMessage(e.Channel, $"{voteskip[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to skip current song. (Needs 50% or more to skip)");
                        }
                    }
                });

            group.CreateCommand("reset")
                .Description("Vote to reset the stream. (Will reset at 50% or more)")
                .FlagMusic(true)
                .Do(async e =>
                {
                    if (!votereset[e.User.VoiceChannel.Id].Contains(e.User.Id))
                    {
                        votereset[e.User.VoiceChannel.Id].Add(e.User.Id);
                        if (votereset[e.User.VoiceChannel.Id].Count >= Math.Ceiling((decimal)CountVoiceChannelMembers(e.User.VoiceChannel) / 2))
                        {
                            await Program.client.SendMessage(e.Channel, $"{votereset[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to reset the stream. 50%+ achieved, resetting stream...");
                            reset[e.User.VoiceChannel.Id] = true;
                            await Task.Delay(5000);
                            await Stream(e.User.VoiceChannel.Id);
                        }
                        else
                        {
                            await Program.client.SendMessage(e.Channel, $"{votereset[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to reset the stream. (Needs 50% or more to reset)");
                        }
                    }
                });

            group.CreateCommand("encore")
                .Alias("replay")
                .Alias("ankoru")
                .Description("Vote to replay the current song. (Will replay at 50% or more)")
                .FlagMusic(true)
                .Do(async e =>
                {
                    if (!voteencore[e.User.VoiceChannel.Id].Contains(e.User.Id))
                    {
                        voteencore[e.User.VoiceChannel.Id].Add(e.User.Id);
                        if (voteencore[e.User.VoiceChannel.Id].Count >= Math.Ceiling((decimal)CountVoiceChannelMembers(e.User.VoiceChannel) / 2))
                        {
                            await Program.client.SendMessage(e.Channel, $"{voteencore[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to replay current song. 50%+ achieved, song will be replayed...");
                            playlist[e.User.VoiceChannel.Id].Insert(1, Tuple.Create(playlist[e.User.VoiceChannel.Id][0].Item1, "Encore", playlist[e.User.VoiceChannel.Id][0].Item3, playlist[e.User.VoiceChannel.Id][0].Item4));
                        }
                        else
                        {
                            await Program.client.SendMessage(e.Channel, $"{voteencore[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to replay current song. (Needs 50% or more to replay)");
                        }
                    }
                });

            // Moderator commands
            group.CreateCommand("forceskip")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll skip the currently playing song.")
                .Do(async e =>
                {
                    skip[e.User.VoiceChannel.Id] = true;
                    await Program.client.SendMessage(e.Channel, "Forcefully skipping song...");
                });

            group.CreateCommand("forcereset")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll reset the stream in case of bugs, while keeping the playlist intact.")
                .Do(async e =>
                {
                    reset[e.User.VoiceChannel.Id] = true;
                    await Program.client.SendMessage(e.Channel, "Reseting stream...");
                    await Task.Delay(5000);
                    await Stream(e.User.VoiceChannel.Id);
                });

            // Administrator commands
            group.CreateCommand("music")
                .Parameter("on/off", Commands.ParameterType.Required)
                .Description("I'll start or end a stream in a particular voice channel, which you need to be in.")
                .MinPermissions(2)
                .Do(async e =>
                {
                    if (e.User.VoiceChannel?.Id <= 0)
                        await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, you need to be in a voice channel to use this.");
                    else
                    {
                        bool on = e.Args[0] == "on";
                        bool off = !on && e.Args[0] == "off";
                        if (on || off)
                        {
                            bool has_stream = streams.Contains(e.User.VoiceChannel.Id);
                            string status = on ? "start" : "halt";
                            if (has_stream == on || has_stream != off)
                            {
                                string blah = on ? "streaming in! Did you mean to !reset or !forcereset the stream?" : "not streaming in!";
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, I can't {status} streaming in a channel that I'm already {blah}");
                            }
                            else
                            {
                                streams.Add(e.User.VoiceChannel.Id);
                                SQL.ExecuteNonQuery(off ? $"update flags set music=0 where channel='{e.User.VoiceChannel.Id}'"
                                    : SQL.ExecuteScalarPos($"select count(channel) from flags where channel = '{e.User.VoiceChannel.Id}'")
                                    ? $"update flags set music=1 where channel='{e.User.VoiceChannel.Id}'"
                                    : $"insert into flags values ('{e.User.VoiceChannel.Id}', 0, 1, 0, -1)");
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, I'm {status}ing the stream!");
                                await Stream(e.User.VoiceChannel.Id);
                            }
                        }
                        else await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, the argument needs to be either on or off.");
                    }
                });
        }
    }
}
