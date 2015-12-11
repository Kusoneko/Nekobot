using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using NAudio.Wave;
using TagLib;
using VideoLibrary;
using Nekobot.Commands.Permissions.Levels;

namespace Nekobot
{
    class Music
    {
        class Song : Tuple<string, string, long, string>
        {
            internal Song(string uri, string type, long requester = 0, string ext = null) : base(uri, type, requester, ext) { }
            internal Song Encore(long requester) => new Song(Item1, "Encore", requester, Item4);
        }
        // Music-related variables
        internal static string Folder;
        internal static bool UseSubdirs;
        static List<long> streams = new List<long>();
        static Dictionary<long, List<Song>> playlist = new Dictionary<long, List<Song>>();
        static Dictionary<long, bool> skip = new Dictionary<long, bool>();
        static Dictionary<long, bool> reset = new Dictionary<long, bool>();
        static Dictionary<long, List<long>> voteskip = new Dictionary<long, List<long>>();
        static Dictionary<long, List<long>> votereset = new Dictionary<long, List<long>>();
        static Dictionary<long, List<long>> voteencore = new Dictionary<long, List<long>>();
        static string[] exts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac", ".ogg" };

        internal static IEnumerable<string> Files() => System.IO.Directory.EnumerateFiles(Folder, "*.*", UseSubdirs ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly).Where(s => exts.Contains(System.IO.Path.GetExtension(s)));
        static bool InPlaylist(List<Song> playlist, string file) => playlist.Where(song => song.Item1 == file).Count() != 0;
        static int NonrequestedIndex(Commands.CommandEventArgs e) => 1 + playlist[e.User.VoiceChannel.Id].Where(song => song.Item2 == "Encore" || song.Item2 == "Request" || song.Item2 == "Youtube").Count();

        static async Task Stream(long cid)
        {
            Channel c = Program.client.GetChannel(cid);
            Discord.Audio.IDiscordVoiceClient _client = await Voice.JoinServer(c);
            Random rnd = new Random();
            if (!playlist.ContainsKey(cid))
                playlist.Add(cid, new List<Song>());
            if (!skip.ContainsKey(cid))
                skip.Add(cid, false);
            if (!reset.ContainsKey(cid))
                reset.Add(cid, false);
            while (streams.Contains(cid))
            {
                voteskip[cid] = new List<long>();
                votereset[cid] = new List<long>();
                voteencore[cid] = new List<long>();
                var files = Files();
                var filecount = files.Count();
                while (playlist[cid].Count() < (filecount < 11 ? filecount : 11))
                {
                    var mp3 = files.ElementAt(rnd.Next(0, filecount));
                    if (InPlaylist(playlist[cid], mp3))
                        continue;
                    playlist[cid].Add(new Song(mp3, "Playlist"));
                }
                await Task.Run(async () =>
                {
                    try
                    {
                        var outFormat = new WaveFormat(48000, 16, 1);
                        int blockSize = outFormat.AverageBytesPerSecond; // 1 second
                        byte[] buffer = new byte[blockSize];
                        string file = playlist[cid][0].Item1;
                        var musicReader = System.IO.Path.GetExtension(file) == ".ogg" ? (IWaveProvider)new NAudio.Vorbis.VorbisWaveReader(file) : new MediaFoundationReader(file);
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

        static async Task ResetStream(long channel)
        {
            reset[channel] = true;
            await Task.Delay(5000);
            await Stream(channel);
        }

        static int CountVoiceChannelMembers(Channel chan)
        {
            if (chan.Type != "voice") return -1;
            return chan.Members.Where(u => u.VoiceChannel == chan).Count()-1;
        }

        static async Task<bool> AddVote(Dictionary<long, List<long>> votes, Commands.CommandEventArgs e, string action, string success, string actionshort)
        {
            var vote = votes[e.User.VoiceChannel.Id];
            if (!vote.Contains(e.User.Id))
            {
                vote.Add(e.User.Id);
                var listeners = CountVoiceChannelMembers(e.User.VoiceChannel);
                if (vote.Count >= Math.Ceiling((decimal)listeners / 2))
                {
                    await Program.client.SendMessage(e.Channel, $"{vote.Count}/{listeners} votes to {action}. 50%+ achieved, {success}...");
                    return true;
                }
                await Program.client.SendMessage(e.Channel, $"{vote.Count}/{listeners} votes to {action}. (Needs 50% or more to {actionshort})");
            }
            return false;
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
                    string reply = "";
                    int i = -1;
                    foreach(var t in playlist[e.User.VoiceChannel.Id])
                    {
                        string title = GetTitle(t);
                        if (++i == 0)
                        {
                            reply = $"Currently playing: {title}.\nNext songs:";
                            continue;
                        }
                        string ext = "";
                        if (t.Item2 == "Request" || t.Item2 == "Youtube")
                            ext = $"{(t.Item2 == "Request" ? " request" : "")} by <@{t.Item3}>";
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
                        var video = await YouTube.Default.GetVideoAsync(e.Args[0]);
                        try
                        {
                            var pl = playlist[e.User.VoiceChannel.Id];
                            if (InPlaylist(pl, video.Uri))
                            {
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist.");
                                return;
                            }
                            pl.Insert(NonrequestedIndex(e), new Song(video.Uri, "Youtube", e.User.Id, $"{video.Title} ({e.Args[0]})"));
                            await Program.client.SendMessage(e.Channel, $"{video.Title} added to the playlist.");
                        }
                        catch (Exception)
                        {
                            // Possibly region locking due to licencing/music identification?
                            await Program.client.SendMessage(e.Channel, $"{video.Title} couldn't be added to the playlist because of an issue under investigation.");
                        }
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
                    foreach (var file in Files())
                    {
                        if (System.IO.Path.GetFileNameWithoutExtension(file).ToLower().Contains(string.Join(" ", e.Args).ToLower()))
                        {
                            var pl = playlist[e.User.VoiceChannel.Id];
                            if (InPlaylist(pl, file))
                            {
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist.");
                                return;
                            }
                            pl.Insert(NonrequestedIndex(e), new Song(file, "Request", e.User.Id));
                            await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request has been added to the list.");
                            return;
                        }
                    }
                    await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request was not found.");
                });

            group.CreateCommand("skip")
                .Description("Vote to skip the current song. (Will skip at 50% or more)")
                .FlagMusic(true)
                .Do(async e =>
                {
                    if (await AddVote(voteskip, e, "skip current song", "skipping song", "skip"))
                       skip[e.User.VoiceChannel.Id] = true;
                });

            group.CreateCommand("reset")
                .Description("Vote to reset the stream. (Will reset at 50% or more)")
                .FlagMusic(true)
                .Do(async e =>
                {
                    if (await AddVote(votereset, e, "reset the stream", "resetting stream", "reset"))
                        await ResetStream(e.User.VoiceChannel.Id);
                });

            group.CreateCommand("encore")
                .Alias("replay")
                .Alias("ankoru")
                .Description("Vote to replay the current song. (Will replay at 50% or more)")
                .FlagMusic(true)
                .Do(async e =>
                {
                    if (await AddVote(voteencore, e, "replay current song", "song will be replayed", "replay"))
                    {
                        var pl = playlist[e.User.VoiceChannel.Id];
                        pl.Insert(1, pl[0].Encore(e.User.Id));
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
                    await Program.client.SendMessage(e.Channel, "Reseting stream...");
                    await ResetStream(e.User.VoiceChannel.Id);
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
                                await Music.Stream(e.User.VoiceChannel.Id);
                            }
                        }
                        else await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, the argument needs to be either on or off.");
                    }
                });
        }
    }
}
