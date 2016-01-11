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
                if (reset)
                {
                    reset = false;
                    return true;
                }
                RemoveAt(0);
                return false;
            }

            #region Information
            bool InPlaylist(string common, bool online = false) => Exists(song => (song.IsOnline == online) && (online ? song.Ext : song.Uri) == common);

            int NonrequestedIndex() => 1 + this.Skip(1).Where(song => song.Nonrequested).Count();

            internal async Task<string> CurrentUri()
            {
                while (!this.Any())
                {
                    if (reset) return null;
                    await Task.Delay(5000);
                }
                lock (this) return this[0].Uri;
            }

            string EmptyPlaylist() =>
                this.Any() ? null : "The playlist is currently empty, use commands to request something.";

            internal string CurrentSong()
            {
                lock(this)
                    return EmptyPlaylist() ?? $"Currently playing: {this[0].Title()}.";
            }

            internal string SongList()
            {
                lock (this)
                {
                    string reply = EmptyPlaylist() ?? "";
                    if (reply != "") return reply;
                    int i = -1;
                    foreach(var t in this)
                    {
                        reply += (++i == 0) ? $"Currently playing: {t.Title()}.\nNext songs:" : $"\n{i} - {t.ExtTitle}";
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
                    skip = true;
            }
            internal async Task Reset(Commands.CommandEventArgs e)
            {
                if (!reset && AddVote(votereset, e, "reset the stream", "resetting stream", "reset"))
                    await ResetStream(e.User.VoiceChannel);
            }
            internal void Pause(Commands.CommandEventArgs e)
            {
                e.Channel.SendMessage($"{(pause ? "Resum" : "Paus")}ing stream...");
                pause = !pause;
            }
            internal async Task ResetStream(Channel c)
            {
                lock (this)
                {
                    pause = false;
                    reset = true;
                }
                await Task.Delay(7500);
                await Stream(c);
            }
            #endregion

            List<ulong> voteskip = new List<ulong>(), votereset = new List<ulong>(), voteencore = new List<ulong>();
            internal bool skip = false, reset = false, pause = false;
        }
        // Music-related variables
        internal static string Folder;
        internal static bool UseSubdirs;
        static List<ulong> streams = new List<ulong>();
        static Dictionary<ulong, Playlist> playlist = new Dictionary<ulong, Playlist>();
        static string[] exts = { ".wma", ".aac", ".mp3", ".m4a", ".wav", ".flac", ".ogg" };

        static bool HasFolder() => Folder.Length != 0;
        static IEnumerable<string> Files() => System.IO.Directory.EnumerateFiles(Folder, "*.*", UseSubdirs ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly).Where(s => exts.Contains(System.IO.Path.GetExtension(s)));

        static Commands.CommandBuilder CreatePLCmd(Commands.CommandGroupBuilder group, string name, string parameter, string description, string alias) => CreatePLCmd(group, name, parameter, description, new[]{alias});
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
            bool Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Track track, bool multiple, bool isplaylist = false)
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
                    if (!isplaylist) e.Channel.SendMessage($"{title} added to the playlist.");
                    return true;
                }
                if (multiple)
                    e.Channel.SendMessage($"{title} is already in the playlist.");
                return false;
            }
            bool Triad(Commands.CommandEventArgs e, SoundCloud.NET.Models.Playlist playlist, bool multiple)
            {
                bool ret = false;
                foreach (var track in playlist.Tracks)
                    ret |= Triad(e, track, false, true);
                if (ret)
                    e.Channel.SendMessage($"The contents of {playlist.Title} by {playlist.User.Username} have been added to the playlist.");
                else if (!multiple)
                    e.Channel.SendMessage($"There is nothing in {playlist.Title} that isn't already in the playlist.");
                return ret;
            }

            public void CreatePermalinkCmd(Commands.CommandGroupBuilder group, string name, string alias, bool is_playlist) => CreatePermalinkCmd(group, name, new[]{alias}, is_playlist);
            public void CreatePermalinkCmd(Commands.CommandGroupBuilder group, string name, string[] aliases, bool is_playlist)
            {
                CreatePLCmd(group, name, $"SoundCloud {(!is_playlist ? "Track" : "Playlist")} Permalink"/*(s)"*/, $"I'll add SoundCloud {(is_playlist ? "playlist" : "")} songs to the playlist!", aliases)
                    .Do(e =>
                    {
                        //MatchCollection m = Regex.Matches(e.Args[0], @"", RegexOptions.IgnoreCase);
                        if (e.Args[0] == ""/*m.Count == 0*/)
                            e.Channel.SendMessage($"{e.User.Mention} No SoundCloud permalink matches.");
                        else //foreach (Match match in m)
                        {
                            if (is_playlist) Triad(e, GetPlaylist(e.Args[0]/*match.Groups[1]*/), false);
                            else Triad(e, GetTrack(e.Args[0]/*match.Groups[1]*/), true);
                        }
                    });
            }
            public void CreateSearchCmd(Commands.CommandGroupBuilder group, string name, string alias, bool is_playlist) => CreateSearchCmd(group, name, new[]{alias}, is_playlist);
            public void CreateSearchCmd(Commands.CommandGroupBuilder group, string name, string[] aliases, bool is_playlist)
            {
                CreatePLCmd(group, name, is_playlist ? "SoundCloud Playlist Keywords" : "song to find",
                    $"I'll search for your {(is_playlist ? "playlist " : "")}request on SoundCloud!\nResults will be considered in order until one not in the playlist is found.", aliases)
                    .Do(e =>
                    {
                        var search = new SoundCloud.NET.SearchParameters { SearchString = string.Join(" ", e.Args), Streamable = true };
                        var container = is_playlist ? (SoundCloud.NET.Models.BaseModel[])SearchPlaylist(search) : SearchTrack(search);
                        if (container.Count() == 0)
                        {
                            e.Channel.SendMessage($"{e.User.Mention} Your request was not found.");
                            return;
                        }
                        foreach (var thing in container)
                            if (is_playlist ? Triad(e, (SoundCloud.NET.Models.Playlist)thing, true) : Triad(e, (SoundCloud.NET.Models.Track)thing, false)) return;
                        e.Channel.SendMessage($"{e.User.Mention} No results for your requested search aren't already in the playlist.");
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
            var _client = await Voice.JoinServer(c);
            if (_client == null) return; // TODO: Remove when voice works.
            if (!playlist.ContainsKey(cid))
                playlist.Add(cid, new Playlist());
            while (streams.Contains(cid))
            {
                var pl = playlist[cid];
                pl.Initialize();
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
                            int byteCount;
                            while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                            {
                                while(pl.pause) await Task.Delay(500); // Play Voice.cs commands in here?
                                if (!streams.Contains(cid) || pl.skip || pl.reset)
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
            await Program.Audio.Leave(c.Server);
        }

        internal static Task StartStreams(DiscordClient client)
        {
            return Task.WhenAll(
              streams.Select(s =>
              {
                  var c = client.GetChannel(s);
                  return c.Type == ChannelType.Voice ? Task.Run(() => Stream(c)) : null;
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

        internal static void StopStream(ulong stream)
        {
            SQL.AddOrUpdateFlag(stream, "music", "0");
            playlist[stream].pause = false;
            streams.Remove(stream);
        }

        internal static async Task StopStreams(Server server)
        {
            var serverstreams = streams.Where(stream => server.GetChannel(stream) != null).ToArray();
            foreach (var stream in serverstreams)
                StopStream(stream);
            if (serverstreams.Length != 0)
                await Task.Delay(5000);
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("playlist")
                .Description("I'll give you the list of songs in the playlist.")
                .FlagMusic(true)
                .Do(e => e.Channel.SendMessage(playlist[e.User.VoiceChannel.Id].SongList()));

            group.CreateCommand("song")
                .Description("I'll tell you the song I'm currently playing.")
                .FlagMusic(true)
                .Do(e => e.Channel.SendMessage(playlist[e.User.VoiceChannel.Id].CurrentSong()));

            CreatePLCmd(group, "ytrequest", "youtube video link(s)", "I'll add youtube videos to the playlist")
                .Do(e =>
                {
                    var rclient = Helpers.GetRestClient("http://www.youtubeinmp3.com/fetch/");
                    MatchCollection m = Regex.Matches(e.Args[0], @"youtu(?:be\.com\/(?:v\/|e(?:mbed)?\/|watch\?v=)|\.be\/)([\w-_]{11}\b)", RegexOptions.IgnoreCase);
                    foreach (var link in from Match match in m select $"youtube.com/watch?v={match.Groups[1]}")
                    {
                        Tuple<string,string> uri_title;
                        try { var video = YouTube.Default.GetVideo(link); uri_title = Tuple.Create(video.Uri, video.Title); }
                        catch
                        {
                            // Content is sometimes an html page instead of JSON, we should ask why.
                            var json = Newtonsoft.Json.Linq.JObject.Parse(rclient.Execute(new RestSharp.RestRequest($"?format=JSON&video={System.Net.WebUtility.UrlEncode(link)}", RestSharp.Method.GET)).Content);
                            uri_title = Tuple.Create(json["link"].ToString(), json["title"].ToString());
                        }
                        e.Channel.SendMessage(playlist[e.User.VoiceChannel.Id].TryInsert(new Song(uri_title.Item1, Song.EType.Youtube, e.User, $"{uri_title.Item2} ({link})"))
                            ? $"{uri_title.Item2} added to the playlist."
                            : $"{e.User.Mention} Your request ({uri_title.Item2}) is already in the playlist.");
                    }
                    if (m.Count == 0)
                        e.Channel.SendMessage($"None of {e.Args[0]} could be added to playlist because no valid youtube links were found within.");
                });

            if (Program.config["SoundCloud"].HasValues)
            {
                SC sc = new SC(Program.config["SoundCloud"]["client_id"].ToString(), Console.Title);
                sc.CreateSearchCmd(group, "scsearch", "scs", false);
                sc.CreatePermalinkCmd(group, "screquest", new[]{"sctrack", "sctr"}, false);
                sc.CreatePermalinkCmd(group, "scplaylist", "scpl", false);
                //sc.CreateSearchCmd(group, "scplsearch", "scpls", true); // Until this stops giving Gateway timeouts, RIP.
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
                .FlagMusic(true)
                .Description("I'll skip the currently playing song(s).")
                .Do(e =>
                {
                    playlist[e.User.VoiceChannel.Id].skip = true;
                    e.Channel.SendMessage("Forcefully skipping...");
                });

            group.CreateCommand("forcereset")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll reset the stream in case of bugs, while keeping the playlist intact.")
                .Do(async e =>
                {
                    await e.Channel.SendMessage("Reseting stream...");
                    await playlist[e.User.VoiceChannel.Id].ResetStream(e.User.VoiceChannel);
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
                    if (e.User.VoiceChannel?.Id <= 0) e.Channel.SendMessage($"{e.User.Mention}, you need to be in a voice channel to use this.");
                    else Helpers.OnOffCmd(e, async on =>
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
                            await e.Channel.SendMessage($"{e.User.Mention}, I'm {status}ing the stream!");
                            if (on)
                            {
                                SQL.AddOrUpdateFlag(e.User.VoiceChannel.Id, "music", "1");
                                await StopStreams(e.Server);
                                streams.Add(e.User.VoiceChannel.Id);
                                await Stream(e.User.VoiceChannel);
                            }
                            else StopStream(e.User.VoiceChannel.Id);
                        }
                    });
                });
        }
    }
}
