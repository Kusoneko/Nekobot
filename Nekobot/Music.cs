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
            internal Song(string uri, EType type = EType.Playlist, User requester = null, string ext = null) { Uri = uri; Type = type; Requester = requester?.Mention; Ext = ext; }
            internal Song Encore() => new Song(Uri, IsOnline ? Type : EType.Encore, null, Ext);

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
            internal string ExtTitle => $"**[{Type}{(Requester != null ? $" by {Requester}" : "")}]** {Title()}";
            internal bool IsOnline => Type == EType.Youtube || Type == EType.SoundCloud;
            internal bool Nonrequested => Type != EType.Playlist;

            internal enum EType { Playlist, Request, Youtube, SoundCloud, Encore }
            internal string Uri, Requester, Ext;
            internal EType Type;
        }
        // Music-related variables
        internal static string Folder;
        internal static bool UseSubdirs;
        static List<ulong> streams = new List<ulong>();
        static Dictionary<ulong, List<Song>> playlist = new Dictionary<ulong, List<Song>>();
        static Dictionary<ulong, bool> skip = new Dictionary<ulong, bool>();
        static Dictionary<ulong, bool> reset = new Dictionary<ulong, bool>();
        internal static Dictionary<ulong, bool> pause = new Dictionary<ulong, bool>();
        static Dictionary<ulong, List<ulong>> voteskip = new Dictionary<ulong, List<ulong>>();
        static Dictionary<ulong, List<ulong>> votereset = new Dictionary<ulong, List<ulong>>();
        static Dictionary<ulong, List<ulong>> voteencore = new Dictionary<ulong, List<ulong>>();
        static string[] exts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac", ".ogg" };

        internal static IEnumerable<string> Files() => System.IO.Directory.EnumerateFiles(Folder, "*.*", UseSubdirs ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly).Where(s => exts.Contains(System.IO.Path.GetExtension(s)));
        static bool InPlaylist(List<Song> playlist, string common, bool online = false) => playlist.Exists(song => (song.IsOnline == online) && (online ? song.Ext : song.Uri) == common);
        static int NonrequestedIndex(Commands.CommandEventArgs e) => 1 + playlist[e.User.VoiceChannel.Id].Skip(1).Where(song => song.Nonrequested).Count();

        static Commands.CommandBuilder CreatePLCmd(Commands.CommandGroupBuilder group, string name, string parameter, string description, string[] aliases = null)
        {
            var cmd = group.CreateCommand(name)
                .Parameter(parameter, Commands.ParameterType.Unparsed)
                .Description(description)
                .FlagMusic(true);
            if (aliases != null) foreach (var alias in aliases) cmd.Alias(alias);
            return cmd;
        }

        class SC : SoundCloud.NET.SoundCloudManager
        {
            public SC(string clientId, string userAgent = "") : base(clientId, userAgent) { }
            async Task<bool> Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Track track, bool multiple, bool isplaylist = false)
            {
                var pl = playlist[e.User.VoiceChannel.Id];
                var title = $"{track.Title} by {track.User.Username}";
                if (!track.Streamable)
                {
                    if (multiple) await e.Channel.SendMessage($"{title} is not streamable.");
                    return false;
                }
                var ext = $"{title} (**{track.PermalinkUrl}**)";
                if (!InPlaylist(pl, ext, true))
                {
                    var uri = track.StreamUrl;
                    pl.Insert(NonrequestedIndex(e), new Song($"{uri}?client_id={ClientID}", Song.EType.SoundCloud, e.User, ext));
                    if (!isplaylist) await e.Channel.SendMessage($"{title} added to the playlist.");
                    return true;
                }
                if (multiple)
                    await e.Channel.SendMessage($"{title} is already in the playlist.");
                return false;
            }
            async Task<bool> Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Playlist playlist, bool multiple)
            {
                bool ret = false;
                foreach (var track in playlist.Tracks)
                    ret |= await Triad(e, track, false, true);
                if (ret)
                    await e.Channel.SendMessage($"The contents of {playlist.Title} by {playlist.User.Username} have been added to the playlist.");
                else if (!multiple)
                    await e.Channel.SendMessage($"There is nothing in {playlist.Title} that isn't already in the playlist.");
                return ret;
            }

            public void CreatePermalinkCmd(Commands.CommandGroupBuilder group, string name, string[] aliases, bool is_playlist)
            {
                CreatePLCmd(group, name, $"SoundCloud {(!is_playlist ? "Track" : "Playlist")} Permalink"/*(s)"*/, $"I'll add SoundCloud {(is_playlist ? "playlist" : "")} songs to the playlist!", aliases)
                    .Do(async e =>
                    {
                        //MatchCollection m = Regex.Matches(e.Args[0], @"", RegexOptions.IgnoreCase);
                        if (e.Args[0] == ""/*m.Count == 0*/)
                            await e.Channel.SendMessage($"{e.User.Mention} No SoundCloud permalink matches.");
                        else //foreach (Match match in m)
                            await (is_playlist ? Triad(e, GetPlaylist(e.Args[0]), false) : Triad(e, GetTrack(e.Args[0])/*match.Groups[1]*/, true));
                    });
            }
            public void CreateSearchCmd(Commands.CommandGroupBuilder group, string name, string[] aliases, bool is_playlist)
            {
                CreatePLCmd(group, name, is_playlist ? "SoundCloud Playlist Keywords" : "song to find",
                    $"I'll search for your {(is_playlist ? "playlist " : "")}request on SoundCloud!\nResults will be considered in order until one not in the playlist is found.", aliases)
                    .Do(async e =>
                    {
                        var search = new SoundCloud.NET.SearchParameters { SearchString = string.Join(" ", e.Args), Streamable = true };
                        var container = is_playlist ? (SoundCloud.NET.Models.BaseModel[])SearchPlaylist(search) : SearchTrack(search);
                        if (container.Count() == 0)
                        {
                            await e.Channel.SendMessage($"{e.User.Mention} Your request was not found.");
                            return;
                        }
                        foreach (var thing in container)
                            if (await (is_playlist ? Triad(e, (SoundCloud.NET.Models.Track)thing, true) : Triad(e, (SoundCloud.NET.Models.Playlist)thing, false))) return;
                        await e.Channel.SendMessage($"{e.User.Mention} No results for your requested search aren't already in the playlist.");
                    });
            }
        }

        static IWaveProvider Reader(string file)
        {
            for (byte i = 3; i != 0; --i) try
            {
                return System.IO.Path.GetExtension(file) == ".ogg"
                        ? (IWaveProvider)new NAudio.Vorbis.VorbisWaveReader(file)
                        : new MediaFoundationReader(file);
            } catch { }
            return null;
        }
        static async Task Stream(Channel c)
        {
            ulong cid = c.Id;
            Discord.Audio.DiscordAudioClient _client = await Voice.JoinServer(c);
            if (_client == null) return; // TODO: Remove when voice works.
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
                voteskip[cid] = new List<ulong>();
                votereset[cid] = new List<ulong>();
                voteencore[cid] = new List<ulong>();
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
                        var musicReader = Reader(playlist[cid][0].Uri);
                        if (musicReader == null)
                        {
                            Program.log.Warning("Stream", $"{playlist[cid][0].Uri} couldn't be read.");
                            return;
                        }
                        using (var resampler = new MediaFoundationResampler(musicReader, outFormat) { ResamplerQuality = 60 })
                        {
                            int byteCount;
                            while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                            {
                                if (!streams.Contains(cid) || skip[cid] || reset[cid])
                                {
                                    _client.Clear();
                                    await Task.Delay(1000);
                                    break;
                                }
                                while(pause[cid]) await Task.Delay(500); // Play Voice.cs commands in here?
                                _client.Send(buffer, blockSize);
                            }
                        }
                    }
                    catch (OperationCanceledException err) { Program.log.Error("Stream", err.Message); }
                });
                _client.Wait(); // Prevent endless queueing which would eventually eat up all the ram
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
            await Program.Audio.Leave(c.Server);
        }

        internal static Task StartStreams(DiscordClient client)
        {
            return Task.WhenAll(
              streams.Select(s =>
              {
                  Channel c = client.GetChannel(s);
                  if (c.Type == ChannelType.Voice)
                      return Task.Run(() => Stream(c));
                  else
                      return null;
              })
              .Where(t => t != null)
              .ToArray());
        }

        internal static void LoadStreams()
        {
            var reader = SQL.ReadChannels("music=1");
            while (reader.Read())
                streams.Add(Convert.ToUInt64(reader["channel"].ToString()));
        }

        static async Task ResetStream(Channel c)
        {
            reset[c.Id] = true;
            await Task.Delay(5000);
            await Stream(c);
        }

        internal static async Task StopStreams(Server server)
        {
            var serverstreams = streams.Where(stream => server.GetChannel(stream) != null).ToArray();
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
            => chan.Type != ChannelType.Voice ? -1 : chan.Users.Where(u => u.VoiceChannel == chan).Count()-1;

        static async Task<bool> AddVote(Dictionary<ulong, List<ulong>> votes, Commands.CommandEventArgs e, string action, string success, string actionshort)
        {
            var vote = votes[e.User.VoiceChannel.Id];
            if (!vote.Contains(e.User.Id))
            {
                vote.Add(e.User.Id);
                var listeners = CountVoiceChannelMembers(e.User.VoiceChannel);
                if (vote.Count >= Math.Ceiling((decimal)listeners / 2))
                {
                    await e.Channel.SendMessage($"{vote.Count}/{listeners} votes to {action}. 50%+ achieved, {success}...");
                    return true;
                }
                await e.Channel.SendMessage($"{vote.Count}/{listeners} votes to {action}. (Needs 50% or more to {actionshort})");
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
                    await e.Channel.SendMessage(reply);
                });

            group.CreateCommand("song")
                .Description("I'll tell you the song I'm currently playing.")
                .FlagMusic(true)
                .Do(async e => await e.Channel.SendMessage($"Currently playing: {playlist[e.User.VoiceChannel.Id][0].Title()}."));

            CreatePLCmd(group, "ytrequest", "youtube video link(s)", "I'll add youtube videos to the playlist")
                .Do(async e =>
                {
                    Program.rclient.BaseUrl = new Uri("http://www.youtubeinmp3.com/fetch/");
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
                            await e.Channel.SendMessage($"{e.User.Mention} Your request ({uri_title.Item2}) is already in the playlist.");
                        else
                        {
                            pl.Insert(NonrequestedIndex(e), new Song(uri_title.Item1, Song.EType.Youtube, e.User, ext));
                            await e.Channel.SendMessage($"{uri_title.Item2} added to the playlist.");
                        }
                    }
                    if (m.Count == 0)
                        await e.Channel.SendMessage($"None of {e.Args[0]} could be added to playlist because no valid youtube links were found within.");
                });

            if (Program.config["SoundCloud"].HasValues)
            {
                SC sc = new SC(Program.config["SoundCloud"]["client_id"].ToString(), Program.rclient.UserAgent);
                sc.CreateSearchCmd(group, "scsearch", new string[]{"scs"}, false);
                sc.CreatePermalinkCmd(group, "screquest", new string[]{"sctrack", "sctr"}, false);
                sc.CreatePermalinkCmd(group, "scplaylist", new string[]{"scpl"}, false);
                //sc.CreateSearchCmd(group, "scplsearch", new string[]{"scpls"}, true); // Until this stops giving Gateway timeouts, RIP.
            }

            CreatePLCmd(group, "request", "song to find", "I'll try to add your request to the playlist!")
                .Do(async e =>
                {
                    var args = string.Join(" ", e.Args);
                    if (args.Length == 0)
                    {
                        await e.Channel.SendMessage("You need to provide at least a character to search for.");
                        return;
                    }
                    args.ToLower();
                    foreach (var file in Files())
                    {
                        if (System.IO.Path.GetFileNameWithoutExtension(file).ToLower().Contains(args))
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
                                        await e.Channel.SendMessage($"{e.User.Mention} Your request is already in the playlist at {cur_i}.");
                                    return;
                                }
                                pl.RemoveAt(cur_i);
                            }
                            pl.Insert(i, new Song(file, Song.EType.Request, e.User));
                            await e.Channel.SendMessage($"{e.User.Mention} Your request has been added to the list.");
                            return;
                        }
                    }
                    await e.Channel.SendMessage($"{e.User.Mention} Your request was not found.");
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
                        await ResetStream(e.User.VoiceChannel);
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
                    await e.Channel.SendMessage("Forcefully skipping song...");
                });

            group.CreateCommand("forcereset")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll reset the stream in case of bugs, while keeping the playlist intact.")
                .Do(async e =>
                {
                    await e.Channel.SendMessage("Reseting stream...");
                    await ResetStream(e.User.VoiceChannel);
                });

            group.CreateCommand("pause")
                .Alias("unpause")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll toggle pause on the stream")
                .Do(async e =>
                {
                    await e.Channel.SendMessage($"{(pause[e.User.VoiceChannel.Id] ? "Resum" : "Paus")}ing stream...");
                    pause[e.User.VoiceChannel.Id] = !pause[e.User.VoiceChannel.Id];
                });

            // Administrator commands
            group.CreateCommand("music")
                .Parameter("on/off", Commands.ParameterType.Required)
                .Description("I'll start or end a stream in a particular voice channel, which you need to be in.")
                .MinPermissions(2)
                .Do(async e => await (e.User.VoiceChannel?.Id <= 0 ? e.Channel.SendMessage($"{e.User.Mention}, you need to be in a voice channel to use this.")
                    : Helpers.OnOffCmd(e, async on =>
                    {
                        bool has_stream = streams.Contains(e.User.VoiceChannel.Id);
                        string status = on ? "start" : "halt";
                        if (has_stream == on)
                        {
                            string blah = on ? "streaming in! Did you mean to !reset or !forcereset the stream?" : "not streaming in!";
                            await e.Channel.SendMessage($"{e.User.Mention}, I can't {status} streaming in a channel that I'm already {blah}");
                        }
                        else
                        {
                            SQL.AddOrUpdateFlag(e.User.VoiceChannel.Id, "music", on ? "1" : "0");
                            await e.Channel.SendMessage($"{e.User.Mention}, I'm {status}ing the stream!");
                            if (on)
                            {
                                await StopStreams(e.Server);
                                streams.Add(e.User.VoiceChannel.Id);
                                await Stream(e.User.VoiceChannel);
                            }
                            else streams.Remove(e.User.VoiceChannel.Id);
                        }
                    })));
        }
    }
}
