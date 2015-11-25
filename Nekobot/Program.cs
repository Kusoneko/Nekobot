using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Nekobot.Commands;
using Nekobot.Commands.Permissions.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using TagLib;
using System.Data.SQLite;
using NAudio.Wave;
using Discord.Audio;
using VideoLibrary;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;

namespace Nekobot
{
    class Program
    {
        // Commands first to help with adding new commands
        static void GenerateCommands(CommandGroupBuilder group)
        {
            group.DefaultMusicFlag(false);
            group.DefaultNsfwFlag(false);

            // User commands
            group.CreateCommand("ping")
                .Description("I'll reply with 'Pong!'")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, $"<@{e.User.Id}>, Pong!");
                });

            group.CreateCommand("whois")
                .Alias("getinfo")
                .Parameter("@User1", Commands.ParameterType.Required)
                .Parameter("@User2", Commands.ParameterType.Optional)
                .Parameter("@UserN", Commands.ParameterType.Multiple)
                .Description("I'll give you information about the mentioned user(s).")
                .Do(async e =>
                {
                    string reply = "";
                    foreach (User u in e.Message.MentionedUsers)
                    {
                        if (u.Id == 63296013791666176 && e.User.Id == 63299786798796800)
                        {
                            reply += $@"
<@{u.Id}> is your onii-chan <3 and his id is {u.Id} and his permission level is {GetPermissions(u, e.Channel).ToString()}.
";
                        }
                        else
                        {
                            reply += $@"
<@{u.Id}>'s id is {u.Id} and their permission level is {GetPermissions(u, e.Channel).ToString()}.
";
                        }
                    }
                    await client.SendMessage(e.Channel, reply);
                });

            group.CreateCommand("playlist")
                .Description("I'll give you the list of songs in the playlist.")
                .FlagMusic(true)
                .Do(async e =>
                {
                    string reply = "";
                    if (playlist[e.User.VoiceChannel.Id][0].Item2 == "Youtube")
                        reply = $@"Currently playing: {playlist[e.User.VoiceChannel.Id][0].Item4}.
Next songs:";
                    else
                    {
                        TagLib.File song = File.Create(playlist[e.User.VoiceChannel.Id][0].Item1);
                        string title = "";
                        if (song.Tag.Title != null && song.Tag.Title != "")
                        {
                            if (song.Tag.Performers != null)
                            {
                                foreach (string p in song.Tag.Performers)
                                {
                                    title += $", {p}";
                                }
                            }
                            if (title != "")
                                title += " **-** ";
                            title += song.Tag.Title;
                            title = title.Substring(2);
                            reply = $@"Currently playing: {title}.
Next songs:";
                        }
                        else
                        {
                            reply = $@"Currently playing: {System.IO.Path.GetFileNameWithoutExtension(playlist[e.User.VoiceChannel.Id][0].Item1)}.
Next songs:";
                        }
                    }
                    for(int i = 1; i < 11; i++)
                    {
                        if (playlist[e.User.VoiceChannel.Id][i].Item2 == "Request")
                        {
                            TagLib.File songfile = File.Create(playlist[e.User.VoiceChannel.Id][i].Item1);
                            string title = "";
                            if (songfile.Tag.Title != null && songfile.Tag.Title != "")
                            {
                                if (songfile.Tag.Performers != null)
                                {
                                    foreach (string p in songfile.Tag.Performers)
                                    {
                                        title += $", {p}";
                                    }
                                }
                                if (title != "")
                                    title += " **-** ";
                                title += songfile.Tag.Title;
                                title = title.Substring(2);
                                reply += $@"
{i.ToString()} - **[{playlist[e.User.VoiceChannel.Id][i].Item2} by <@{playlist[e.User.VoiceChannel.Id][i].Item3}>]** {title}";
                            }
                            else
                            {
                                reply += $@"
{i.ToString()} - **[{playlist[e.User.VoiceChannel.Id][i].Item2} by <@{playlist[e.User.VoiceChannel.Id][i].Item3}>]** {System.IO.Path.GetFileNameWithoutExtension(playlist[e.User.VoiceChannel.Id][i].Item1)}";
                            }
                        }
                        else if (playlist[e.User.VoiceChannel.Id][i].Item2 == "Youtube")
                        {
                            reply += $@"
{i.ToString()} - **[{playlist[e.User.VoiceChannel.Id][i].Item2} request by <@{playlist[e.User.VoiceChannel.Id][i].Item3}>]** {playlist[e.User.VoiceChannel.Id][i].Item4}";
                        }
                        else
                        {
                            TagLib.File songfile = File.Create(playlist[e.User.VoiceChannel.Id][i].Item1);
                            string title = "";
                            if (songfile.Tag.Title != null && songfile.Tag.Title != "")
                            {
                                if (songfile.Tag.Performers != null)
                                {
                                    foreach (string p in songfile.Tag.Performers)
                                    {
                                        title += $", {p}";
                                    }
                                }
                                if (title != "")
                                    title += " **-** ";
                                title += songfile.Tag.Title;
                                title = title.Substring(2);
                                reply += $@"
{i.ToString()} - **[{playlist[e.User.VoiceChannel.Id][i].Item2}]** {title}";
                            }
                            else
                            {
                                reply += $@"
{i.ToString()} - **[{playlist[e.User.VoiceChannel.Id][i].Item2}]** {System.IO.Path.GetFileNameWithoutExtension(playlist[e.User.VoiceChannel.Id][i].Item1)}";
                            }
                        }
                    }
                    await client.SendMessage(e.Channel, reply);
                });

            group.CreateCommand("song")
                .Description("I'll tell you the song I'm currently playing.")
                .FlagMusic(true)
                .Do(async e =>
                {
                    string reply = "";
                    if (playlist[e.User.VoiceChannel.Id][0].Item2 == "Youtube")
                    {
                        reply = $@"Currently playing: {playlist[e.User.VoiceChannel.Id][0].Item4}.";
                    }
                    else
                    {
                        TagLib.File song = File.Create(playlist[e.User.VoiceChannel.Id][0].Item1);
                        string title = "";
                        if (song.Tag.Title != null && song.Tag.Title != "")
                        {
                            if (song.Tag.Performers != null)
                            {
                                foreach (string p in song.Tag.Performers)
                                {
                                    title += $", {p}";
                                }
                            }
                            if (title != "")
                                title += " **-** ";
                            title += song.Tag.Title;
                            title = title.Substring(2);
                            reply = $@"Currently playing: {title}.";
                        }
                        else
                        {
                            reply = $@"Currently playing: {System.IO.Path.GetFileNameWithoutExtension(playlist[e.User.VoiceChannel.Id][0].Item1)}.";
                        }
                    }
                    await client.SendMessage(e.Channel, reply);
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
                                await client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist at position {(songindex).ToString()}.");
                                return;
                            }
                            playlist[e.User.VoiceChannel.Id].Insert(index, Tuple.Create<string, string, long, string>(video.Uri, "Youtube", e.User.Id, e.Args[0]));
                            await client.SendMessage(e.Channel, $"{video.Title} added to the playlist.");
                        //}
                        //else
                        //{
                        //    await client.SendMessage(e.Channel, $"{video.Title} couldn't be added to the playlist because of unsupported fileformat: {video.FileExtension}.");
                        //}
                    }
                    else
                    {
                        await client.SendMessage(e.Channel, $"{e.Args[0]} couldn't be added to playlist because it's not a valid youtube link.");
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
                                await client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request is already in the playlist at position {(songindex).ToString()}.");
                                return;
                            }
                            playlist[e.User.VoiceChannel.Id].Insert(index, Tuple.Create<string, string, long, string>(files.ElementAt(j).File, "Request", e.User.Id, null));
                            await client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request has been added to the list.");
                            requestfound = true;
                            break;
                        }
                    }
                    if (!requestfound)
                    {
                        await client.SendMessage(e.Channel, $"<@{e.User.Id}> Your request was not found.");
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
                            await client.SendMessage(e.Channel, $"{voteskip[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to skip current song. 50%+ achieved, skipping song...");
                            skip[e.User.VoiceChannel.Id] = true;
                        }
                        else
                        {
                            await client.SendMessage(e.Channel, $"{voteskip[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to skip current song. (Needs 50% or more to skip)");
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
                            await client.SendMessage(e.Channel, $"{votereset[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to reset the stream. 50%+ achieved, resetting stream...");
                            reset[e.User.VoiceChannel.Id] = true;
                            await Task.Delay(5000);
                            await StreamMusic(e.User.VoiceChannel.Id);
                        }
                        else
                        {
                            await client.SendMessage(e.Channel, $"{votereset[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to reset the stream. (Needs 50% or more to reset)");
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
                            await client.SendMessage(e.Channel, $"{voteencore[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to replay current song. 50%+ achieved, song will be replayed...");
                            playlist[e.User.VoiceChannel.Id].Insert(1, Tuple.Create(playlist[e.User.VoiceChannel.Id][0].Item1, "Encore", playlist[e.User.VoiceChannel.Id][0].Item3, playlist[e.User.VoiceChannel.Id][0].Item4));
                        }
                        else
                        {
                            await client.SendMessage(e.Channel, $"{voteencore[e.User.VoiceChannel.Id].Count}/{CountVoiceChannelMembers(e.User.VoiceChannel)} votes to replay current song. (Needs 50% or more to replay)");
                        }
                    }
                });

            group.CreateCommand("nsfw status")
                .Alias("canlewd status")
                .Description("I'll tell you if this channel allows nsfw commands.")
                .Do(async e =>
                {
                    bool nsfw = GetNsfwFlag(e.Channel);
                    if (nsfw)
                        await client.SendMessage(e.Channel, "This channel allows nsfw commands.");
                    else
                        await client.SendMessage(e.Channel, "This channel doesn't allow nsfw commands.");
                });

            group.CreateCommand("quote")
                .Description("I'll give you a random quote from https://inspiration.julxzs.website/quotes")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("https://inspiration.julxzs.website");
                    var request = new RestRequest("api/quote", Method.GET);
                    JObject result = JObject.Parse(rclient.Execute<JObject>(request).Content);
                    string quote = result["quote"]["quote"].ToString();
                    string author = result["quote"]["author"].ToString();
                    string date = result["quote"]["date"].ToString();
                    await client.SendMessage(e.Channel, $"\"{quote}\" - {author} {date}");
                });

            group.CreateCommand("version")
                .Description("I'll tell you the current version and check if a newer version is available.")
                .Do(async e =>
                {
                    string[] versions = version.Split('.');
                    rclient.BaseUrl = new Uri("https://raw.githubusercontent.com");
                    var request = new RestRequest("Kusoneko/Nekobot/master/version.json", Method.GET);
                    JObject result = JObject.Parse(rclient.Execute<JObject>(request).Content);
                    string remoteversion = result["version"].ToString();
                    string[] remoteversions = remoteversion.Split('.');
                    if (int.Parse(versions[0]) < int.Parse(remoteversions[0]))
                    {
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(remoteversions[0]) - int.Parse(versions[0])).ToString()} major version(s) behind. (Current version: {version}, latest version: {remoteversion})");
                    }
                    else if (int.Parse(versions[0]) > int.Parse(remoteversions[0]))
                    {
                        await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(versions[0]) - int.Parse(remoteversions[0])).ToString()} major version(s) ahead. (Current version: {version}, latest released version: {remoteversion})");
                    }
                    else
                    {
                        if (int.Parse(versions[1]) < int.Parse(remoteversions[1]))
                        {
                            await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(remoteversions[1]) - int.Parse(versions[1])).ToString()} minor version(s) behind. (Current version: {version}, latest version: {remoteversion})");
                        }
                        else if (int.Parse(versions[1]) > int.Parse(remoteversions[1]))
                        {
                            await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(versions[1]) - int.Parse(remoteversions[1])).ToString()} minor version(s) ahead. (Current version: {version}, latest released version: {remoteversion})");
                        }
                        else
                        {
                            if (int.Parse(versions[2]) < int.Parse(remoteversions[2]))
                            {
                                await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(remoteversions[2]) - int.Parse(versions[2])).ToString()} patch(es) behind. (Current version: {version}, latest version: {remoteversion})");
                            }
                            else if (int.Parse(versions[2]) > int.Parse(remoteversions[2]))
                            {
                                await client.SendMessage(e.Channel, $"I'm currently {(int.Parse(versions[2]) - int.Parse(remoteversions[2])).ToString()} patch(es) ahead. (Current version: {version}, latest released version: {remoteversion})");
                            }
                            else
                            {
                                await client.SendMessage(e.Channel, $"I'm up to date! (Current version: {version})");
                            }
                        }
                    }
                });

            group.CreateCommand("neko")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/neko/")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, LewdSX("neko"));
                });

            group.CreateCommand("qt")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/qt/")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, LewdSX("qt"));
                });

            group.CreateCommand("kitsune")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/kitsune/")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, LewdSX("kitsune"));
                });

            group.CreateCommand("lewd")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/lewd/")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, LewdSX("lewd"));
                });

            if (pitur != "")
            {
                group.CreateCommand("pitur")
                    .FlagNsfw(true)
                    .Description("I'll give you a random lewd image from pitur's hentai collection")
                    .Do(async e =>
                    {
                        await client.SendFile(e.Channel, ImageFolders(pitur));
                    });
            }

            if (gold != "")
            {
                group.CreateCommand("gold")
                    .FlagNsfw(true)
                    .Description("I'll give you a random kancolle image from gold's collection")
                    .Do(async e =>
                    {
                        await client.SendFile(e.Channel, ImageFolders(gold));
                    });
            }

            if (cosplay != "")
            {
                group.CreateCommand("cosplay")
                    .FlagNsfw(true)
                    .Description("I'll give you a random cosplay image from Salvy's collection")
                    .Do(async e =>
                    {
                        await client.SendFile(e.Channel, ImageFolders(cosplay));
                    });
            }

            group.CreateCommand("safebooru")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from safebooru.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("safebooru", String.Join("%20", e.Args)));
                });

            group.CreateCommand("gelbooru")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Hide() // Disabled because of them disabling their API
                .Description("I'll give you a random image of the tags you entered from gelbooru.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("gelbooru", String.Join("%20", e.Args)));
                });

            group.CreateCommand("rule34")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from rule34.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("rule34", String.Join("%20", e.Args)));
                });

            group.CreateCommand("konachan")
                .Alias("kona")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from konachan.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("konachan", String.Join("%20", e.Args)));
                });

            group.CreateCommand("yandere")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from yandere.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("yandere", String.Join("%20", e.Args)));
                });

            group.CreateCommand("lolibooru")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from lolibooru.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("lolibooru", String.Join("%20", e.Args)));
                });

            group.CreateCommand("e621")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image from e621 (optionally with tags)")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, ImageBooru("e621", String.Join("%20", e.Args)));
                });

            group.CreateCommand("fortune")
                .Description("I'll give you a fortune!")
                .Do(async e =>
                {
                    string[] fortunes = new string[] { "Don't sleep for too long, or you'll miss naptime!", "Before crying over spilt milk, remember it can still be delicious without a bowl.", "A bird in the paw is worth nom nom nom...", "Let no surface, no matter how high or cluttered, go unexplored.", "Neko never catches the laser if neko never tries.", "Our greatest glory is not in never falling, but in making sure master doesn't find the mess.", "A mouse shared halves the food but doubles the happiness.", "There exists nary a toy as pertinent as the box from whence that toy came.", "Neko will never be fed if neko does not meow all day!", "Ignore physics, and physics will ignore you.", "Never bite the hand that feeds you!", "Before finding the red dot, you must first find yourself.", "Some see the glass half empty. Some see the glass half full. Neko sees the glass and knocks it over.", "Make purrs not war.", "Give a neko fish and you feed them for a day; Teach a neko to fish and... mmmm fish.", "Wheresoever you go, go with all of master's things.", "Live your dreams every day! Why do you think neko naps so much?", "The hardest thing of all is to find a black cat in a dark room, especially if there is no cat.", "Meow meow meow meow, meow meow. Meow meow meow." };
                    Random rnd = new Random();
                    await client.SendMessage(e.Channel, fortunes[rnd.Next(0, fortunes.Count())]);
                });

            group.CreateCommand("playeravatar")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll get you the avatar of each Player.me username provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new System.Uri("https://player.me/api/v1/auth");
                    var request = new RestRequest("pre-login", Method.POST);
                    foreach (string s in e.Args)
                    {
                        request.AddQueryParameter("login", s);
                        JObject result = JObject.Parse(rclient.Execute(request).Content);
                        if (Convert.ToBoolean(result["success"]) == false)
                            await client.SendMessage(e.Channel, $"{s} was not found.");
                        else
                        {
                            string avatar = result["results"]["avatar"]["original"].ToString();
                            await client.SendMessage(e.Channel, $"{s}'s avatar: https:{avatar}");
                        }
                    }
                });

            group.CreateCommand("nya")
                .Description("I'll say 'Nyaa~'")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "Nyaa~");
                });

            group.CreateCommand("poi")
                .Description("I'll say 'Poi!'")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "Poi!");
                });

            group.CreateCommand("aicrai")
                .Alias("aicraievritiem")
                .Alias("aicraievritaim")
                .Alias("sadhorn")
                .Alias("icri")
                .Description("When sad things happen...")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=0JAn8eShOo8");
                });

            group.CreateCommand("notnow")
                .Alias("rinpls")
                .Description("How to Rekt: Rin 101")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=2BZUzJfKFwM");
                });

            group.CreateCommand("uninstall")
                .Description("A great advice in any situation.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=iNCXiMt1bR4");
                });

            group.CreateCommand("killyourself")
                .Alias("kys")
                .Description("Another great advice.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=2dbR2JZmlWo");
                });

            group.CreateCommand("congratulations")
                .Alias("congrats")
                .Alias("grats")
                .Description("Congratulate someone for whatever reason.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "https://www.youtube.com/watch?v=oyFQVZ2h0V8");
                });

            group.CreateCommand("say")
                .Alias("forward")
                .Parameter("text...", Commands.ParameterType.Multiple)
                .Description("I'll repeat what you said.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, String.Join(" ", e.Args));
                });

            group.CreateCommand("reverse")
                .Parameter("text...", Commands.ParameterType.Multiple)
                .Description("I'll repeat what you said, in reverse!")
                .Do(async e =>
                {
                    char[] chars = String.Join(" ", e.Args).ToArray();
                    Array.Reverse(chars);
                    await client.SendMessage(e.Channel, new string(chars));
                });

            group.CreateCommand("whereami")
                .Alias("channelinfo")
                .Alias("location")
                .Description("I'll tell you information about the channel and server you're asking me this from.")
                .Do(async e =>
                {
                    if (e.Channel.IsPrivate)
                        await client.SendMessage(e.Channel, "You're in a private message with me, baka.");
                    else
                    {
                        string message = $@"You are currently in {e.Channel.Name} (id: {e.Channel.Id})
on server **{e.Server.Name}** (id: {e.Server.Id}) (region: {e.Server.Region})
owned by {e.Server.Owner.Name} (id {e.Server.Owner.Id}).";
                        if (e.Channel.Topic != "" || e.Channel.Topic != null)
                            message = message + $@"
The current topic is: {e.Channel.Topic}";
                        await client.SendMessage(e.Channel, message);
                    }
                });

            group.CreateCommand("avatar")
                .Parameter("@User1", Commands.ParameterType.Required)
                .Parameter("@User2", Commands.ParameterType.Optional)
                .Parameter("@UserN", Commands.ParameterType.Multiple)
                .Description("I'll give you the avatars of every mentioned users.")
                .Do(async e =>
                {
                    foreach (User u in e.Message.MentionedUsers)
                    {
                        if (u.AvatarUrl == null)
                            await client.SendMessage(e.Channel, $"<@{u.Id}> has no avatar.");
                        else
                            await client.SendMessage(e.Channel, $"<@{u.Id}>'s avatar is: https://discordapp.com/api/{u.AvatarUrl}");
                    }
                });

            group.CreateCommand("rand")
                .Parameter("min", Commands.ParameterType.Optional)
                .Parameter("max", Commands.ParameterType.Optional)
                .Description("I'll give you a random number between *min* and *max*. Both are optional. If only one is given, it's *max*. (defaults: 1-100)")
                .Do(async e =>
                {
                    int min = 1;
                    int max = 101;
                    Random rnd = new Random();
                    string message = "";
                    if (e.Args.Count() == 2)
                    {
                        bool valid = true;
                        foreach (string s in e.Args)
                        {
                            int dummy = 0;
                            if (!int.TryParse(s, out dummy))
                            {
                                valid = false;
                                message = $"{s} is not a number!";
                                break;
                            }
                        }
                        if (valid)
                        {
                            min = int.Parse(e.Args[0]);
                            max = int.Parse(e.Args[1]);
                            if (min > max)
                            {
                                int z = min;
                                min = max;
                                max = z;
                            }
                            if (min == max)
                                message = $"You're joking right? It's {min}.";
                            else
                            {
                                message = $"Your number is **{rnd.Next(min, max + 1).ToString()}**.";
                            }
                        }
                    }
                    else if (e.Args.Count() == 1)
                    {
                        bool valid = true;
                        int dummy = 0;
                        if (!int.TryParse(e.Args[0], out dummy))
                        {
                            valid = false;
                            message = $"{e.Args[0]} is not a number!";
                        }
                        if (valid)
                        {
                            max = int.Parse(e.Args[1]);
                            if (min > max) // in case the nup set a lower number than the default min for max
                            {
                                int z = min;
                                min = max;
                                max = z;
                            }
                            if (min == max)
                                message = $"You're joking right? It's {min}.";
                            else
                            {
                                message = $"Your number is **{rnd.Next(min, max + 1).ToString()}**.";
                            }
                        }
                    }
                    else
                    {
                        message = $"Your number is **{rnd.Next(min,max)}**.";
                    }
                    await client.SendMessage(e.Channel, message);
                });

            group.CreateCommand("roll")
                .Parameter("dice", Commands.ParameterType.Optional)
                .Parameter("sides", Commands.ParameterType.Optional)
                .Parameter("times", Commands.ParameterType.Optional)
                .Description("I'll roll a few sided dice for a given number of times. All params are optional. (defaults: 1 *dice*, 6 *sides*, 1 *times*)")
                .Do(async e =>
                {
                    bool rick = false;
                    bool valid = true;
                    foreach (string s in e.Args)
                    {
                        int dummy = 0;
                        if (!int.TryParse(s, out dummy))
                            valid = false;
                        if (s == "rick")
                            rick = true;
                        if (rick || !valid)
                            break;
                    }
                    if (!rick)
                    {
                        if (valid)
                        {
                            int dice = 1;
                            int sides = 6;
                            int times = 1;
                            if (e.Args.Count() == 3)
                            {
                                dice = int.Parse(e.Args[0]);
                                sides = int.Parse(e.Args[1]);
                                times = int.Parse(e.Args[2]);
                            }
                            else if (e.Args.Count() == 2)
                            {
                                dice = int.Parse(e.Args[0]);
                                sides = int.Parse(e.Args[1]);
                            }
                            else if (e.Args.Count() == 1)
                            {
                                dice = int.Parse(e.Args[0]);
                            }
                            int roll = 0;
                            Random rnd = new Random();
                            for (int i = times; i > 0; i--)
                            {
                                for (int j = dice; j > 0; j--)
                                {
                                    roll += rnd.Next(1, sides + 1);
                                }
                            }
                            await client.SendMessage(e.Channel, $"You rolled {dice.ToString()} different {sides.ToString()}-sided dice {times.ToString()} times... Result: **{roll}**");
                        }
                        else
                            await client.SendMessage(e.Channel, $"Arguments are not all numbers!");
                    }
                    else
                        await client.SendMessage(e.Channel, $"https://www.youtube.com/watch?v=dQw4w9WgXcQ");
                });

            group.CreateCommand("lotto")
                .Description("I'll give you a set of 6 lucky numbers!")
                .Do(async e =>
                {
                    List<int> lotto = new List<int>();
                    Random rnd = new Random();
                    while (lotto.Count() < 6)
                    {
                        int number = rnd.Next(1, 60);
                        for (int i = 0; i < lotto.Count(); i++)
                        {
                            if (lotto[i] == number)
                            {
                                lotto.Remove(number);
                                break;
                            }
                        }
                        lotto.Add(number);
                    }
                    await client.SendMessage(e.Channel, $"Your lucky numbers are **{lotto[0].ToString()}, {lotto[1].ToString()}, {lotto[2].ToString()}, {lotto[3].ToString()}, {lotto[4].ToString()}, {lotto[5].ToString()}**.");
                });

            group.CreateCommand("pet")
                .Alias("pets")
                .Parameter("@User1", Commands.ParameterType.Optional)
                .Parameter("@User2", Commands.ParameterType.Optional)
                .Parameter("@UserN", Commands.ParameterType.Multiple)
                .Description("Everyone loves being pet, right!?! Pets each *@user*. Leave empty (or mention me too) to pet me!")
                .Do(async e =>
                {
                    string message = $"<@{e.User.Id}> pets ";
                    if (e.Message.MentionedUsers.Count() == 0 && !e.Message.MentionedRoles.Contains(e.Server.EveryoneRole))
                        message = "*purrs*";
                    else if (e.Message.MentionedRoles.Contains(e.Server.EveryoneRole))
                        message = message + $"{Mention.Everyone()} *purrs*";
                    else
                    {
                        foreach (User u in e.Message.MentionedUsers)
                        {
                            message = message + $"<@{u.Id}> ";
                        }
                        if (e.Message.IsMentioningMe)
                        {
                            message = message + "*purrs*";
                        }
                    }
                    await client.SendMessage(e.Channel, message);
                });

            group.CreateCommand("trash")
                .Alias("worstgirl")
                .Alias("onodera")
                .Description("I'll upload an image of 'worst girl'. (WARNING: May cause nausea!)")
                .Do(async e =>
                {
                    await client.SendFile(e.Channel, "images/trash.png");
                });

            group.CreateCommand("doit")
                .Alias("justdoit")
                .Alias("shia")
                .Description("DON'T LET YOUR DREAMS JUST BE DREAMS!")
                .Do(async e =>
                {
                    await client.SendFile(e.Channel, "images/shia.jpg");
                });

            group.CreateCommand("bulli")
                .Alias("bully")
                .Alias("dunbulli")
                .Alias("dontbully")
                .Description("DON'T BULLY!")
                .Do(async e =>
                {
                    await client.SendFile(e.Channel, "images/bulli.jpg");
                });

            group.CreateCommand("8ball")
                .Parameter("question", Commands.ParameterType.Optional)
                .Parameter("?", Commands.ParameterType.Multiple)
                .Description("The magic eightball can answer any question!")
                .Do(async e =>
                {
                    string[] eightball = new string[] { "It is certain.", "It is decidedly so.", "Without a doubt.", "Yes, definitely.", "You may rely on it.", "As I see it, yes.", "Most likely.", "Outlook good.", "Yes.", "Signs point to yes.", "Reply hazy try again...", "Ask again later...", "Better not tell you now...", "Cannot predict now...", "Concentrate and ask again...", "Don't count on it.", "My reply is no.", "My sources say no.", "Outlook not so good.", "Very doubtful.", "Nyas.", "Why not?", "zzzzz...", "No." };
                    Random rnd = new Random();
                    if (String.Join(" ", e.Args)[String.Join(" ", e.Args).Length - 1] != '?')
                        await client.SendMessage(e.Channel, "You must ask a proper question!");
                    else
                        await client.SendMessage(e.Channel, $"*{eightball[rnd.Next(eightball.Length)]}*");
                });

            group.CreateCommand("img")
                .Parameter("search query", Commands.ParameterType.Required)
                .Parameter("extended query", Commands.ParameterType.Multiple)
                .Description("I'll get a random image from Google!")
                .Do(async e =>
                {
                    Random rnd = new Random();
                    rclient.BaseUrl = new Uri("https://ajax.googleapis.com/ajax/services/search");
                    var request = new RestRequest($"images?v=1.0&q={String.Join(" ", e.Args)}&rsz=8&start={rnd.Next(1, 12).ToString()}&safe=active", Method.GET);
                    JObject result = JObject.Parse(rclient.Execute(request).Content);
                    List<string> images = new List<string>();
                    foreach (var element in result["responseData"]["results"])
                    {
                        var image = element["unescapedUrl"];
                        images.Add(image.ToString());
                    }
                    var imageURL = images[rnd.Next(images.Count())].ToString();
                    await client.SendMessage(e.Channel, imageURL);
                });

            group.CreateCommand("hbavatar")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you the hummingbird avatar of the usernames provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("http://hummingbird.me/api/v1/users");
                    string message = "";
                    foreach (string s in e.Args)
                    {
                        var request = new RestRequest($"{s}", Method.GET);
                        if (rclient.Execute(request).Content[0] == '<')
                        {
                            message += $@"
{s} doesn't exist.";
                        }
                        else
                        {
                            JObject result = JObject.Parse(rclient.Execute(request).Content);
                            string username = result["name"].ToString();
                            string avatar = result["avatar"].ToString();
                            message += $@"
{username}'s avatar: {avatar}";
                        }
                    }
                    await client.SendMessage(e.Channel, message);
                });

            group.CreateCommand("hb")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you information on the hummingbird accounts of the usernames provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new Uri("http://hummingbird.me/api/v1/users");
                    foreach (string s in e.Args)
                    {
                        string message = "";
                        var request = new RestRequest($"{s}", Method.GET);
                        if (rclient.Execute(request).Content[0] == '<')
                        {
                            message += $@"{s} doesn't exist.";
                        }
                        else
                        {
                            JObject result = JObject.Parse(rclient.Execute(request).Content);
                            var username = result["name"].ToString();
                            var avatar = result["avatar"].ToString();
                            var userurl = $"http://hummingbird.me/users/{username}";
                            var waifu = result["waifu"].ToString();
                            var waifu_prefix = result["waifu_or_husbando"].ToString();
                            var bio = result["bio"].ToString();
                            var location = result["location"].ToString();
                            var website = result["website"].ToString();
                            var life_spent_on_anime = int.Parse(result["life_spent_on_anime"].ToString());

                            string lifeAnime = CalculateTime(life_spent_on_anime);

                            message += $@"
**User**: {username}
**Avatar**: {avatar} 
**{waifu_prefix}**: {waifu}
**Bio**: {bio}
**Time wasted on Anime**: {lifeAnime}";
                            if (String.IsNullOrWhiteSpace(location))
                                message += $@"
**Location**: {location}";
                            if (String.IsNullOrWhiteSpace(website))
                                message += $@"
**Website**: {website}";
                            message += $@"
**Hummingbird page**: {userurl}";
                        }
                        await client.SendMessage(e.Channel, message);
                    }
                });

            group.CreateCommand("player")
                .Parameter("username1", Commands.ParameterType.Required)
                .Parameter("username2", Commands.ParameterType.Optional)
                .Parameter("username3", Commands.ParameterType.Multiple)
                .Description("I'll give you information on the Player.me of each usernames provided.")
                .Do(async e =>
                {
                    rclient.BaseUrl = new System.Uri("https://player.me/api/v1/auth");
                    var request = new RestRequest("pre-login", Method.POST);
                    foreach (string s in e.Args)
                    {
                        request.AddQueryParameter("login", s);
                        JObject result = JObject.Parse(rclient.Execute(request).Content);
                        if (Convert.ToBoolean(result["success"]) == false)
                            await client.SendMessage(e.Channel, $"{s} was not found.");
                        else
                        {
                            string username = result["results"]["username"].ToString();
                            string avatar = "https:" + result["results"]["avatar"]["original"].ToString();
                            string bio = result["results"]["short_description"].ToString();
                            DateTime date = DateTime.Parse(result["results"]["created_at"].ToString());
                            string joined = date.ToString("yyyy-MM-dd");
                            int followers = Convert.ToInt32(result["results"]["followers_count"]);
                            int following = Convert.ToInt32(result["results"]["following_count"]);
                            await client.SendMessage(e.Channel, $@"
**User**: {username}
**Avatar**: {avatar}
**Bio**: {bio}
**Joined on**: {joined}
**Followers**: {followers}
**Following**: {following}");
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
                    await client.SendMessage(e.Channel, "Forcefully skipping song...");
                });

            group.CreateCommand("forcereset")
                .MinPermissions(1)
                .FlagMusic(true)
                .Description("I'll reset the stream in case of bugs, while keeping the playlist intact.")
                .Do(async e =>
                {
                    reset[e.User.VoiceChannel.Id] = true;
                    await client.SendMessage(e.Channel, "Reseting stream...");
                    await Task.Delay(5000);
                    await StreamMusic(e.User.VoiceChannel.Id);
                });

            group.CreateCommand("nsfw")
                .Alias("canlewd")
                .Parameter("on/off", Commands.ParameterType.Required)
                .MinPermissions(1)
                .Description("I'll set a channel's nsfw flag to on or off.")
                .Do(async e =>
                {
                    if (e.Args[0] == "on")
                    {
                        if (!GetNsfwFlag(e.Channel))
                        {
                            sql = $"select count(channel) from flags where channel='{e.Channel.Id}'";
                            query = new SQLiteCommand(sql, connection);
                            if (Convert.ToInt32(query.ExecuteScalar()) > 0)
                            {
                                sql = $"update flags set nsfw=1 where channel='{e.Channel.Id}'";
                                query = new SQLiteCommand(sql, connection);
                                await query.ExecuteNonQueryAsync();
                            }
                            else
                            {
                                sql = $"insert into flags values ('{e.Channel.Id}', 1, 0, 0)";
                                query = new SQLiteCommand(sql, connection);
                                await query.ExecuteNonQueryAsync();
                            }
                            await client.SendMessage(e.Channel, "I've set this channel to allow nsfw commands.");
                        }
                        else
                        {
                            await client.SendMessage(e.Channel, $"<@{e.User.Id}>, this channel is already allowing nsfw commands.");
                        }
                    }
                    else if (e.Args[0] == "off")
                    {
                        if (GetNsfwFlag(e.Channel))
                        {
                            sql = $"update flags set nsfw=0 where channel='{e.Channel.Id}'";
                            query = new SQLiteCommand(sql, connection);
                            await query.ExecuteNonQueryAsync();
                            await client.SendMessage(e.Channel, "I've set this channel to disallow nsfw commands.");
                        }
                        else
                        {
                            await client.SendMessage(e.Channel, $"<@{e.User.Id}>, this channel is already disallowing nsfw commands.");
                        }
                    }
                    else
                    {
                        await client.SendMessage(e.Channel, $"<@{e.User.Id}>, '{String.Join(" ", e.Args)}' isn't a valid argument. Please use on or off instead.");
                    }
                });

            group.CreateCommand("invite")
                .Parameter("invite code or link", Commands.ParameterType.Required)
                .MinPermissions(1)
                .Description("I'll join a new server using the provided invite code or link.")
                .Do(async e =>
                {
                    await client.AcceptInvite(client.GetInvite(e.Args[0]).Result);
                });

            // Administrator commands

            group.CreateCommand("music")
                .Parameter("on/off", Commands.ParameterType.Required)
                .Description("I'll start or end a stream in a particular voice channel, which you need to be in.")
                .MinPermissions(2)
                .Do(async e =>
                {
                    if (e.User.VoiceChannel?.Id <= 0)
                    {
                        await client.SendMessage(e.Channel, $"<@{e.User.Id}>, you need to be in a voice channel to use this.");
                    }
                    else
                    {
                        if (e.Args[0] == "on")
                        {
                            if (!streams.Contains(e.User.VoiceChannel.Id))
                            {
                                streams.Add(e.User.VoiceChannel.Id);
                                sql = $"select count(channel) from flags where channel = '{e.User.VoiceChannel.Id}'";
                                query = new SQLiteCommand(sql, connection);
                                if (Convert.ToInt32(query.ExecuteScalar()) > 0)
                                {
                                    sql = $"update flags set music=1 where channel='{e.User.VoiceChannel.Id}'";
                                    query = new SQLiteCommand(sql, connection);
                                    query.ExecuteNonQuery();
                                }
                                else
                                {
                                    sql = $"insert into flags values ('{e.User.VoiceChannel.Id}', 0, 1, 0)";
                                    query = new SQLiteCommand(sql, connection);
                                    query.ExecuteNonQuery();
                                }
                                await client.SendMessage(e.Channel, $"<@{e.User.Id}>, I'm starting the stream!");
                                await StreamMusic(e.User.VoiceChannel.Id);
                            }
                            else
                            {
                                await client.SendMessage(e.Channel, $"<@{e.User.Id}>, I can't start streaming in a channel that I'm already streaming in! Did you mean to !reset or !forcereset the stream?");
                            }
                        }
                        else if (e.Args[0] == "off")
                        {
                            if (streams.Contains(e.User.VoiceChannel.Id))
                            {
                                streams.Remove(e.User.VoiceChannel.Id);
                                sql = $"update flags set music=0 where channel='{e.User.VoiceChannel.Id}'";
                                query = new SQLiteCommand(sql, connection);
                                query.ExecuteNonQuery();
                                await client.SendMessage(e.Channel, $"<@{e.User.Id}>, I'm stopping the stream!");
                            }
                            else
                            {
                                await client.SendMessage(e.Channel, $"<@{e.User.Id}>, I can't stop streaming in a channel that I'm already not streaming in!");
                            }
                        }
                        else
                        {
                            await client.SendMessage(e.Channel, $"<@{e.User.Id}>, the argument needs to be either on or off.");
                        }
                    }
                });

            group.CreateCommand("setpermissions")
                .Alias("setperms")
                .Alias("setauth")
                .Parameter("newPermissionLevel", Commands.ParameterType.Required)
                .Parameter("@User1", Commands.ParameterType.Required)
                .Parameter("@User2", Commands.ParameterType.Optional)
                .Parameter("@UserN", Commands.ParameterType.Multiple)
                .MinPermissions(2)
                .Description("I'll set the permission level of the mentioned people to the level mentioned (cannot be higher than or equal to yours).")
                .Do(async e =>
                {
                    int newPermLevel = 0;
                    if (e.Args.Count() < 2 || e.Message.MentionedUsers.Count() < 1)
                        await client.SendMessage(e.Channel, "You need to at least specify a permission level and mention one user.");
                    else if (!int.TryParse(e.Args[0], out newPermLevel))
                        await client.SendMessage(e.Channel, "The first argument needs to be the new permission level.");
                    else
                    {
                        string reply = "";
                        foreach (User u in e.Message.MentionedUsers)
                        {
                            if (GetPermissions(u, e.Channel) != newPermLevel)
                            {
                                sql = $"select count(user) from users where user='{u.Id}'";
                                query = new SQLiteCommand(sql, connection);
                                if (Convert.ToInt32(query.ExecuteScalar()) > 0)
                                {
                                    sql = $"update users set perms={newPermLevel} where user='{u.Id}'";
                                    query = new SQLiteCommand(sql, connection);
                                    await query.ExecuteNonQueryAsync();
                                    if (reply == "")
                                        reply = reply + $@"<@{u.Id}>'s permission level is now {newPermLevel}.";
                                    else
                                        reply = reply + $@"
<@{u.Id}>'s permission level is now {newPermLevel}.";
                                }
                                else
                                {
                                    sql = $"insert into users values ('{u.Id}', {newPermLevel}, 0)";
                                    query = new SQLiteCommand(sql, connection);
                                    await query.ExecuteNonQueryAsync();
                                    if (reply == "")
                                        reply = reply + $@"<@{u.Id}>'s permission level is now {newPermLevel}.";
                                    else
                                        reply = reply + $@"
<@{u.Id}>'s permission level is now {newPermLevel}.";
                                }
                            }
                            else
                            {
                                if (reply == "")
                                    reply = reply + $@"<@{u.Id}>'s permission level is already at {newPermLevel}.";
                                else
                                    reply = reply + $@"
<@{u.Id}>'s permission level is already at {newPermLevel}.";
                            }
                        }
                        await client.SendMessage(e.Channel, reply);
                    }
                });

            // Owner commands

            group.CreateCommand("leave")
                .MinPermissions(3)
                .Description("I'll leave the server this command was used in.")
                .Do(async e =>
                {
                    await client.SendMessage(e.Channel, "Bye bye!");
                    await client.LeaveServer(e.Server);
                });

            group.CreateCommand("color")
                .Parameter("Rolename", Commands.ParameterType.Required)
                .Parameter("hex", Commands.ParameterType.Optional)
                .Parameter("r", Commands.ParameterType.Optional)
                .Parameter("g", Commands.ParameterType.Optional)
                .Parameter("b", Commands.ParameterType.Optional)
                .MinPermissions(3)
                .Description("I'll set a role's color to the hex(000000-FFFFFF) or rgb(0-255 0-255 0-255) color value provided.")
                .Do(async e =>
                {
                    if (e.Args.Count() == 2)
                    {
                        // assume hex code was provided
                        string r = e.Args[1].Substring(0, 2);
                        string g = e.Args[1].Substring(2, 2);
                        string b = e.Args[1].Substring(4, 2);
                        int red = Convert.ToInt32(r, 16);
                        int green = Convert.ToInt32(g, 16);
                        int blue = Convert.ToInt32(b, 16);
                        Role role = client.FindRoles(e.Server, e.Args[0]).FirstOrDefault();
                        Discord.Color color = new Color(0);
                        color.R = Convert.ToByte(red);
                        color.B = Convert.ToByte(blue);
                        color.G = Convert.ToByte(green);
                        await client.EditRole(role, color: color);
                        await client.SendMessage(e.Channel, $"Role {role.Name}'s color has been changed.");
                    }
                    else if (e.Args.Count() == 4)
                    {
                        // assume it's rgb color codes
                        int red = int.Parse(e.Args[1]);
                        int green = int.Parse(e.Args[2]);
                        int blue = int.Parse(e.Args[3]);
                        Role role = client.FindRoles(e.Server, e.Args[0]).FirstOrDefault();
                        Discord.Color color = new Color(0);
                        color.R = Convert.ToByte(red);
                        color.B = Convert.ToByte(blue);
                        color.G = Convert.ToByte(green);
                        await client.EditRole(role, color: color);
                        await client.SendMessage(e.Channel, $"Role {role.Name}'s color has been changed.");
                    }
                    else
                        await client.SendMessage(e.Channel, "The parameters are invalid.");
                });

            group.CreateCommand("ignore")
                .Parameter("channel", Commands.ParameterType.Optional)
                .Parameter("user", Commands.ParameterType.Optional)
                .Parameter("...", Commands.ParameterType.Multiple)
                .MinPermissions(3)
                .Description("I'll ignore commands coming from a particular channel or user")
                .Do(async e =>
                {
                    if (e.Message.MentionedChannels.Count() > 0 || e.Message.MentionedUsers.Count() > 0)
                    {
                        string reply = "";
                        Action<string> setreply = x => reply = x;
                        foreach (Channel c in e.Message.MentionedChannels)
                        {
                            await SetIgnoredFlag("channel", "flags", c.Id, "0, 0, 1", '#', reply, setreply);
                        }
                        foreach (User u in e.Message.MentionedUsers)
                        {
                            await SetIgnoredFlag("user", "users", u.Id, ", 0, 1", '@', reply, setreply);
                        }
                        await client.SendMessage(e.Channel, reply);
                    }
                    else
                    {
                        await client.SendMessage(e.Channel, "You need to mention at least one user or channel!");
                    }
                });
        }

        // Variables
        static DiscordClient client = new DiscordClient(new DiscordClientConfig { AckMessages = true, EnableVoiceMultiserver = true, VoiceMode = DiscordVoiceMode.Outgoing/*, LogLevel = LogMessageSeverity.Debug*/ });
        static CommandService commands;
        static RestClient rclient = new RestClient();
        static SQLiteConnection connection;
        static SQLiteCommand query;
        static JObject config;
        static JObject versionfile;
        static string sql;
        static long masterId;
        static string email;
        static string password;
        static string musicFolder;
        static string pitur;
        static string gold;
        static string cosplay;
        static string version;
        // Music-related variables
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
            {
                playlist.Add(cid, new List<Tuple<string, string, long, string>>());
            }
            if (!skip.ContainsKey(cid))
            {
                skip.Add(cid, false);
            }
            if (!reset.ContainsKey(cid))
            {
                reset.Add(cid, false);
            }
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
                    for (int i = 0; i < playlist[cid].Count; i++)
                    {
                        if (playlist[cid][i].Item1 == files.ElementAt(mp3).File)
                            isAlreadyInPlaylist = true;
                    }
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

        static void InputThread()
        {
            bool accept = true;
            while (accept)
            {
                string input = Console.ReadLine();
            }
        }

        static void Main(string[] args)
        {
            // Load up the DB, or create it if it doesn't exist
            LoadDB();
            // Load up the config file
            LoadConfig();
            Console.Title = $"Nekobot v{version}";
            // Load the stream channels
            LoadStreams();
            // Initialize rest client
            RCInit();
            // Set up the events and enforce use of the command prefix
            commands.CommandError += CommandError;
            client.Connected += Connected;
            client.Disconnected += Disconnected;
            client.UserJoined += UserJoined;
            client.LogMessage += LogMessage;
            client.AddService(commands);
            client.AddService(new PermissionLevelService(GetPermissions));
            commands.CreateGroup("", group => GenerateCommands(group));
            // Keep the window open in case of crashes elsewhere... (hopefully)
            Thread input = new Thread(InputThread);
            input.Start();
            // Connection, join server if there is one in config, and start music streams
            try
            {
                client.Run(async() =>
                {
                    await client.Connect(email, password);
                    if (config["server"].ToString() != "")
                    {
                        await client.AcceptInvite(client.GetInvite(config["server"].ToString()).Result);
                    }
                    await StartMusicStreams();
                });
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error: {e.GetBaseException().Message}");
            }
        }


        protected static string CalculateTime(int minutes)
        {
            if (minutes == 0)
                return "No time.";

            int years, months, days, hours = 0;

            hours = minutes / 60;
            minutes %= 60;
            days = hours / 24;
            hours %= 24;
            months = days / 30;
            days %= 30;
            years = months / 12;
            months %= 12;

            string animeWatched = "";

            if (years > 0)
            {
                animeWatched += years;
                if (years == 1)
                    animeWatched += " **year**";
                else
                    animeWatched += " **years**";
            }

            if (months > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += ", ";
                animeWatched += months;
                if (months == 1)
                    animeWatched += " **month**";
                else
                    animeWatched += " **months**";
            }

            if (days > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += ", ";
                animeWatched += days;
                if (days == 1)
                    animeWatched += " **day**";
                else
                    animeWatched += " **days**";
            }

            if (hours > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += ", ";
                animeWatched += hours;
                if (hours == 1)
                    animeWatched += " **hour**";
                else
                    animeWatched += " **hours**";
            }

            if (minutes > 0)
            {
                if (animeWatched.Length > 0)
                    animeWatched += " and ";
                animeWatched += minutes;
                if (minutes == 1)
                    animeWatched += " **minute**";
                else
                    animeWatched += " **minutes**";
            }

            return animeWatched;


        }

        private class ImageBoard : Tuple<string, string, string>
        {
            public ImageBoard(string link, string resource, string post) : base(link, resource, post) { }
            public string link { get { return Item1; } }
            public string resource { get { return Item2; } }
            public string post { get { return Item3; } }
        };
        private static string ImageBooru(string booru, string tags)
        {
            string res1 = $"index.php?page=dapi&s=post&q=index&limit=1&tags={tags}&pid=", post1 = $"/index.php?page=post&s=view&id=";
            string res2 = $"/index.xml?limit=1&tags={tags}&page=", post2 = $"/show/";
            ImageBoard board = null;
            if (booru == "safebooru")
                board = new ImageBoard("http://safebooru.org", res1, post1);
            else if (booru == "gelbooru")
                board = new ImageBoard("http://gelbooru.com", res1, post1);
            else if (booru == "rule34")
                board = new ImageBoard("http://rule34.xxx", res1, post1);
            else if (booru == "konachan")
                board = new ImageBoard("http://konachan.com/post", res2, post2);
            else if (booru == "yandere")
                board = new ImageBoard("https://yande.re/post", res2, post2);
            else if (booru == "lolibooru")
                board = new ImageBoard("http://lolibooru.moe/post", res2, post2);
            else if (booru == "e621")
                board = new ImageBoard("https://e621.net/post", res2, post2);
            for (int i = 10; i != 0; --i)
            {
                try
                {
                    int posts = GetBooruPostCount(board);
                    if (posts == 0)
                        return $@"There is nothing under the tag(s):
{tags.Replace("%20", " ")}
on {booru}. Please try something else.";
                    return GetBooruImageLink(board, posts == 1 ? 0 : (new Random()).Next(1, posts - 1));
                }
                catch (Exception) {}
            }
            return $"Failed ten times, something must be broken with {booru}'s API.";
        }

        private static JObject GetBooruCommon(ImageBoard board, int rnd)
        {
            rclient.BaseUrl = new System.Uri(board.link);
            var request = new RestRequest(board.resource + rnd.ToString(), Method.GET);
            var result = rclient.Execute(request);
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(result.Content);
            string json = JsonConvert.SerializeXmlNode(xml);
            return JObject.Parse(json);
        }

        private static string GetBooruImageLink(ImageBoard board, int rnd)
        {
            JObject res = GetBooruCommon(board, rnd);
            return "**"+board.link+board.post+res["posts"]["post"]["@id"].ToString()+ "** " + res["posts"]["post"]["@file_url"].ToString().Replace(" ", "%20");
        }

        private static int GetBooruPostCount(ImageBoard board)
        {
            JObject res = GetBooruCommon(board, 0);
            return int.Parse(res["posts"]["@count"].ToString());
        }

        private static string ImageFolders(string folder)
        {
            string[] imgexts = new string[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var files = from file in System.IO.Directory.EnumerateFiles($@"{folder}", "*.*").Where(s => imgexts.Contains(System.IO.Path.GetExtension(s.ToLower()))) select new { File = file };
            Random rnd = new Random();
            int img = rnd.Next(0, files.Count());
            return files.ElementAt(img).File;
        }

        private static string LewdSX(string chan)
        {
            rclient.BaseUrl = new Uri("https://lewdchan.com");
            var request = new RestRequest($"{chan}/src/list.php", Method.GET);
            string result = rclient.Execute(request).Content;
            List<string> list = result.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
            Regex re = new Regex(@"([^\s]+(\.(jpg|jpeg|png|gif|bmp)))");
            foreach (Match m in re.Matches(result))
            {
                list.Add(m.Value);
            }
            Random rnd = new Random();
            string image = $"https://lewdchan.com/{chan}/src/{list[rnd.Next(0, list.Count())]}";
            return image;
        }

        private static void RCInit()
        {
            rclient.UserAgent = $"Nekobot {version}";
        }

        private static void LogMessage(object sender, LogMessageEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[{e.Severity}] {e.Source} : {e.Message}");
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

        private static void LoadConfig()
        {
            if (System.IO.File.Exists("config.json"))
                config = JObject.Parse(System.IO.File.ReadAllText(@"config.json"));
            else
            {
                Console.WriteLine("config.json file not found! Unable to initialize Nekobot!");
                connection.Close();
                connection.Dispose();
                Console.ReadKey();
                Environment.Exit(0);
            }
            email = config["email"].ToString();
            password = config["password"].ToString();
            masterId = Convert.ToInt64(config["master"].ToString());
            musicFolder = config["musicFolder"].ToString();
            pitur = config["pitur"].ToString();
            gold = config["gold"].ToString();
            cosplay = config["cosplay"].ToString();
            CommandServiceConfig command_config = new CommandServiceConfig();
            command_config.CommandChars = config["prefix"].ToString().ToCharArray();
            command_config.RequireCommandCharInPrivate = config["prefixprivate"].ToString().Equals("true");
            command_config.RequireCommandCharInPublic = config["prefixpublic"].ToString().Equals("true");
            string helpmode = config["helpmode"].ToString();
            command_config.HelpMode = helpmode.Equals("public") ? HelpMode.Public : helpmode.Equals("private") ? HelpMode.Private : HelpMode.Disable;
            commands = new CommandService(command_config, GetNsfwFlag, GetMusicFlag, GetIgnoredFlag);

            if (System.IO.File.Exists(@"version.json"))
                versionfile = JObject.Parse(System.IO.File.ReadAllText(@"version.json"));
            version = versionfile["version"].ToString();
        }

        private static void LoadDB()
        {
            if (!System.IO.File.Exists("nekobot.db"))
            {
                SQLiteConnection.CreateFile("nekobot.db");
            }
            connection = new SQLiteConnection("Data Source=nekobot.db;Version=3;");
            connection.Open();
            sql = "create table if not exists users (user varchar(17), perms int, ignored int)";
            query = new SQLiteCommand(sql, connection);
            query.ExecuteNonQuery();
            sql = "create table if not exists flags (channel varchar(17), nsfw int, music int, ignored int)";
            query = new SQLiteCommand(sql, connection);
            query.ExecuteNonQuery();
        }

        private static void UserJoined(object sender, UserEventArgs e)
        {
            client.SendMessage(e.Server.DefaultChannel, $"Welcome to {e.Server.Name}, <@{e.User.Id}>!");
        }

        private static void Disconnected(object sender, DisconnectedEventArgs e)
        {
            Console.WriteLine("Disconnected");
        }

        private static void Connected(object sender, EventArgs e)
        {
            Console.WriteLine("Connected.");
        }

        private static void CommandError(object sender, CommandErrorEventArgs e)
        {
            string error = "Command Error : ";
            if (e.Exception == null)
                error += "No Exception, this should be fixed!";
            else
            {
                if (e.Exception.GetType() == typeof(NsfwFlagException))
                {
                    error = "This channel doesn't allow nsfw commands.";
                }
                else if (e.Exception.GetType() == typeof(MusicFlagException))
                {
                    error = "You need to be in a music streaming channel to use this command.";
                }
                else error += e.Exception.GetBaseException().Message;
            }
            client.SendMessage(e.Channel, error);
            //Console.WriteLine(error);
        }

        private static void LoadStreams()
        {
            sql = "select channel from flags where music = 1";
            query = new SQLiteCommand(sql, connection);
            SQLiteDataReader reader = query.ExecuteReader();
            while (reader.Read())
            {
                streams.Add(Convert.ToInt64(reader["channel"].ToString()));
            }
        }

        private static int CountVoiceChannelMembers(Channel chan)
        {
            if (chan.Type != "voice") { return -1; }
            int result = 0;
            foreach (User u in chan.Members)
            {
                if (u.VoiceChannel == chan)
                    result++;
            }
            return result;
        }

        private static bool GetIgnoredFlag(Channel chan, User user)
        {
            return GetIgnoredFlag(chan) || GetIgnoredFlag(user);
        }

        private static bool GetIgnoredFlag(User user)
        {
            return GetIgnoredFlag("user", "users", user.Id);
        }

        private static bool GetIgnoredFlag(Channel chan)
        {
            return GetIgnoredFlag("channel", "flags", chan.Id);
        }

        private static bool GetIgnoredFlag(string row, string table, long id)
        {
            sql = $"select ignored from {table} where {row} = '{id}'";
            query = new SQLiteCommand(sql, connection);
            SQLiteDataReader reader = query.ExecuteReader();
            bool isIgnored = false;
            while (reader.Read())
            {
                if (int.Parse(reader["ignored"].ToString()) == 1)
                    isIgnored = true;
            }
            return isIgnored;
        }

        protected static async Task SetIgnoredFlag(string row, string table, long id, string insertdata, char symbol, string reply, Action<string> setreply)
        {
            sql = $"select count({row}) from {table} where {row}='{id}'";
            query = new SQLiteCommand(sql, connection);
            if (reply != "")
                reply += '\n';
            if (Convert.ToInt32(query.ExecuteScalar()) > 0)
            {
                bool isIgnored = GetIgnoredFlag(row, table, id);
                sql = $"update {table} set ignored={Convert.ToInt32(!isIgnored)} where {row}='{id}'";
                query = new SQLiteCommand(sql, connection);
                await query.ExecuteNonQueryAsync();

                string ignoredstatus = !isIgnored ? "now" : "no longer";
                reply += $"<{symbol}{id}> is {ignoredstatus} ignored.";
            }
            else
            {
                sql = $"insert into {table} values ('{id}'{insertdata})";
                query = new SQLiteCommand(sql, connection);
                await query.ExecuteNonQueryAsync();
                reply += $"<{symbol}{id}> is now ignored.";
            }
            setreply(reply);
        }

        private static bool GetMusicFlag(User user)
        {
            sql = "select channel from flags where music = 1";
            query = new SQLiteCommand(sql, connection);
            SQLiteDataReader reader = query.ExecuteReader();
            bool isInMusicChannel = false;
            List<long> streams = new List<long>();
            while (reader.Read())
            {
                streams.Add(Convert.ToInt64(reader["channel"].ToString()));
            }
            if (user.VoiceChannel != null)
            {
                if (streams.Contains(user.VoiceChannel.Id))
                {
                    isInMusicChannel = true;
                }
            }
            return isInMusicChannel;
        }

        private static bool GetNsfwFlag(Channel chan)
        {
            sql = "select nsfw from flags where channel = '" + chan.Id + "'";
            query = new SQLiteCommand(sql, connection);
            SQLiteDataReader reader = query.ExecuteReader();
            bool isNsfw = false;
            while (reader.Read())
            {
                if (int.Parse(reader["nsfw"].ToString()) == 1)
                    isNsfw = true;
            }
            return isNsfw;
        }

        private static int GetPermissions(User user, Channel channel)
        {
            int PermissionLevel = 0;
            if (user.Id != masterId)
            {
                sql = $"select count(perms) from users where user = '{user.Id}'";
                query = new SQLiteCommand(sql, connection);
                if (Convert.ToInt32(query.ExecuteScalar()) > 0)
                {
                    sql = $"select perms from users where user = '{user.Id}'";
                    query = new SQLiteCommand(sql, connection);
                    SQLiteDataReader reader = query.ExecuteReader();
                    while (reader.Read())
                    {
                        PermissionLevel = int.Parse(reader["perms"].ToString());
                    }
                }
            }
            else
            {
                PermissionLevel = 10;
            }
            return PermissionLevel;
        }
    }
}
