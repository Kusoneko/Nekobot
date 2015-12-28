using System;
using System.Collections.Generic;
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
        class Song
        {
            internal Song(string uri, EType type = EType.Playlist, long requester = 0, string ext = null) { Uri = uri; Type = type; Requester = requester; Ext = ext; }
            internal Song Encore() => new Song(Uri, IsOnline ? Type : EType.Encore, 0, Ext);

            internal string Title()
            {
                if (Ext == null)
                {
                    File song = File.Create(Uri);
                    if (song.Tag.Title != null && song.Tag.Title != "")
                    {
                        Ext = "";
                        if (song.Tag.Performers != null)
                            foreach (string p in song.Tag.Performers)
                                Ext += $", {p}";
                        if (Ext != "")
                            Ext = Ext.Substring(2) + " **-** ";
                        Ext += song.Tag.Title;
                    }
                    else Ext = System.IO.Path.GetFileNameWithoutExtension(Uri);
                }
                return Ext;
            }
            internal string ExtTitle => $"**[{Type}{(Requester != 0 ? $" by <@{Requester}>" : "")}]** {Title()}";
            internal bool IsOnline => Type == EType.Youtube || Type == EType.SoundCloud;
            internal bool Nonrequested => Type != EType.Playlist;

            internal enum EType { Playlist, Request, Youtube, SoundCloud, Encore }
            internal string Uri, Ext;
            internal EType Type;
            internal long Requester;
        }
        // Music-related variables
        internal static string Folder;
        internal static bool UseSubdirs;
        static List<long> streams = new List<long>();
        static Dictionary<long, List<Song>> playlist = new Dictionary<long, List<Song>>();
        static Dictionary<long, bool> skip = new Dictionary<long, bool>();
        static Dictionary<long, bool> reset = new Dictionary<long, bool>();
        internal static Dictionary<long, bool> pause = new Dictionary<long, bool>();
        static Dictionary<long, List<long>> voteskip = new Dictionary<long, List<long>>();
        static Dictionary<long, List<long>> votereset = new Dictionary<long, List<long>>();
        static Dictionary<long, List<long>> voteencore = new Dictionary<long, List<long>>();
        static string[] exts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac", ".ogg" };

        internal static IEnumerable<string> Files() => System.IO.Directory.EnumerateFiles(Folder, "*.*", UseSubdirs ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly).Where(s => exts.Contains(System.IO.Path.GetExtension(s)));
        static bool InPlaylist(List<Song> playlist, string common, bool online = false) => playlist.Exists(song => (song.IsOnline == online) && (online ? song.Ext : song.Uri) == common);
        static int NonrequestedIndex(Commands.CommandEventArgs e) => 1 + playlist[e.User.VoiceChannel.Id].Skip(1).Where(song => song.Nonrequested).Count();

        class SC
        {
            public static async Task<bool> Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Track track, bool multiple, string client_id, bool isplaylist = false)
            {
                var pl = playlist[e.User.VoiceChannel.Id];
                var title = $"{track.Title} by {track.User.Username}";
                if (!track.Streamable)
                {
                    if (multiple) await Program.client.SendMessage(e.Channel, $"{title} is not streamable.");
                    return false;
                }
                var ext = $"{title} (**{track.PermalinkUrl}**)";
                if (!InPlaylist(pl, ext, true))
                {
                    var uri = track.StreamUrl;
                    pl.Insert(NonrequestedIndex(e), new Song($"{uri}?client_id={client_id}", Song.EType.SoundCloud, e.User.Id, ext));
                    if (!isplaylist) await Program.client.SendMessage(e.Channel, $"{title} added to the playlist.");
                    return true;
                }
                if (multiple)
                    await Program.client.SendMessage(e.Channel, $"{title} is already in the playlist.");
                return false;
            }

            public static async Task<bool> PLTriad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Playlist playlist, bool multiple, string client_id)
            {
                bool ret = false;
                foreach (var track in playlist.Tracks)
                    ret |= await Triad(e, track, false, client_id, true);
                if (ret)
                    await Program.client.SendMessage(e.Channel, $"The contents of {playlist.Title} by {playlist.User.Username} have been added to the playlist.");
                else if (!multiple)
                    await Program.client.SendMessage(e.Channel, $"There is nothing in {playlist.Title} that isn't already in the playlist.");
                return ret;
            }

            public static SoundCloud.NET.SearchParameters SearchArgs(string[] args)
                => new SoundCloud.NET.SearchParameters { SearchString = string.Join(" ", args), Streamable = true };
        }

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
            if (!pause.ContainsKey(cid))
                pause.Add(cid, false);
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
                    playlist[cid].Add(new Song(mp3));
                }
                await Task.Run(async () =>
                {
                    try
                    {
                        var outFormat = new WaveFormat(48000, 16, 1);
                        int blockSize = outFormat.AverageBytesPerSecond; // 1 second
                        byte[] buffer = new byte[blockSize];
                        string file = playlist[cid][0].Uri;
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
                                while(pause[cid]) await Task.Delay(500); // Play Voice.cs commands in here?
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
            voteskip.Remove(cid);
            votereset.Remove(cid);
            voteencore.Remove(cid);
            skip.Remove(cid);
            reset.Remove(cid);
            pause.Remove(cid);
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
            var reader = SQL.ReadChannels("music = 1");
            while (reader.Read())
                streams.Add(Convert.ToInt64(reader["channel"].ToString()));
        }

        static async Task ResetStream(long channel)
        {
            reset[channel] = true;
            await Task.Delay(5000);
            await Stream(channel);
        }

        internal static async Task StopStreams(Server server)
        {
            var serverstreams = streams.Where(stream => Program.client.GetChannel(stream).Server == server).ToArray();
            foreach (var stream in serverstreams)
            {
                SQL.AddOrUpdateFlag(stream, "music", "0");
                if (pause[stream]) pause[stream] = false;
                streams.Remove(stream);
            }
            if (serverstreams.Length != 0)
                await Task.Delay(5000);
        }

        static async Task Encore(Commands.CommandEventArgs e)
        {
            if (await AddVote(voteencore, e, "replay current song", "song will be replayed", "replay"))
            {
                var pl = playlist[e.User.VoiceChannel.Id];
                pl.Insert(1, pl[0].Encore());
            }
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
                        reply += (++i == 0) ? $"Currently playing: {t.Title()}.\nNext songs:" : $"\n{i} - {t.ExtTitle}";
                        if (reply.Length > 2000)
                        {
                            reply = reply.Substring(0, reply.LastIndexOf('\n'));
                            break;
                        }
                    }
                    await Program.client.SendMessage(e.Channel, reply);
                });

            group.CreateCommand("song")
                .Description("I'll tell you the song I'm currently playing.")
                .FlagMusic(true)
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, $"Currently playing: {playlist[e.User.VoiceChannel.Id][0].Title()}.");
                });

            // TODO: Clean up the request commands, they share too much code.
            group.CreateCommand("ytrequest")
                .Parameter("youtube video link(s)", Commands.ParameterType.Unparsed)
                .Description("I'll add youtube videos to the playlist")
                .FlagMusic(true)
                .Do(async e =>
                {
                    MatchCollection m = Regex.Matches(e.Args[0], @"youtu(?:be\.com\/(?:v\/|e(?:mbed)?\/|watch\?v=)|\.be\/)([\w-_]{11}\b)", RegexOptions.IgnoreCase);
                    foreach (Match match in m)
                    {
                        var link = $"youtube.com/watch?v={match.Groups[1]}";
                        Tuple<string,string> uri_title;
                        try { var video = await YouTube.Default.GetVideoAsync(link); uri_title = Tuple.Create(video.Uri, video.Title); }
                        catch
                        {
                            Program.rclient.BaseUrl = new Uri("http://www.youtubeinmp3.com/fetch/");
                            // Content is sometimes an html page instead of JSON, we should ask why.
                            var json = Newtonsoft.Json.Linq.JObject.Parse(Program.rclient.Execute(new RestSharp.RestRequest($"?format=JSON&video={System.Net.WebUtility.UrlEncode(link)}", RestSharp.Method.GET)).Content);
                            uri_title = Tuple.Create(json["link"].ToString(), json["title"].ToString());
                        }
                        var pl = playlist[e.User.VoiceChannel.Id];
                        var ext = $"{uri_title.Item2} ({link})";
                        if (InPlaylist(pl, ext, true))
                            await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request ({uri_title.Item2}) is already in the playlist.");
                        else
                        {
                            pl.Insert(NonrequestedIndex(e), new Song(uri_title.Item1, Song.EType.Youtube, e.User.Id, ext));
                            await Program.client.SendMessage(e.Channel, $"{uri_title.Item2} added to the playlist.");
                        }
                    }
                    if (m.Count == 0)
                        await Program.client.SendMessage(e.Channel, $"None of {e.Args[0]} could be added to playlist because no valid youtube links were found within.");
                });

            if (Program.config["SoundCloud"].HasValues)
            {
                var client_id = Program.config["SoundCloud"]["client_id"].ToString();
                var mgr = new SoundCloud.NET.SoundCloudManager(client_id, Program.rclient.UserAgent);
                group.CreateCommand("scsearch")
                    .Alias("scs")
                    .Parameter("song to find", Commands.ParameterType.Required)
                    .Parameter("...", Commands.ParameterType.Multiple)
                    .Description("I'll search for your request on SoundCloud!\nResults will be considered in order until one not in the playlist is found.")
                    .FlagMusic(true)
                    .Do(async e =>
                    {
                        var tracks = mgr.SearchTrack(SC.SearchArgs(e.Args));
                        if (tracks.Count() == 0)
                        {
                            await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request was not found.");
                            return;
                        }
                        foreach (var track in tracks)
                            if (await SC.Triad(e, track, false, client_id)) return;
                        await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> No results for your requested search aren't already in the playlist.");
                    });

                group.CreateCommand("screquest")
                    .Alias("sctrack")
                    .Alias("sctr")
                    .Parameter("SoundCloud Track Permalink"/*(s)"*/, Commands.ParameterType.Unparsed)
                    .Description("I'll add SoundCloud songs to the playlist!")
                    .FlagMusic(true)
                    .Do(async e =>
                    {
                        //MatchCollection m = Regex.Matches(e.Args[0], @"", RegexOptions.IgnoreCase);
                        if (e.Args[0] == ""/*m.Count == 0*/)
                            await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> No SoundCloud track permalink matches.");
                        else //foreach (Match match in m)
                            await SC.Triad(e, mgr.GetTrack(e.Args[0])/*match.Groups[1]*/, true, client_id);
                    });

                group.CreateCommand("scplaylist")
                    .Alias("scpl")
                    .Parameter("SoundCloud Playlist Permalink"/*(s)"*/, Commands.ParameterType.Unparsed)
                    .Description("I'll add SoundCloud playlist songs to the playlist!")
                    .FlagMusic(true)
                    .Do(async e =>
                    {
                        //MatchCollection m = Regex.Matches(e.Args[0], @"", RegexOptions.IgnoreCase);
                        if (e.Args[0] == ""/*m.Count == 0*/)
                            await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> No SoundCloud playlist permalink matches.");
                        else //foreach (Match match in m)
                            await SC.PLTriad(e, mgr.GetPlaylist(e.Args[0]/*match.Groups[1]*/), false, client_id);
                    });

                group.CreateCommand("scplsearch")
                    .Alias("scpls")
                    .Parameter("SoundCloud Playlist Keywords", Commands.ParameterType.Unparsed)
                    .Description("I'll add SoundCloud playlist songs to the playlist!")
                    .FlagMusic(true)
                    .AddCheck((h, i, d) => false).Hide() // Until this stops giving Gateway timeouts, RIP.
                    .Do(async e =>
                    {
                        var pls = mgr.SearchPlaylist(SC.SearchArgs(e.Args));
                        foreach (var pl in pls)
                            if (await SC.PLTriad(e, pl, true, client_id)) return;
                        await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> No results for your requested search aren't already in the playlist.");
                    });
            }

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
                            var i = NonrequestedIndex(e);
                            var cur_i = pl.FindIndex(song => song.Uri == file);
                            if (cur_i != -1)
                            {
                                if (i > cur_i)
                                {
                                    if (cur_i == 0)
                                        await Encore(e);
                                    else
                                        await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist at {cur_i}.");
                                    return;
                                }
                                pl.RemoveAt(cur_i);
                            }
                            pl.Insert(i, new Song(file, Song.EType.Request, e.User.Id));
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
                .Do(async e => await Encore(e));

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

            group.CreateCommand("pause")
                .Alias("unpause")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll toggle pause on the stream")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, $"{(pause[e.User.VoiceChannel.Id] ? "Resum" : "Paus")}ing stream...");
                    pause[e.User.VoiceChannel.Id] = !pause[e.User.VoiceChannel.Id];
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
                                SQL.AddOrUpdateFlag(e.User.VoiceChannel.Id, "music", off ? "0" : "1");
                                await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, I'm {status}ing the stream!");
                                if (on)
                                {
                                    await StopStreams(e.Server);
                                    streams.Add(e.User.VoiceChannel.Id);
                                    await Stream(e.User.VoiceChannel.Id);
                                }
                                else streams.Remove(e.User.VoiceChannel.Id);
                            }
                        }
                        else await Program.client.SendMessage(e.Channel, $"<@{e.User.Id}>, the argument needs to be either on or off.");
                    }
                });
        }
    }
}
