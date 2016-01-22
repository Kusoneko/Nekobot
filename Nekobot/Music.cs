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
        class Playlist : List<Song>
        {
            internal void Initialize()
            {
                if (!HasFolder()) return;
                lock (this)
                {
                    var files = Files();
                    var filecount = Math.Min(files.Count(), 11);
                    Random rnd = new Random();
                    while (Count < filecount)
                    {
                        var mp3 = files.ElementAt(rnd.Next(0, filecount));
                        if (InPlaylist(mp3))
                            continue;
                        Add(new Song(mp3));
                    }
                }
            }
            // Return true if we're resetting
            internal bool Cleanup()
            {
                lock (voteskip) voteskip.Clear();
                lock (votereset) votereset.Clear();
                lock (voteencore) voteencore.Clear();
                skip = false;
                if (exit)
                {
                    exit = false;
                    return true;
                }
                lock(this) if (Any) RemoveAt(0);
                return false;
            }

            internal void SkipSongs(int count = 1)
            {
                if (count >= Count)
                    SkipAll();
                else if (count > 0)
                {
                    lock (this)
                    {
                        if (count != 1) RemoveRange(1, count - 1);
                        skip = true; // Skip current song.
                    }
                }
            }

            internal void SkipAll()
            {
                lock(this)
                {
                    Clear();
                    skip = true; // Skip current song.
                }
            }

            #region Information
            bool Any => this.Any();

            bool InPlaylist(string common, bool online = false) => Exists(song => (song.IsOnline == online) && (online ? song.Ext : song.Uri) == common);

            int NonrequestedIndex() => Any ? 1 + this.Skip(1).Where(song => song.Nonrequested).Count() : 0;

            internal async Task<string> CurrentUri()
            {
                while (!Any)
                {
                    if (exit) return null;
                    await Task.Delay(5000);
                }
                lock (this) return this[0].Uri;
            }

            string EmptyPlaylist() =>
                Any ? null : "The playlist is currently empty, use commands to request something.";

            internal string CurrentSong()
            {
                lock(this)
                    return EmptyPlaylist() ?? $"Currently playing: {this[0].Title()}.";
            }

            internal string SongCount()
            {
                lock (this)
                    return EmptyPlaylist() ?? $"There are currently {Count} songs left in the playlist.";
            }

            internal string SongList()
            {
                lock (this)
                {
                    string reply = EmptyPlaylist() ?? "";
                    if (reply != "") return reply;
                    string padding = Helpers.ZeroPadding(Count);
                    int i = -1;
                    foreach(var t in this)
                    {
                        reply += (++i == 0) ? $"Currently playing: {t.Title()}.{(Count > 1 ? "\nNext songs:" : "")}" : $"\n{Helpers.ZeroPaddingAt(i, ref padding)}{i} - {t.ExtTitle}";
                        if (reply.Length > 2000) return reply.Substring(0, reply.LastIndexOf('\n'));
                    }
                    return reply;
                }
            }
            #endregion

            #region Insert Wrappers
            internal void InsertFile(string file, Commands.CommandEventArgs e)
            {
                lock (this)
                {
                    var i = NonrequestedIndex();
                    var cur_i = FindIndex(song => song.Uri == file);
                    if (cur_i != -1)
                    {
                        if (i > cur_i)
                        {
                            if (cur_i == 0)
                            {
                                if (EncoreVote(e)) InsertEncore();
                            }
                            else
                                e.Channel.SendMessage($"{e.User.Mention} Your request is already in the playlist at {cur_i}.");
                            return;
                        }
                        RemoveAt(cur_i);
                    }
                    Insert(i, new Song(file, Song.EType.Request, e.User));
                }
            }

            internal bool TryInsert(Song song)
            {
                lock (this)
                if (!InPlaylist(song.Ext, true))
                {
                    Insert(NonrequestedIndex(), song);
                    return true;
                }
                return false;
            }

            void InsertEncore() => Insert(1, this[0].Encore());
            #endregion

            #region Votes and actions
            internal bool AddVote(List<ulong> vote, Commands.CommandEventArgs e, string action, string success, string actionshort)
            {
                lock (vote)
                if (!vote.Contains(e.User.Id))
                {
                    vote.Add(e.User.Id);
                    var listeners = e.User.VoiceChannel.Users.Count() - 1;
                    var needed = Math.Ceiling((decimal)listeners / 2);
                    if (vote.Count == needed)
                    {
                        e.Channel.SendMessage($"{vote.Count}/{listeners} votes to {action}. 50%+ achieved, {success}...");
                        return true;
                    }
                    else if (vote.Count < needed)
                        e.Channel.SendMessage($"{vote.Count}/{listeners} votes to {action}. (Needs 50% or more to {actionshort})");
                }
                return false;
            }

            bool EncoreVote(Commands.CommandEventArgs e)
                => AddVote(voteencore, e, "replay current song", "song will be replayed", "replay");

            internal void Encore(Commands.CommandEventArgs e)
            {
                if (EncoreVote(e))
                    lock (this) InsertEncore();
            }

            internal void Skip(Commands.CommandEventArgs e)
            {
                if (!skip && AddVote(voteskip, e, "skip current song", "skipping song", "skip"))
                    SkipSongs();
            }
            internal async Task Reset(Commands.CommandEventArgs e)
            {
                if (!exit && AddVote(votereset, e, "reset the stream", "resetting stream", "reset"))
                    await streams.Reset(e.User.VoiceChannel);
            }
            internal void Pause(Commands.CommandEventArgs e)
            {
                e.Channel.SendMessage($"{(pause ? "Resum" : "Paus")}ing stream...");
                pause = !pause;
            }
            internal void Exit()
            {
                lock (this)
                {
                    pause = false;
                    exit = true;
                }
            }
            #endregion

            List<ulong> voteskip = new List<ulong>(), votereset = new List<ulong>(), voteencore = new List<ulong>();
            internal bool skip = false, exit = false, pause = false;
        }

        class Stream
        {
            internal Stream(Channel chan, bool request) { Channel = chan; _request = request; }

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
            internal async Task Play()
            {
                ulong cid = Channel.Id;
                var _client = await Voice.JoinServer(Channel);
                if (!playlist.ContainsKey(cid))
                    playlist.Add(cid, new Playlist());
                var pl = Playlist;
                while (streams.Contains(this))
                {
                    if (!_request) pl.Initialize();
                    await Task.Run(async () =>
                    {
                        try
                        {
                            var outFormat = new WaveFormat(48000, 16, Program.Audio.Config.Channels);
                            int blockSize = outFormat.AverageBytesPerSecond; // 1 second
                            byte[] buffer = new byte[blockSize];
                            string uri = await pl.CurrentUri();
                            if (uri == null) return; // Only happens if we're done here.
                            var musicReader = Reader(uri);
                            if (musicReader == null)
                            {
                                Program.log.Warning("Stream", $"{uri} couldn't be read.");
                                return;
                            }
                            using (var resampler = new MediaFoundationResampler(musicReader, outFormat) { ResamplerQuality = 60 })
                            {
                                while (resampler.Read(buffer, 0, blockSize) > 0)
                                {
                                    while(pl.pause) await Task.Delay(500); // Play Voice.cs commands in here?
                                    if (!streams.Contains(this) || pl.skip || pl.exit)
                                    {
                                        _client.Clear();
                                        await Task.Delay(1000);
                                        break;
                                    }
                                    _client.Send(buffer, 0, blockSize);
                                }
                            }
                        }
                        catch (OperationCanceledException err) { Program.log.Error("Stream", err.Message); }
                    });
                    _client.Wait(); // Prevent endless queueing which would eventually eat up all the ram
                    if (pl.Cleanup()) break;
                }
                await Program.Audio.Leave(Channel.Server);
            }

            internal void Stop()
            {
                SQL.AddOrUpdateFlag(Channel.Id, "music", "0");
                playlist[Channel.Id].Exit();
                streams.Remove(this);
            }

            internal Channel Channel;
            internal Server Server => Channel.Server;
            Playlist Playlist => playlist[Channel.Id];

            private bool _request;
            internal bool Request
            {
                get { return _request; }
                set
                {
                    if (_request = value)
                        Playlist.SkipAll();
                    else
                        Playlist.Initialize();
                }
            }
        }

        class Streams : List<Stream>
        {
            internal bool Has(Channel c) => this.Any(s => c == s.Channel);
            internal Stream Get(Channel c) => this.FirstOrDefault(s => c == s.Channel);

            internal async Task AddStream(Channel c, bool request = false)
            {
                var stream = new Stream(c, request);
                Add(stream);
                await stream.Play();
            }

            internal Task Load(DiscordClient client)
            {
                // Load the stream channels
                var channels = new List<Tuple<Channel,int>>();
                var reader = SQL.ReadChannels("music<>0", "channel,music");
                while (reader.Read())
                    channels.Add(Tuple.Create(client.GetChannel(Convert.ToUInt64(reader["channel"].ToString())), int.Parse(reader["music"].ToString())));
                return Task.WhenAll(
                  channels.Select(s => s.Item1.Type == ChannelType.Voice ? Task.Run(async() => await AddStream(s.Item1, s.Item2 == 2)) : null)
                  .Where(t => t != null).ToArray());
            }

            internal async Task Play(Commands.CommandEventArgs e, bool request, Stream stream = null)
            {
                SQL.AddOrUpdateFlag(e.User.VoiceChannel.Id, "music", request ? "2" : "1");
                if (stream != null)
                    stream.Request = request;
                else
                {
                    await streams.Stop(e.Server);
                    await streams.AddStream(e.User.VoiceChannel, request);
                }
            }

            internal async Task Stop(Server server)
            {
                var serverstreams = this.Where(stream => server == stream.Server).ToArray();
                foreach (var stream in serverstreams)
                    stream.Stop();
                if (serverstreams.Length != 0)
                    await Task.Delay(5000);
            }

            internal async Task Reset(Channel c)
            {
                playlist[c.Id].Exit();
                await Task.Delay(7500);
                await streams.First(s => s.Channel == c).Play(); // If this throws, something has gone horribly wrong.
            }
        }

        internal static bool Get(Channel c) => c != null && streams.Has(c);
        internal static void Load(DiscordClient c) => streams.Load(c);
        internal static async Task Stop(Server s) => await streams.Stop(s);

        // Music-related variables
        internal static string Folder;
        internal static bool UseSubdirs;
        static Streams streams = new Streams();
        static Dictionary<ulong, Playlist> playlist = new Dictionary<ulong, Playlist>();
        static string[] exts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac", ".ogg" };

        static bool HasFolder() => Folder.Length != 0;
        static IEnumerable<string> Files() => System.IO.Directory.EnumerateFiles(Folder, "*.*", UseSubdirs ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly).Where(s => exts.Contains(System.IO.Path.GetExtension(s)));

        static Commands.CommandBuilder CreatePLCmd(Commands.CommandGroupBuilder group, string name, string description, string[] aliases = null)
        {
            var cmd = group.CreateCommand(name)
                .Description(description)
                .FlagMusic(true);
            if (aliases != null) foreach (var alias in aliases) cmd.Alias(alias);
            return cmd;
        }
        static Commands.CommandBuilder CreatePLCmd(Commands.CommandGroupBuilder group, string name, string parameter, string description, string alias) => CreatePLCmd(group, name, parameter, description, new[]{alias});
        static Commands.CommandBuilder CreatePLCmd(Commands.CommandGroupBuilder group, string name, string parameter, string description, string[] aliases = null) => CreatePLCmd(group, name, description, aliases).Parameter(parameter, Commands.ParameterType.Unparsed);

        class SC : SoundCloud.NET.SoundCloudManager
        {
            public SC(string clientId, string userAgent = "") : base(clientId, userAgent) { }
            bool Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Track track, bool multiple, bool say_added = false)
            {
                var pl = playlist[e.User.VoiceChannel.Id];
                var title = $"{track.Title} by {track.User.Username}";
                if (!track.Streamable)
                {
                    if (multiple) e.Channel.SendMessage($"{title} is not streamable.");
                    return false;
                }
                var ext = $"{title} (**{track.PermalinkUrl}**)";
                if (pl.TryInsert(new Song($"{track.StreamUrl}?client_id={ClientID}", Song.EType.SoundCloud, e.User, ext)))
                {
                    if (!say_added) e.Channel.SendMessage($"{title} added to the playlist.");
                    return true;
                }
                if (multiple)
                    e.Channel.SendMessage($"{title} is already in the playlist.");
                return false;
            }
            int Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Playlist playlist, bool multiple, bool say_added = false)
            {
                int ret = 0; // playlist.TrackCount includes unplayable tracks.
                foreach (var track in playlist.Tracks)
                    if (Triad(e, track, false, true)) ++ret;
                if (!say_added)
                {
                    if (ret != 0)
                        e.Channel.SendMessage($"The contents of {playlist.Title} by {playlist.User.Username} ({ret} tracks) have been added to the playlist.");
                    else if (!multiple)
                        e.Channel.SendMessage($"There is nothing in {playlist.Title} that isn't already in the playlist.");
                }
                return ret;
            }

            public void CreatePermalinkCmd(Commands.CommandGroupBuilder group, string name, string alias, bool is_playlist) => CreatePermalinkCmd(group, name, new[]{alias}, is_playlist);
            public void CreatePermalinkCmd(Commands.CommandGroupBuilder group, string name, string[] aliases, bool is_playlist)
            {
                CreatePLCmd(group, name, $"SoundCloud {(!is_playlist ? "Track" : "Playlist")} Permalink(s)", $"I'll add SoundCloud {(is_playlist ? "playlist " : "")}songs to the playlist!", aliases)
                    .Do(e =>
                    {
                        // TODO: Find out if snd.sc links for playlists exist, it's doubtful since it's not exposed in their ui.
                        MatchCollection m = Regex.Matches(e.Args[0].Replace(' ', '\n'), $@"^https?:\/\/(?:soundcloud\.com({(is_playlist ? "?:" : "?!")}\/.+\/sets){(is_playlist ? "" : @"|snd\.sc")})\/.+$", RegexOptions.IgnoreCase|RegexOptions.Multiline);
                        if (m.Count == 0)
                            e.Channel.SendMessage($"{e.User.Mention} No SoundCloud {(is_playlist ? "playlist" : "track")} permalink matches.");
                        else foreach (var link in from Match match in m select match.Groups[0].ToString())
                            if (is_playlist) Triad(e, GetPlaylist(link), false);
                            else Triad(e, GetTrack(link), true);
                    });
            }

            public enum SearchType
            {
                Simple,
                Multiple,
                Random
            }
            private SoundCloud.NET.Models.BaseModel[] Search(Commands.CommandEventArgs e, bool is_playlist, int desired = 0)
            {
                var search = new SoundCloud.NET.SearchParameters(string.Join(" ", desired != 0 ? e.Args.Skip(1) : e.Args)){ Limit = 200, Streamable = true };
                var container = is_playlist ? (SoundCloud.NET.Models.BaseModel[])SearchPlaylist(search) : SearchTrack(search, desired != 0 ? desired : 500);
                if (container.Count() == 0)
                {
                    e.Channel.SendMessage($"{e.User.Mention} Your request was not found.");
                    return null;
                }
                return container;
            }
            public void CreateSearchCmd(Commands.CommandGroupBuilder group, string name, string alias, bool is_playlist, SearchType st = SearchType.Simple) => CreateSearchCmd(group, name, new[]{alias}, is_playlist, st);
            public void CreateSearchCmd(Commands.CommandGroupBuilder group, string name, string[] aliases, bool is_playlist, SearchType st = SearchType.Simple)
            {
                const int random_tries = 10; // how many times to retry random selection.
                bool multiple = st == SearchType.Multiple;
                var cmd = CreatePLCmd(group, name,
                    $"I'll search for your {(is_playlist ? "playlist " : "")}request on SoundCloud!\nResults will be considered {(st == SearchType.Random ? $"{random_tries} times at random until one is found that isn't already in the playlist." : $"in order until {(!multiple ? "one not in the playlist is found" : "the amount of count or all (that are not already in the playlist) have been added")}")}.", aliases);
                if (multiple) cmd.Parameter("count", Commands.ParameterType.Optional);
                // Until we can figure out how to include Playlist search terms without getting gateway errors, RIP.
                if (!is_playlist) cmd.Parameter("keywords", Commands.ParameterType.Unparsed);
                cmd.Do(e => st == SearchType.Random ? Task.Run(() =>
                    {
                        var container = Search(e, is_playlist);
                        if (container == null) return;
                        var r = new Random();
                        for (int i = random_tries; i != 0; --i)
                        {
                            var thing = container[r.Next(container.Length)];
                            if (is_playlist ? Triad(e, (SoundCloud.NET.Models.Playlist)thing, true) != 0 : Triad(e, (SoundCloud.NET.Models.Track)thing, false)) return;
                        }
                        e.Channel.SendMessage($"No new tracks found, tried {random_tries} times.");
                    })
                    : Task.Run(() =>
                    {
                        int max = 0;
                        if (multiple && e.Args.Length > 1 && !int.TryParse(e.Args[0], out max))
                            max = 100;
                        var container = Search(e, is_playlist, max);
                        if (container == null) return;
                        int count = 0;
                        int trackcount = 0;
                        foreach (var thing in container)
                        {
                            var tc = is_playlist ? Triad(e, (SoundCloud.NET.Models.Playlist)thing, true, multiple) : Triad(e, (SoundCloud.NET.Models.Track)thing, false, multiple) ? 1 : 0;
                            if (tc != 0)
                            {
                                if (!multiple) return;
                                if (is_playlist) trackcount += tc;
                                if (++count == max) break;
                            }
                        }
                        e.Channel.SendMessage(multiple && count != 0 ? $"{count} {(is_playlist ? $"playlists (totaling {trackcount} tracks)" : "tracks")} added." : $"{e.User.Mention} No results for your requested search aren't already in the playlist.");
                    }));
            }
        }

        static class YT
        {
            static RestSharp.RestClient rclient = Helpers.GetRestClient("http://www.youtubeinmp3.com/fetch/");

            class VideoData
            {
                public VideoData(string u, string t, string l) { Uri = u; Title = t; Link = l; }
                public string Uri, Title, Link;
                public static VideoData Get(string link)
                {
                    try { var video = YouTube.Default.GetVideo(link); return new VideoData(video.Uri, video.Title, link); }
                    catch
                    {
                        // TODO: Content is sometimes an html page instead of JSON, we should ask why.
                        var json = Newtonsoft.Json.Linq.JObject.Parse(rclient.Execute(new RestSharp.RestRequest($"?format=JSON&video={System.Net.WebUtility.UrlEncode(link)}", RestSharp.Method.GET)).Content);
                        return new VideoData(json["link"].ToString(), json["title"].ToString(), link);
                    }
                }
            }

            static bool Triad(Commands.CommandEventArgs e, VideoData video)
            {
                bool ret = playlist[e.User.VoiceChannel.Id].TryInsert(new Song(video.Uri, Song.EType.Youtube, e.User, video.Title + (video.Link == null ? "" : $" ({video.Link})")));
                e.Channel.SendMessage(ret ? $"{video.Title} added to the playlist."
                        : $"{e.User.Mention} Your request ({video.Title}) is already in the playlist.");
                return ret;
            }

            static public void CreateCommand(Commands.CommandGroupBuilder group, string name, string alias) => CreateCommand(group, name, new[]{alias});
            static public void CreateCommand(Commands.CommandGroupBuilder group, string name, string[] aliases = null)
            {
                CreatePLCmd(group, name, $"youtube video link(s)", $"I'll add youtube videos to the playlist", aliases)
                    .Do(e =>
                    {
                        MatchCollection m = Regex.Matches(e.Args[0], $@"youtu(?:be\.com\/(?:v\/|e(?:mbed)?\/|watch\?v=)|\.be\/)([\w-_]{"{11}"}\b)", RegexOptions.IgnoreCase);
                        if (m.Count == 0)
                            e.Channel.SendMessage($"None of {e.Args[0]} could be added to playlist because no valid youtube links were found within.");
                        else foreach (var link in from Match match in m select $"youtube.com/watch?v={match.Groups[1]}")
                            Triad(e, VideoData.Get(link));
                    });
            }
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("playlist")
                .Description("I'll give you the list of songs in the playlist.")
                .FlagMusic(true)
                .Do(e => e.Channel.SendMessage(playlist[e.User.VoiceChannel.Id].SongList()));

            group.CreateCommand("songcount")
                .Alias("playlist size")
                .FlagMusic(true)
                .Do(e => e.Channel.SendMessage(playlist[e.User.VoiceChannel.Id].SongCount()));

            group.CreateCommand("song")
                .Description("I'll tell you the song I'm currently playing.")
                .FlagMusic(true)
                .Do(e => e.Channel.SendMessage(playlist[e.User.VoiceChannel.Id].CurrentSong()));

            YT.CreateCommand(group, "ytrequest");

            if (Program.config["SoundCloud"].HasValues)
            {
                SC sc = new SC(Program.config["SoundCloud"]["client_id"].ToString(), Console.Title);
                sc.CreateSearchCmd(group, "scsearch", "scs", false);
                sc.CreateSearchCmd(group, "scsrandom", "scsr", false, SC.SearchType.Random);
                sc.CreateSearchCmd(group, "scsall", new[] {"scsmultiple", "scsa", "scsmulti"}, false, SC.SearchType.Multiple);
                sc.CreatePermalinkCmd(group, "screquest", new[]{"sctrack", "sctr"}, false);
                sc.CreatePermalinkCmd(group, "scplaylist", "scpl", true);
                sc.CreateSearchCmd(group, "scplsearch", "scpls", true);
                sc.CreateSearchCmd(group, "scplsrandom", "scplsr", true, SC.SearchType.Random);
                sc.CreateSearchCmd(group, "scplsall", new[]{"scplsmultiple", "scplsa", "scplsm"}, true, SC.SearchType.Multiple);
            }

            if (HasFolder())
            {
                CreatePLCmd(group, "request", "song to find", "I'll try to add your request to the playlist!")
                    .Do(e =>
                    {
                        var args = string.Join(" ", e.Args);
                        if (args.Length == 0)
                        {
                            e.Channel.SendMessage("You need to provide at least a character to search for.");
                            return;
                        }
                        args = args.ToLower();
                        var file = Files().FirstOrDefault(f => System.IO.Path.GetFileNameWithoutExtension(f).ToLower().Contains(args));
                        if (file != null) playlist[e.User.VoiceChannel.Id].InsertFile(file, e);
                        e.Channel.SendMessage($"{e.User.Mention} Your request {(file != null ? "has been added to the list" : "was not found")}.");
                    });
            }

            group.CreateCommand("skip")
                .Description("Vote to skip the current song. (Will skip at 50% or more)")
                .FlagMusic(true)
                .Do(e => playlist[e.User.VoiceChannel.Id].Skip(e));

            group.CreateCommand("reset")
                .Description("Vote to reset the stream. (Will reset at 50% or more)")
                .FlagMusic(true)
                .Do(e => playlist[e.User.VoiceChannel.Id].Reset(e));

            group.CreateCommand("encore")
                .Alias("replay")
                .Alias("ankoru")
                .Description("Vote to replay the current song. (Will replay at 50% or more)")
                .FlagMusic(true)
                .Do(e => playlist[e.User.VoiceChannel.Id].Encore(e));

            // Moderator commands
            group.CreateCommand("forceskip")
                .MinPermissions(1)
                .Parameter("count", Commands.ParameterType.Optional)
                .FlagMusic(true)
                .Description("I'll skip the currently playing song(s).")
                .Do(e =>
                {
                    int count;
                    playlist[e.User.VoiceChannel.Id].SkipSongs(e.Args.Any() && int.TryParse(e.Args[0], out count) ? count : 1);
                    e.Channel.SendMessage("Forcefully skipping...");
                });

            group.CreateCommand("forcereset")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll reset the stream in case of bugs, while keeping the playlist intact.")
                .Do(async e =>
                {
                    await e.Channel.SendMessage("Reseting stream...");
                    await streams.Reset(e.User.VoiceChannel);
                });

            group.CreateCommand("pause")
                .Alias("unpause")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll toggle pause on the stream")
                .Do(e => playlist[e.User.VoiceChannel.Id].Pause(e));

            // Administrator commands
            group.CreateCommand("music")
                .Parameter("on/off", Commands.ParameterType.Required)
                .Description("I'll start or end a stream in a particular voice channel, which you need to be in.")
                .MinPermissions(2)
                .Do(e =>
                {
                    if (e.User.VoiceChannel == null) e.Channel.SendMessage($"{e.User.Mention}, you need to be in a voice channel to use this.");
                    else Helpers.OnOffCmd(e, async on =>
                    {
                        var stream = streams.Get(e.User.VoiceChannel);
                        string status = on ? "start" : "halt";
                        if ((stream != null) == on)
                        {
                            if (on && stream.Request) // The user is switching back to normal streaming mode.
                            {
                                await e.Channel.SendMessage("Switching to normal streaming mode.");
                                await streams.Play(e, false, stream);
                            }
                            else
                            {
                                string blah = on ? "streaming in! Did you mean to !reset or !forcereset the stream?" : "not streaming in!";
                                await e.Channel.SendMessage($"{e.User.Mention}, I can't {status} streaming in a channel that I'm already {blah}");
                            }
                        }
                        else
                        {
                            await e.Channel.SendMessage($"{e.User.Mention}, I'm {status}ing the stream!");
                            if (on) await streams.Play(e, false);
                            else streams.Get(e.User.VoiceChannel).Stop();
                        }
                    });
                });

            if (HasFolder()) // Request-driven mode is always on when we don't have a folder, therefore we won't need this command.
            {
                group.CreateCommand("music request")
                    .Description("I'll turn request-driven streaming on in a particular voice channel, which you need to be in.")
                    .MinPermissions(2)
                    .Do(e =>
                    {
                        var stream = streams.Get(e.User.VoiceChannel);
                        if (stream != null && stream.Request)
                        {
                            e.Channel.SendMessage("The stream is already in request mode.");
                            return;
                        }
                        Task.Run(() => streams.Play(e, true, stream));
                        e.Channel.SendMessage("I am now streaming in request-driven mode.");
                    });
            }
        }
    }
}
