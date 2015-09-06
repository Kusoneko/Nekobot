using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Discord;
using Discord.Helpers;
using System.Xml.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using NAudio.Wave;

namespace Nekobot
{
    class Program
    {
        public static DiscordClient client = new DiscordClient(new DiscordClientConfig { EnableVoice = true, EnableDebug = false });
        public static string email = "";
        public static string pass = "";
        public static List<string> sfw = new List<string> { };
        public static List<string> streams = new List<string> { };
        public static List<string> owner = new List<string> { };
        public static List<string> admins = new List<string> { };
        public static List<string> mods = new List<string> { };
        public static List<string> normal = new List<string> { };
        public static List<List<string>> permissions = new List<List<string>> { normal, mods, admins, owner };
        public static Dictionary<string, List<string>> votes = new Dictionary<string, List<string>>();
        public static Dictionary<string, bool> forceskip = new Dictionary<string, bool>();
        public static Dictionary<string, string> songrequest = new Dictionary<string, string>();
        public static Dictionary<string, List<string>> replay = new Dictionary<string, List<string>>();

        public static void AI(MessageEventArgs e) // empty for now, will be the place to insert the AI-fication stuffs
        {

        }

        static void InputThread()
        {
            bool accept = true;
            while (accept)
            {
                string input = Console.ReadLine();
                switch (input)
                {
                    case "addpermission":
                        Console.Write("Username: ");
                        string username = Console.ReadLine();
                        Console.Write("Permission level: ");
                        int permission;
                        if (!int.TryParse(Console.ReadLine(), out permission))
                        {
                            Console.WriteLine("Permission needs to be a number from 0 to 3.");
                            break;
                        }
                        if (permission < 0 || permission > 3)
                        {
                            Console.WriteLine("Permission needs to be a number from 0 to 3.");
                            break;
                        }
                        User user = client.GetUser(username); // Will need fixing when findusers happens
                        if (user == null)
                        {
                            Console.WriteLine(username + " doesn't exist.");
                            break;
                        }
                        if (permissions[0].Contains(user.Id))
                            permissions[0].Remove(user.Id);
                        if (permissions[1].Contains(user.Id))
                            permissions[1].Remove(user.Id);
                        if (permissions[2].Contains(user.Id))
                            permissions[2].Remove(user.Id);
                        if (permissions[3].Contains(user.Id))
                            permissions[3].Remove(user.Id);
                        permissions[permission].Add(user.Id);
                        UpdatePermissionFiles();
                        break;
                    case "showpermissions":
                        Console.WriteLine("Owners:");
                        foreach (string owners in owner)
                            Console.WriteLine(owners);
                        Console.WriteLine("Admins:");
                        foreach (string admin in admins)
                            Console.WriteLine(admin);
                        Console.WriteLine("Mods:");
                        foreach (string mod in mods)
                            Console.WriteLine(mod);
                        Console.WriteLine("Normal:");
                        foreach (string normals in normal)
                            Console.WriteLine(normals);
                        break;
                    case "showpermfiles":
                        List<string> a = File.ReadAllLines("owner").ToList();
                        List<string> b = File.ReadAllLines("admins").ToList();
                        List<string> c = File.ReadAllLines("mods").ToList();
                        List<string> d = File.ReadAllLines("normal").ToList();
                        Console.WriteLine("Owners:");
                        foreach (string owners in a)
                            Console.WriteLine(owners);
                        Console.WriteLine("Admins:");
                        foreach (string admin in b)
                            Console.WriteLine(admin);
                        Console.WriteLine("Mods:");
                        foreach (string mod in c)
                            Console.WriteLine(mod);
                        Console.WriteLine("Normal:");
                        foreach (string normals in d)
                            Console.WriteLine(normals);
                        break;
                    case "connect":
                        client.Connect(email, pass);
                        Console.WriteLine("Attempting to connect...");
                        break;
                    case "disconnect":
                        client.Disconnect();
                        break;
                    case "reconnect":
                        client.Disconnect();
                        client.Connect(email, pass);
                        break;
                    case "quit":
                        if (client.IsConnected)
                        {
                            client.Disconnect();
                        }
                        Environment.Exit(0);
                        break;
                    default:
                        break;
                }
            }
        }

        private static void LoadStreamChannels()
        {
            if (File.Exists("streams"))
            {
                streams = File.ReadAllLines("streams").ToList();
            }
        }

        private static void UpdateStreamChannels()
        {
            File.WriteAllLines("streams", streams);
        }

        private static async void StreamMusic(string cid/*, DiscordClient _client*/)
        {
            Channel c = client.GetChannel(cid);
            try
            {
                await client.JoinVoiceServer(c);
            }
            catch (Exception e)
            {
                Console.WriteLine("Join Voice Server Error: " + e.Message);
                return;
            }
            Random rnd = new Random();
            string prevsong = null;
            bool willreplay = false;
            while (streams.Contains(cid))
            {
                if (votes.ContainsKey(cid))
                {
                    votes[cid] = new List<string> { };
                }
                else
                {
                    votes.Add(cid, new List<string> { });
                }
                if (replay.ContainsKey(cid))
                {
                    replay[cid] = new List<string> { };
                }
                else
                {
                    replay.Add(cid, new List<string> { });
                }
                if (forceskip.ContainsKey(cid))
                {
                    forceskip[cid] = false;
                }
                else
                {
                    forceskip.Add(cid, false);
                }
                int listeningcount = 0;
                foreach (Membership m in c.Server.Members)
                {
                    if (m.VoiceChannelId == cid && m.UserId != client.User.Id)
                    {
                        listeningcount++;
                    }
                }
                var files = from file in Directory.EnumerateFiles(@"D:\Users\Kusoneko\Google Drive\Music", "*.mp3", System.IO.SearchOption.AllDirectories) select new { File = file };
                int mp3 = rnd.Next(0, Directory.GetFiles(@"D:\Users\Kusoneko\Google Drive\Music", "*.mp3", System.IO.SearchOption.AllDirectories).Length);
                if (willreplay)
                {
                    int j = 0;
                    foreach (var f in files)
                    {
                        if (f.File == prevsong)
                        {
                            mp3 = j;
                            willreplay = false;
                            break;
                        }
                        j++;
                    }
                }
                int i = 0;
                foreach (var f in files)
                {
                    if (mp3 == i)
                    {
                        prevsong = f.File;
                        var outFormat = new WaveFormat(48000, 16, 1);
                        using (var mp3Reader = new Mp3FileReader(f.File))
                        using (var resampler = new MediaFoundationResampler(mp3Reader, outFormat))
                        {
                            resampler.ResamplerQuality = 60;
                            int blockSize = outFormat.AverageBytesPerSecond / 50; //20 ms
                            byte[] buffer = new byte[blockSize];
                            int byteCount;
                            while ((byteCount = resampler.Read(buffer, 0, blockSize)) > 0)
                            {
                                if (byteCount < blockSize)
                                {
                                    //Incomplete frame (end of audio?), wipe the end of the buffer
                                    for (int j = byteCount; j < blockSize; j++)
                                        buffer[j] = 0;
                                }
                                if (songrequest.ContainsKey(cid))
                                {
                                    string title = "";
                                    TagLib.File song = TagLib.File.Create(f.File);
                                    if (song.Tag.Title != null && song.Tag.Title != "")
                                    {
                                        if (song.Tag.Performers != null)
                                        {
                                            foreach (string p in song.Tag.Performers)
                                            {
                                                title += p + " ";
                                            }
                                        }
                                        if (title != "")
                                            title += "- ";
                                        title += song.Tag.Title;
                                    }
                                    else
                                    {
                                        title = Path.GetFileNameWithoutExtension(song.Name);
                                    }
                                    client.SendMessage(songrequest[cid],"Song: " + title);
                                    songrequest.Remove(cid);
                                }
                                if (!streams.Contains(cid))
                                {
                                    break;
                                }
                                if (votes[cid].Count >= Math.Ceiling((decimal)listeningcount / 2))
                                {
                                    break;
                                }
                                if (replay[cid].Count >= Math.Ceiling((decimal)listeningcount / 2))
                                {
                                    willreplay = true;
                                }
                                if (forceskip[cid])
                                {
                                    break;
                                }
                                client.SendVoicePCM(buffer, blockSize);
                            }
                        }
                        break;
                    }
                    i++;
                }
                await client.WaitVoice(); //Prevent endless queueing which would eventually eat up all the ram
            }
            votes.Remove(cid);
            forceskip.Remove(cid);
            replay.Remove(cid);
            await client.LeaveVoiceServer();
        }

        private static void UpdatePermissionFiles()
        {
            File.WriteAllLines("owner", owner);
            File.WriteAllLines("admins", admins);
            File.WriteAllLines("mods", mods);
            File.WriteAllLines("normal", normal);
        }

        private static void LoadPermissionFiles()
        {
            if (File.Exists("owner"))
                owner = File.ReadAllLines("owner").ToList<string>();
            if (File.Exists("admins"))
                admins = File.ReadAllLines("admins").ToList<string>();
            if (File.Exists("mods"))
                mods = File.ReadAllLines("mods").ToList<string>();
            if (File.Exists("normal"))
                normal = File.ReadAllLines("normal").ToList<string>();
        }

        private static void LoadCredentials()
        {
            if (File.Exists("creds"))
            {
                List<string> file = File.ReadAllLines("creds").ToList();
                email = file[0];
                pass = file[1];
            }
        }

        private static void LoadChannels()
        {
            if (File.Exists("chans"))
            {
                sfw = File.ReadAllLines("chans").ToList();
            }
        }

        private static void UpdateChannels()
        {
            File.WriteAllLines("chans", sfw);
        }

        private static void ClientDisconnected(object sender, EventArgs e)
        {
            Console.WriteLine("Disconnected.");
            File.AppendAllLines("Nekobot-" + DateTime.Now.ToString("dd-MM-yyyy") + ".log", new string[] { DateTime.Now.ToString() + " - /!\\ - Disconnected." });
        }

        private static void ClientConnected(object sender, EventArgs e)
        {
            Console.WriteLine("Connected under the username " + client.User.Name + "!");
            File.AppendAllLines("Nekobot-" + DateTime.Now.ToString("dd-MM-yyyy") + ".log", new string[] { DateTime.Now.ToString() + " - /!\\ - Connected under the username " + client.User.Name + "!" });
        }

        private static void ClientMessageCreated(object sender, MessageEventArgs e)
        {
            if (!e.Message.Text.StartsWith("!") && e.Message.UserId != client.User.Id && e.Message.IsMentioningMe)
            {
                AI(e);
            }
            if (!e.Message.Channel.IsPrivate)
            {
                Console.WriteLine(DateTime.Now.ToString() + " - Message from {0} in {1} on {2}: {3}", e.Message.User.Name, e.Message.Channel.Name, e.Message.Channel.Server.Name, e.Message.Text);
                File.AppendAllLines("Nekobot-" + DateTime.Now.ToString("dd-MM-yyyy") + ".log", new string[] { DateTime.Now.ToString() + " - Message from " + e.Message.User.Name + " in " + e.Message.Channel.Name + " on " + e.Message.Channel.Server.Name + ": " + e.Message.Text });
            }
            else
            {
                Console.WriteLine(DateTime.Now.ToString() + " - Private Message from {0}: {1}", e.Message.User.Name, e.Message.Text);
                File.AppendAllLines("Nekobot-" + DateTime.Now.ToString("dd-MM-yyyy") + ".log", new string[] { DateTime.Now.ToString() + " - Private Message from " + e.Message.User.Name + ": " + e.Message.Text });
            }
            if (e.Message.Text.StartsWith("!") && e.Message.UserId != client.User.Id)
            {
                string mess = e.Message.Text.Substring(1).ToLower();
                string[] words = mess.Split(' ');
                switch (words[0])
                {
                    case "status":
                        Status(e);
                        break;

                    case "whereami":
                        WhereAmI(e);
                        break;

                    case "whois":
                        WhoIs(e);
                        break;

                    case "leave":
                        Leave(e);
                        break;

                    case "ping":
                        Ping(e);
                        break;

                    case "quote":
                        Quote(e);
                        break;

                    case "pet":
                        Pet(e);
                        break;

                    case "nya":
                        Nya(e);
                        break;

                    case "poi":
                        Poi(e);
                        break;

                    case "rand":
                        Rand(e);
                        break;

                    case "neko":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Neko(e);
                        else
                            SfwChannel(e);
                        break;

                    case "kitsune":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Kitsune(e);
                        else
                            SfwChannel(e);
                        break;

                    case "lewd":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Lewd(e);
                        else
                            SfwChannel(e);
                        break;

                    case "qt":
                        Qt(e);
                        break;

                    case "uninstall":
                        Uninstall(e);
                        break;

                    case "roll":
                        Roll(e);
                        break;

                    case "reverse":
                        Reverse(e);
                        break;

                    case "playerpost":
                        PlayerPost(e);
                        break;

                    case "playercomment":
                        PlayerComment(e);
                        break;

                    case "playerbio":
                        PlayerBio(e);
                        break;

                    case "playerlongbio":
                        PlayerLongBio(e);
                        break;

                    case "avatar":
                        Avatar(e);
                        break;

                    case "playeravatar":
                        PlayerAvatar(e);
                        break;

                    case "owner":
                        Owner(e);
                        break;

                    case "deowner":
                        Deowner(e);
                        break;

                    case "admin":
                        Admin(e);
                        break;

                    case "deadmin":
                        Deadmin(e);
                        break;

                    case "mod":
                        Mod(e);
                        break;

                    case "demod":
                        Demod(e);
                        break;

                    case "die":
                        SelfDestruct(e);
                        break;

                    case "invite":
                        AcceptInvitation(e);
                        break;

                    case "say":
                        Say(e);
                        break;
                    //Killed temporarily because causes a random RuntimeBinderException
/*
                    case "sidetail":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Danbooru(e, "sidetail", 480);
                        else
                            SfwChannel(e);
                        break;

                    case "futa":
                    case "futanari":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Danbooru(e, "futanari", 68);
                        else
                            SfwChannel(e);
                        break;

                    case "incest":
                    case "wincest":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Danbooru(e, "incest", 41);
                        else
                            SfwChannel(e);
                        break;
*/
                    case "rule34":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Rule34(e);
                        else
                            SfwChannel(e);
                        break;

                    case "gelbooru":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Gelbooru(e);
                        else
                            SfwChannel(e);
                        break;

                    case "safebooru":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Safebooru(e);
                        else
                            SfwChannel(e);
                        break;

                    case "cosplay":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Cosplay(e);
                        else
                            SfwChannel(e);
                        break;

                    case "pitur":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Pitur(e);
                        else
                            SfwChannel(e);
                        break;

                    case "waifu":
                        Waifu(e);
                        break;

                    case "kona":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Kona(e);
                        else
                            SfwChannel(e);
                        break;

                    case "gold":
                        if (!sfw.Contains(e.Message.ChannelId))
                            Gold(e);
                        else
                            SfwChannel(e);
                        break;

                    case "nsfw":
                        Nsfw(e);
                        break;

                    case "music":
                        Music(e);
                        break;

                    case "cid":
                        Cid(e);
                        break;

                    case "skip":
                        Skip(e);
                        break;

                    case "forceskip":
                        AdminSkip(e);
                        break;

                    case "song":
                        Song(e);
                        break;

                    case "kys":
                    case "killyourself":
                        Kys(e);
                        break;

                    case "notnow":
                        NotNow(e);
                        break;

                    case "encore":
                    case "ankouru":
                    case "replay":
                        VoteReplay(e);
                        break;

                    case "aicraievritaim":
                    case "aicrai":
                    case "aicraievritiem":
                    case "icri":
                    case "sadhorn":
                        SadHorn(e);
                        break;

                    case "help":
                    case "commands":
                        Commands(e);
                        break;

                    default:
                        break;
                }
            }
        }

        private static void VoteReplay(MessageEventArgs e)
        {
            foreach (string id in streams)
            {
                if (e.Message.Channel.Server.VoiceChannels.Contains(client.GetChannel(id)))
                {
                    int listeningcount = 0;
                    foreach (Membership m in e.Message.Channel.Server.Members)
                    {
                        if (m.VoiceChannelId == id && m.UserId != client.User.Id)
                        {
                            listeningcount++;
                        }
                    }
                    foreach (Membership m in e.Message.Channel.Server.Members)
                    {
                        if (m.VoiceChannelId == id && m.UserId == e.Message.UserId)
                        {
                            if (!replay[id].Contains(e.Message.UserId))
                            {
                                replay[id].Add(e.Message.UserId);
                                client.SendMessage(e.Message.Channel, replay[id].Count + "/" + listeningcount + " votes to replay current song. (Needs 50%+ to replay)");
                            }
                        }
                    }
                }
            }
        }

        private static void SadHorn(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "https://www.youtube.com/watch?v=0JAn8eShOo8");
        }

        private static void NotNow(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "https://www.youtube.com/watch?v=2BZUzJfKFwM");
        }

        private static void Song(MessageEventArgs e)
        {
            foreach (string id in streams)
            {
                if (e.Message.Channel.Server.VoiceChannels.Contains(client.GetChannel(id)))
                {
                    foreach (Membership m in e.Message.Channel.Server.Members)
                    {
                        if (m.VoiceChannelId == id && m.UserId == e.Message.UserId)
                        {
                            songrequest.Add(id, e.Message.ChannelId);
                        }
                    }
                }
            }
        }

        private static void AdminSkip(MessageEventArgs e)
        {
            if (permissions[1].Contains(e.Message.UserId) | permissions[2].Contains(e.Message.UserId) | permissions[3].Contains(e.Message.UserId))
            {
                foreach (string id in streams)
                {
                    if (e.Message.Channel.Server.VoiceChannels.Contains(client.GetChannel(id)))
                    {
                        foreach (Membership m in e.Message.Channel.Server.Members)
                        {
                            if (m.VoiceChannelId == id && m.UserId == e.Message.UserId)
                            {
                                forceskip[id] = true;
                                client.SendMessage(e.Message.Channel, "Skipping song.");
                            }
                        }
                    }
                }
            }
        }

        private static void Skip(MessageEventArgs e)
        {
            foreach (string id in streams)
            {
                if (e.Message.Channel.Server.VoiceChannels.Contains(client.GetChannel(id)))
                {
                    int listeningcount = 0;
                    foreach (Membership m in e.Message.Channel.Server.Members)
                    {
                        if (m.VoiceChannelId == id && m.UserId != client.User.Id)
                        {
                            listeningcount++;
                        }
                    }
                    foreach (Membership m in e.Message.Channel.Server.Members)
                    {
                        if (m.VoiceChannelId == id && m.UserId == e.Message.UserId)
                        {
                            if (!votes[id].Contains(e.Message.UserId))
                            {
                                votes[id].Add(e.Message.UserId);
                                client.SendMessage(e.Message.Channel, votes[id].Count + "/" + listeningcount + " votes to skip current song. (Needs 50%+ to skip)");
                            }
                        }
                    }
                }
            }
        }

        private static void Cid(MessageEventArgs e)
        {
            string mess = e.Message.Text.Substring(5);
            IEnumerable<Channel> chans = e.Message.Channel.Server.Channels;
            foreach (Channel c in chans)
            {
                if (c.Name.Contains(mess))
                {
                    client.SendMessage(e.Message.Channel, "Channel <#" + c.Id + "> (" + c.Id + ") is a " + c.Type + " channel.");
                }
            }
        }

        private static async void Music(MessageEventArgs e)
        {
            string mess = e.Message.Text.Substring(1);
            string[] words = mess.Split(' ');
            if (permissions[3].Contains(e.Message.UserId))
            {
                if (words[1].ToLower() == "on")
                {
                    if (words[2] != null)
                    {
                        Channel c = client.GetChannel(words[2]);
                        if (c.Type == "voice")
                        {
                            streams.Add(c.Id);
                            UpdateStreamChannels();
                            /*DiscordClient _client = new DiscordClient(new DiscordClientConfig() { EnableVoice = true });
                            await _client.Connect(email, pass);
                            while (!_client.IsConnected)
                                await Task.Delay(1000);*/
                            Thread music = new Thread(() => StreamMusic(c.Id/*, _client*/));
                            music.Start();
                            await client.SendMessage(e.Message.Channel, "Channel " + c.Name + " added to music streaming channel list.");
                        }
                    }
                }
                else if (words[1].ToLower() == "off")
                {
                    if (words[2] != null)
                    {
                        Channel c = client.GetChannel(words[2]);
                        if (c.Type == "voice")
                        {
                            if (streams.Contains(c.Id))
                            {
                                streams.Remove(c.Id);
                                UpdateStreamChannels();
                                await client.SendMessage(e.Message.Channel, "Channel " + c.Name + " removed from music streaming channel list.");
                            }
                        }
                    }
                }
            }
        }

        private static void SfwChannel(MessageEventArgs e)
        {
            string nsfwchans = "";
            foreach (Channel c in e.Message.Channel.Server.Channels)
            {
                if (!sfw.Contains(c.Id) && c.Type != "voice")
                    nsfwchans = nsfwchans + "<#" + c.Id + "> ";
            }
            if (nsfwchans != "")
                client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> This channel doesn't allow nsfw commands. On this server, these channels allow it: " + nsfwchans, new string[] {e.Message.UserId});
            else
                client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> This channel doesn't allow nsfw commands.", new string[] { e.Message.UserId });
        }

        private static void Nsfw(MessageEventArgs e)
        {
            string mess = e.Message.Text.Substring(1).ToLower();
            string[] words = mess.Split(' ');
            if (words[1] == "status")
            {
                if (sfw.Contains(e.Message.ChannelId))
                {
                    client.SendMessage(e.Message.Channel, "Channel " + e.Message.Channel.Name + " has nsfw commands disabled.");
                }
                else
                {
                    client.SendMessage(e.Message.Channel, "Channel " + e.Message.Channel.Name + " has nsfw commands enabled.");
                }
            }
            if (permissions[1].Contains(e.Message.UserId) | permissions[2].Contains(e.Message.UserId) | permissions[3].Contains(e.Message.UserId))
            {
                if (words[1] == "on")
                {
                    if (sfw.Contains(e.Message.ChannelId))
                    {
                        sfw.Remove(e.Message.ChannelId);
                        UpdateChannels();
                    }
                    client.SendMessage(e.Message.Channel, "Channel " + e.Message.Channel.Name + " now has nsfw commands enabled.");
                }
                else if (words[1] == "off")
                {
                    if (!sfw.Contains(e.Message.ChannelId))
                    {
                        sfw.Add(e.Message.ChannelId);
                        UpdateChannels();
                    }
                    client.SendMessage(e.Message.Channel, "Channel " + e.Message.Channel.Name + " now has nsfw commands disabled.");
                }
            }
        }

        public static async void Danbooru(MessageEventArgs e, string tag, int max)
        {
            int retry = 0;
            Random rnd = new Random();
            string sURL;
            sURL = "https://danbooru.donmai.us/posts.json?tags=" + tag + "&limit=100&page=" + rnd.Next(1, max).ToString();
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null)
                    break;
            }
            dynamic result = Newtonsoft.Json.Linq.JArray.Parse(sLine);
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, "https://danbooru.donmai.us" + result[rnd.Next(0, 100)].file_url.ToString());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Safebooru(MessageEventArgs e)
        {
            int count = 0;
            int retry = 0;
            Random rnd = new Random();
            HttpWebRequest wr;
            HttpWebResponse res;
            XmlDocument xdoc = new XmlDocument();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    wr = WebRequest.Create("http://safebooru.org/index.php?page=dapi&s=post&q=index&tags=" + e.Message.RawText.Substring(10)) as HttpWebRequest;
                    res = wr.GetResponse() as HttpWebResponse;
                    xdoc.Load(res.GetResponseStream());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            XmlNode posts = xdoc.SelectSingleNode("posts");
            count = int.Parse(posts.Attributes["count"].Value.ToString());
            if (count < 1)
            {
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, "There isn't anything under the tag(s) " + e.Message.RawText.Substring(10) + " on safebooru.");
                        return;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (HttpRequestException we)
                    {
                        Console.WriteLine(we.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception aex)
                    {
                        Console.WriteLine(aex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                    if (retry == 5)
                        return;
                }
            }
            string sURL;
            sURL = "http://safebooru.org/index.php?page=dapi&s=post&q=index&limit=1&tags=" + e.Message.RawText.Substring(10) + "&pid=" + rnd.Next(0, count).ToString();
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            sLine = objReader.ReadToEnd();
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(sLine);
            dynamic result = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeXmlNode(xml));
            string file_url = "";
            try
            {
                foreach (string key in result.posts.post)
                {
                    if (key.Contains("http://"))
                    {
                        file_url = key;
                        break;
                    }
                }
            }
            catch (Exception aex)
            {
                Console.WriteLine(aex.Message);
                return;
            }
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, file_url);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Gelbooru(MessageEventArgs e)
        {
            int count = 0;
            int retry = 0;
            Random rnd = new Random();
            HttpWebRequest wr;
            HttpWebResponse res;
            XmlDocument xdoc = new XmlDocument();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    wr = WebRequest.Create("http://gelbooru.com/index.php?page=dapi&s=post&q=index&tags=" + e.Message.RawText.Substring(9)) as HttpWebRequest;
                    res = wr.GetResponse() as HttpWebResponse;
                    xdoc.Load(res.GetResponseStream());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            XmlNode posts = xdoc.SelectSingleNode("posts");
            count = int.Parse(posts.Attributes["count"].Value.ToString());
            if (count < 1)
            {
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, "There isn't anything under the tag(s) " + e.Message.RawText.Substring(9) + " on gelbooru.");
                        return;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (HttpRequestException we)
                    {
                        Console.WriteLine(we.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception aex)
                    {
                        Console.WriteLine(aex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                    if (retry == 5)
                        return;
                }
            }
            string sURL;
            sURL = "http://gelbooru.com/index.php?page=dapi&s=post&q=index&limit=1&tags=" + e.Message.RawText.Substring(9) + "&pid=" + rnd.Next(0, count).ToString();
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            sLine = objReader.ReadToEnd();
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(sLine);
            dynamic result = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeXmlNode(xml));
            string file_url = "";
            try
            {
                foreach (string key in result.posts.post)
                {
                    if (key.Contains("http://"))
                    {
                        file_url = key;
                        break;
                    }
                }
            }
            catch (Exception aex)
            {
                Console.WriteLine(aex.Message);
                return;
            }
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, file_url);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Rule34(MessageEventArgs e)
        {
            int count = 0;
            int retry = 0;
            Random rnd = new Random();
            HttpWebRequest wr;
            HttpWebResponse res;
            XmlDocument xdoc = new XmlDocument();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    wr = WebRequest.Create("http://rule34.xxx/index.php?page=dapi&s=post&q=index&tags=" + e.Message.RawText.Substring(8)) as HttpWebRequest;
                    res = wr.GetResponse() as HttpWebResponse;
                    xdoc.Load(res.GetResponseStream());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            XmlNode posts = xdoc.SelectSingleNode("posts");
            count = int.Parse(posts.Attributes["count"].Value.ToString());
            if (count < 1)
            {
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, "There isn't anything under the tag(s) " + e.Message.RawText.Substring(8) + " on rule34.");
                        return;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (HttpRequestException we)
                    {
                        Console.WriteLine(we.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception aex)
                    {
                        Console.WriteLine(aex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                    if (retry == 5)
                        return;
                }
            }
            string sURL;
            sURL = "http://rule34.xxx/index.php?page=dapi&s=post&q=index&limit=1&tags=" + e.Message.RawText.Substring(8) + "&pid=" + rnd.Next(0, count).ToString();
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            sLine = objReader.ReadToEnd();
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(sLine);
            dynamic result = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeXmlNode(xml));
            string file_url = "";
            try
            {
                foreach (string key in result.posts.post)
                {
                    if (key.Contains("http://"))
                    {
                        file_url = key;
                        break;
                    }
                }
            }
            catch (Exception aex)
            {
                Console.WriteLine(aex.Message);
                return;
            }
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, file_url);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Kona(MessageEventArgs e)
        {
            int count = 0;
            int retry = 0;
            Random rnd = new Random();
            HttpWebRequest wr;
            HttpWebResponse res;
            XmlDocument xdoc = new XmlDocument();
            while (retry < 5)
            {
                try
                {
                    wr = WebRequest.Create("http://konachan.com/post.xml?tags=" + e.Message.RawText.Substring(6)) as HttpWebRequest;
                    res = wr.GetResponse() as HttpWebResponse;
                    xdoc.Load(res.GetResponseStream());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            XmlNode posts = xdoc.SelectSingleNode("posts");
            count = int.Parse(posts.Attributes["count"].Value.ToString());
            if (count < 1)
            {
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, "There isn't anything under the tag(s) " + e.Message.RawText.Substring(6) + " on konachan.");
                        return;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (HttpRequestException we)
                    {
                        Console.WriteLine(we.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception aex)
                    {
                        Console.WriteLine(aex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                    if (retry == 5)
                        return;
                }
            }
            string sURL;
            sURL = "http://konachan.com/post.json?tags=" + e.Message.RawText.Substring(6) + "&limit=1&page=" + rnd.Next(1, count + 1).ToString();
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null)
                    break;
            }
            dynamic result = Newtonsoft.Json.Linq.JArray.Parse(sLine);
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, result[0].file_url.ToString());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (HttpRequestException we)
                {
                    Console.WriteLine(we.Message);
                    await Task.Delay(5000);
                }
                catch (Exception aex)
                {
                    Console.WriteLine(aex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static void Say(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, e.Message.Text.Substring(5));
        }

        public static void Owner(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId))
            {
                List<User> users = new List<User> { };
                foreach (string mention in e.Message.MentionIds)
                {
                    users.Add(client.GetUser(mention));
                }
                foreach (User user in users)
                {
                    if (user != null)
                    {
                        if (permissions[0].Contains(user.Id))
                            permissions[0].Remove(user.Id);
                        if (permissions[1].Contains(user.Id))
                            permissions[1].Remove(user.Id);
                        if (permissions[2].Contains(user.Id))
                            permissions[2].Remove(user.Id);
                        if (permissions[3].Contains(user.Id))
                            permissions[3].Remove(user.Id);
                        permissions[3].Add(user.Id);
                        UpdatePermissionFiles();
                        Console.WriteLine(user.Name + " is now an owner! (Permission level is now 3)");
                        client.SendMessage(e.Message.Channel, user.Name + " is now an owner! (Permission level is now 3)");
                    }
                }
            }
        }

        public static void Deowner(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId))
            {
                List<User> users = new List<User> { };
                foreach (string mention in e.Message.MentionIds)
                {
                    users.Add(client.GetUser(mention));
                }
                foreach (User user in users)
                {
                    if (user != null)
                    {
                        if (permissions[3].Contains(user.Id))
                        {
                            permissions[3].Remove(user.Id);
                            permissions[0].Add(user.Id);
                            UpdatePermissionFiles();
                            Console.WriteLine(user.Name + " is no longer an owner! (Permission level is now 0)");
                            client.SendMessage(e.Message.Channel, user.Name + " is no longer an owner! (Permission level is now 0)");
                        }
                        else
                        {
                            client.SendMessage(e.Message.Channel, user.Name + " is not an owner!");
                        }
                    }
                }
            }
        }

        public static void Admin(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId))
            {
                List<User> users = new List<User> { };
                foreach (string mention in e.Message.MentionIds)
                {
                    users.Add(client.GetUser(mention));
                }
                foreach (User user in users)
                {
                    if (user != null)
                    {
                        if (!permissions[3].Contains(user.Id))
                        {
                            if (permissions[0].Contains(user.Id))
                                permissions[0].Remove(user.Id);
                            if (permissions[1].Contains(user.Id))
                                permissions[1].Remove(user.Id);
                            if (permissions[2].Contains(user.Id))
                                permissions[2].Remove(user.Id);
                            permissions[2].Add(user.Id);
                            UpdatePermissionFiles();
                            Console.WriteLine(user.Name + " is now an admin! (Permission level is now 2)");
                            client.SendMessage(e.Message.Channel, user.Name + " is now an admin! (Permission level is now 2)");
                        }
                        else
                        {
                            client.SendMessage(e.Message.Channel, "The !admin command cannot be used on an owner!");
                        }
                    }
                }
            }
        }

        public static void Deadmin(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId))
            {
                List<User> users = new List<User> { };
                foreach (string mention in e.Message.MentionIds)
                {
                    users.Add(client.GetUser(mention));
                }
                foreach (User user in users)
                {
                    if (user != null)
                    {
                        if (permissions[2].Contains(user.Id) && !permissions[3].Contains(user.Id))
                        {
                            permissions[2].Remove(user.Id);
                            permissions[0].Add(user.Id);
                            UpdatePermissionFiles();
                            Console.WriteLine(user.Name + " is no longer an admin! (Permission level is now 0)");
                            client.SendMessage(e.Message.Channel, user.Name + " is no longer an admin! (Permission level is now 0)");
                        }
                        else
                        {
                            client.SendMessage(e.Message.Channel, user.Name + " is not an admin!");
                        }
                    }
                }
            }
        }

        public static void Mod(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId) || permissions[2].Contains(e.Message.UserId))
            {
                List<User> users = new List<User> { };
                foreach (string mention in e.Message.MentionIds)
                {
                    users.Add(client.GetUser(mention));
                }
                foreach (User user in users)
                {
                    if (user != null)
                    {
                        if (!permissions[3].Contains(user.Id) && !permissions[2].Contains(user.Id))
                        {
                            if (permissions[0].Contains(user.Id))
                                permissions[0].Remove(user.Id);
                            if (permissions[1].Contains(user.Id))
                                permissions[1].Remove(user.Id);
                            permissions[1].Add(user.Id);
                            UpdatePermissionFiles();
                            Console.WriteLine(user.Name + " is now a mod! (Permission level is now 1)");
                            client.SendMessage(e.Message.Channel, user.Name + " is now a mod! (Permission level is now 1)");
                        }
                        else
                        {
                            client.SendMessage(e.Message.Channel, "The !mod command cannot be used on an admin or an owner!");
                        }
                    }
                }
            }
        }

        public static void Demod(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId) || permissions[2].Contains(e.Message.UserId))
            {
                List<User> users = new List<User> { };
                foreach (string mention in e.Message.MentionIds)
                {
                    users.Add(client.GetUser(mention));
                }
                foreach (User user in users)
                {
                    if (user != null)
                    {
                        if (permissions[1].Contains(user.Id) && !permissions[3].Contains(user.Id) && !permissions[2].Contains(user.Id))
                        {
                            permissions[1].Remove(user.Id);
                            permissions[0].Add(user.Id);
                            UpdatePermissionFiles();
                            Console.WriteLine(user.Name + " is no longer a mod! (Permission level is now 0)");
                            client.SendMessage(e.Message.Channel, user.Name + " is no longer a mod! (Permission level is now 0)");
                        }
                        else
                        {
                            client.SendMessage(e.Message.Channel, user.Name + " is not a mod!");
                        }
                    }
                }
            }
        }

        public static void AcceptInvitation(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId) || permissions[2].Contains(e.Message.UserId) || permissions[1].Contains(e.Message.UserId))
            {
                string[] words = e.Message.Text.Substring(1).Split(' ');
                if (1 < words.Length)
                {
                    try
                    {
                        client.AcceptInvite(words[1]);
                        client.SendMessage(e.Message.Channel, "Invitation accepted!");
                    }
                    catch
                    {
                        client.SendMessage(e.Message.Channel, "Invitation invalid!");
                    }
                }
            }
        }

        public static void Status(MessageEventArgs e)
        {
            if (e.Message.Channel.IsPrivate)
            {
                client.SendMessage(e.Message.Channel, "I work!");
            }
            else
            {
                client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> I work!", new string[] { e.Message.UserId });
            }
        }

        public static void WhereAmI(MessageEventArgs e)
        {
            if (!e.Message.Channel.IsPrivate)
            {
                Discord.Server server = client.GetServer(e.Message.Channel.ServerId);
                string sowner = "";
                foreach (var member in server.Members)
                    if (member.UserId == server.OwnerId)
                        sowner = member.User.Name;
                string whereami = String.Format("You are currently in *{0}* ({1}) on server *{2}* ({3}) owned by {4}.", e.Message.Channel.Name, e.Message.ChannelId, server.Name, server.Id, sowner);
                client.SendMessage(e.Message.ChannelId, whereami);
            }
            else
            {
                string whereami = String.Format("You are currently in a private message with me, baka.");
                client.SendMessage(e.Message.ChannelId, whereami);
            }
        }

        public static void WhoIs(MessageEventArgs e)
        {
            //!whois Username
            if (!e.Message.Channel.IsPrivate)
            {
                string username = e.Message.Text.Substring(7).Split(' ')[0];
                Console.WriteLine("Whois was invoked on: " + username);
                Discord.Server foundServer = client.GetServer(e.Message.Channel.ServerId);
                if (foundServer != null)
                {
                    foreach (var member in foundServer.Members)
                    {
                        if (member.User.Name.ToLower() == username.ToLower())
                        {
                            Discord.User foundMember = member.User;
                            int permission = 0;
                            if (permissions[3].Contains(foundMember.Id))
                                permission = 3;
                            else if (permissions[2].Contains(foundMember.Id))
                                permission = 2;
                            else if (permissions[1].Contains(foundMember.Id))
                                permission = 1;
                            else if (permissions[0].Contains(foundMember.Id))
                                permission = 0;
                            if (member.UserId != client.User.Id)
                                client.SendMessage(e.Message.Channel, string.Format("{0}'s user id is {1} and his/her permission level to me is {2}.", foundMember.Name, foundMember.Id, permission.ToString()));
                            else
                                client.SendMessage(e.Message.Channel, "My id is " + client.User.Id + ".");
                            return;
                        }
                    }
                }
            }
            else
            {
                client.SendMessage(e.Message.Channel, "Sorry, I can't do this in a private message.");
            }
        }

        public static void SelfDestruct(MessageEventArgs e)
        {
            if (permissions[3].Contains(e.Message.UserId))
            {
                client.SendMessage(e.Message.Channel, "Bye!");
                client.Disconnect();
                Environment.Exit(0);
            }
            else
            {
                if (e.Message.Channel.IsPrivate)
                {
                    client.SendMessage(e.Message.Channel, "Wh-why? You don't like me? :c");
                }
                else
                {
                    client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> Wh-why? You don't like me? :c", new string[] { e.Message.UserId });
                }
            }
        }

        public static void Leave(MessageEventArgs e)
        {
            if (permissions[2].Contains(e.Message.UserId) || permissions[3].Contains(e.Message.UserId))
            {
                client.SendMessage(e.Message.Channel, "Bye " + e.Message.Channel.Name + "!");
                client.LeaveServer(e.Message.Channel.Server);
            }
            else
            {
                if (e.Message.Channel.IsPrivate)
                {
                    client.SendMessage(e.Message.Channel, "Wh-why? You don't like me? :c");
                }
                else
                {
                    client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> Wh-why? You don't like me? :c", new string[] { e.Message.UserId });
                }
            }
        }

        public static void Ping(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "Pong!");
        }

        public static async void Quote(MessageEventArgs e)
        {
            int retry = 0;
            string sURL;
            sURL = "https://julxzs.website/api/quote";
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null)
                    break;
            }
            dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, "\"" + result.quote.quote + "\" - " + result.quote.author + " " + result.quote.date);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static void Pet(MessageEventArgs e)
        {
            string reply = "";
            if (e.Message.MentionIds.Count<string>() > 0 || e.Message.IsMentioningEveryone)
            {
                reply = reply + "<@" + e.Message.UserId + "> *pets* ";
                List<string> mentions = new List<string>() { };
                mentions.Add(e.Message.UserId);
                foreach (string m in e.Message.MentionIds)
                {
                    mentions.Add(m);
                }
                if (e.Message.IsMentioningEveryone)
                {
                    reply = reply + "@everyone ! *purrs*";
                }
                else
                {
                    foreach (string m in e.Message.MentionIds)
                    {
                        reply = reply + "<@" + m + "> ";
                    }
                    reply = reply + "! ";
                }
                if (e.Message.IsMentioningMe && !e.Message.IsMentioningEveryone)
                {
                    reply = reply + "*purrs*";
                }
                client.SendMessage(e.Message.Channel, reply, mentions.ToArray());
            }
            else
            {
                client.SendMessage(e.Message.Channel, "*purrs*");
            }
        }

        public static void Nya(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "Nyaaa~");
        }

        public static void Poi(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "Poi!");
        }

        public static void Rand(MessageEventArgs e)
        {
            string[] words = e.Message.Text.Substring(1).Split(' ');
            int x = 1;
            int y = 101;
            int z;
            if (1 < words.Length)
            {
                if (int.TryParse(words[1], out z))
                {
                    x = int.Parse(words[1]);
                }
                if (2 < words.Length)
                {
                    if (int.TryParse(words[2], out z))
                    {
                        y = int.Parse(words[2]);
                        y++;
                    }
                }
                else
                {
                    y = x;
                    x = 1;
                }
            }
            if (y < x)
            {
                z = x;
                x = y;
                y = z;
            }
            else if (x == y)
            {
                y++;
            }
            Random rnd = new Random();
            if (e.Message.Channel.IsPrivate)
            {
                client.SendMessage(e.Message.Channel, rnd.Next(x, y).ToString());
            }
            else
            {
                client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> " + rnd.Next(x, y).ToString(), new string[] { e.Message.UserId });
            }
        }

        public static async void Neko(MessageEventArgs e)
        {
            int retry = 0;
            string sURL;
            sURL = "https://lewdchan.com/neko/src/list.php";
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            List<string> files = new List<string>();
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null && StringExtension.GetLast(sLine, 4) != "webm")
                    files.Add(sLine);
            }
            Random rnd = new Random();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, "https://lewdchan.com/neko/src/" + files[rnd.Next(0, files.Count)]);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Kitsune(MessageEventArgs e)
        {
            int retry = 0;
            string sURL;
            sURL = "https://lewdchan.com/kitsune/src/list.php";
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            List<string> files = new List<string>();
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null && StringExtension.GetLast(sLine, 4) != "webm")
                    files.Add(sLine);
            }
            Random rnd = new Random();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, "https://lewdchan.com/kitsune/src/" + files[rnd.Next(0, files.Count)]);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Lewd(MessageEventArgs e)
        {
            int retry = 0;
            string sURL;
            sURL = "https://lewdchan.com/lewd/src/list.php";
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            List<string> files = new List<string>();
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null && StringExtension.GetLast(sLine, 4) != "webm")
                    files.Add(sLine);
            }
            Random rnd = new Random();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, "https://lewdchan.com/lewd/src/" + files[rnd.Next(0, files.Count)]);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static async void Qt(MessageEventArgs e)
        {
            int retry = 0;
            string sURL;
            sURL = "https://lewdchan.com/qt/src/list.php";
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            List<string> files = new List<string>();
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null && StringExtension.GetLast(sLine, 4) != "webm")
                    files.Add(sLine);
            }
            Random rnd = new Random();
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, "https://lewdchan.com/qt/src/" + files[rnd.Next(0, files.Count)]);
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static void Uninstall(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "https://www.youtube.com/watch?v=iNCXiMt1bR4");
        }

        public static void Kys(MessageEventArgs e)
        {
            client.SendMessage(e.Message.Channel, "https://www.youtube.com/watch?v=2dbR2JZmlWo");
        }

        public static void Roll(MessageEventArgs e)
        {
            string[] words = e.Message.Text.Substring(1).Split(' ');
            int x = 1;
            int y = 7;
            int z = 1;
            int a;
            Random rnd = new Random();
            if (1 < words.Length)
            {
                if (int.TryParse(words[1], out a))
                {
                    y = int.Parse(words[1]);
                    y++;
                }
                if (2 < words.Length)
                {
                    if (int.TryParse(words[2], out a))
                    {
                        x = int.Parse(words[2]);
                    }
                    if (3 < words.Length)
                    {
                        if (int.TryParse(words[3], out a))
                        {
                            z = int.Parse(words[3]);
                        }
                    }
                }
            }
            if (x <= 0 || y <= 0 || z <= 0)
            {
                if (e.Message.Channel.IsPrivate)
                {
                    client.SendMessage(e.Message.Channel, "All 3 parameters must be higher than 0.");
                }
                else
                {
                    client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> All 3 parameters must be higher than 0.", new string[] { e.Message.UserId });
                }
                return;
            }
            if (x > 999)
                x = 999;
            if (y > 999)
                y = 1000;
            if (z > 999)
                z = 999;
            a = 0;
            unchecked
            {
                for (int i = 1; i <= z; i++)
                {
                    for (int j = 1; j <= x; j++)
                    {
                        a += rnd.Next(1, y);
                    }
                }
            }
            if (a <= 0)
            {
                if (e.Message.Channel.IsPrivate)
                {
                    client.SendMessage(e.Message.Channel, "Result overflowed maximum int value of " + int.MaxValue.ToString());
                }
                else
                {
                    client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> Result overflowed maximum int value of " + int.MaxValue.ToString(), new string[] { e.Message.UserId });
                }
            }
            else
            {
                if (e.Message.Channel.IsPrivate)
                {
                    client.SendMessage(e.Message.Channel, "Rolling " + x.ToString() + " " + (y - 1).ToString() + "-faced dices " + z.ToString() + " times... Result: " + a.ToString());
                }
                else
                {
                    client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> Rolling " + x.ToString() + " " + (y - 1).ToString() + "-faced dices " + z.ToString() + " times... Result: " + a.ToString(), new string[] { e.Message.UserId });
                }
            }
        }

        public static void Reverse(MessageEventArgs e)
        {
            string message = e.Message.Text.Substring(9);
            /*MatchCollection matches = Regex.Matches(message, "<@(\\d{17})>");
            if (matches.Count > 0)
            {
                foreach (Match m in matches)
                {
                    message = message.Replace("<@" + m.Groups[1].Value + ">", "@" + client.GetUser(m.Groups[1].Value).Name);
                }
            }
            matches = Regex.Matches(message, "<#(\\d{17})>");
            if (matches.Count > 0)
            {
                foreach (Match m in matches)
                {
                    message = message.Replace("<#" + m.Groups[1].Value + ">", client.GetChannel(m.Groups[1].Value).Name);
                }
            }*/
            char[] chars = message.ToCharArray();
            char[] result = new char[chars.Length];
            for (int i = 0, j = message.Length - 1; i < message.Length; i++, j--)
            {
                result[i] = chars[j];
            }
            client.SendMessage(e.Message.Channel, new string(result), e.Message.MentionIds);
        }

        public static async void PlayerPost(MessageEventArgs e)
        {
            int retry = 0;
            string[] words = e.Message.Text.Substring(1).Split(' ');
            if (1 < words.Length)
            {
                string sURL;
                sURL = "https://player.me/api/v1/feed/" + words[1];
                WebRequest wrGETURL;
                wrGETURL = WebRequest.Create(sURL);
                Stream objStream = null;
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                        break;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                }
                StreamReader objReader = new StreamReader(objStream);
                string sLine = "";
                while (sLine != null)
                {
                    sLine = objReader.ReadLine();
                    if (sLine != null)
                        break;
                }
                dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, result.results.user.username.ToString() + ": " + result.results.data.post_raw.ToString());
                        break;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                }
            }
        }

        public static async void PlayerComment(MessageEventArgs e)
        {
            int retry = 0;
            string[] words = e.Message.Text.Substring(1).Split(' ');
            if (2 < words.Length)
            {
                string sURL;
                string sURL2;
                sURL = "https://player.me/api/v1/feed/" + words[1];
                sURL2 = "https://player.me/api/v1/feed/" + words[1] + "/comments/" + words[2];
                WebRequest wrGETURL;
                WebRequest wrGETURL2;
                wrGETURL = WebRequest.Create(sURL);
                wrGETURL2 = WebRequest.Create(sURL2);
                Stream objStream = null;
                Stream objStream2 = null;
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                        break;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                }
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        objStream2 = wrGETURL2.GetResponse().GetResponseStream();
                        break;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                }
                StreamReader objReader = new StreamReader(objStream);
                StreamReader objReader2 = new StreamReader(objStream2);
                string sLine = "";
                string sLine2 = "";
                while (sLine != null)
                {
                    sLine = objReader.ReadLine();
                    if (sLine != null)
                        break;
                }
                while (sLine2 != null)
                {
                    sLine2 = objReader2.ReadLine();
                    if (sLine2 != null)
                        break;
                }
                dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                dynamic result2 = Newtonsoft.Json.Linq.JObject.Parse(sLine2);
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, "Post - " + result.results.user.username.ToString() + ": " + result.results.data.post_raw.ToString());
                        break;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                }
                retry = 0;
                while (retry < 5)
                {
                    try
                    {
                        await client.SendMessage(e.Message.Channel, "Comment - " + result2.results.user.username.ToString() + ": " + result2.results.data.post_raw.ToString());
                        break;
                    }
                    catch (WebException ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        await Task.Delay(5000);
                    }
                    retry++;
                }
            }
        }

        public static async void PlayerBio(MessageEventArgs e)
        {
            string[] words = e.Message.Text.Substring(1).Split(' ');
            if (1 < words.Length)
            {
                string sURL;
                sURL = "https://player.me/api/v1/users?_query=" + words[1];
                WebRequest wrGETURL;
                wrGETURL = WebRequest.Create(sURL);
                Stream objStream = null;
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                StreamReader objReader = new StreamReader(objStream);
                string sLine = "";
                while (sLine != null)
                {
                    sLine = objReader.ReadLine();
                    if (sLine != null)
                        break;
                }
                dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                try
                {
                    if (result.results[0] != null)
                        Console.Write("");
                    try
                    {
                        await client.SendMessage(e.Message.Channel, words[1] + "'s short bio: " + result.results[0].short_description.ToString());
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.Message);
                    }
                }
                catch (ArgumentOutOfRangeException exc)
                {
                    Console.WriteLine(exc.Message);
                    sURL = "https://player.me/api/v1/groups?_query=" + words[1];
                    wrGETURL = WebRequest.Create(sURL);
                    try
                    {
                        objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    objReader = new StreamReader(objStream);
                    sLine = "";
                    while (sLine != null)
                    {
                        sLine = objReader.ReadLine();
                        if (sLine != null)
                            break;
                    }
                    result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                    try
                    {
                        if (result.results[0] != null)
                            Console.Write("");
                        try
                        {
                            await client.SendMessage(e.Message.Channel, words[1] + "'s short bio: " + result.results[0].short_description.ToString());
                        }
                        catch (Exception exce)
                        {
                            Console.WriteLine(exce.Message);
                        }
                    }
                    catch (ArgumentOutOfRangeException exce)
                    {
                        Console.WriteLine(exce.Message);
                        try
                        {
                            await client.SendMessage(e.Message.Channel, words[1] + " doesn't exist.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
        }

        public static async void PlayerLongBio(MessageEventArgs e)
        {
            string[] words = e.Message.Text.Substring(1).Split(' ');
            if (1 < words.Length)
            {
                string sURL;
                sURL = "https://player.me/api/v1/users?_query=" + words[1];
                WebRequest wrGETURL;
                wrGETURL = WebRequest.Create(sURL);
                Stream objStream = null;
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                StreamReader objReader = new StreamReader(objStream);
                string sLine = "";
                while (sLine != null)
                {
                    sLine = objReader.ReadLine();
                    if (sLine != null)
                        break;
                }
                dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                try
                {
                    if (result.results[0] != null)
                        Console.Write("");
                    try
                    {
                        await client.SendMessage(e.Message.Channel, words[1] + "'s long bio: " + result.results[0].description.ToString());
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.Message);
                    }
                }
                catch (ArgumentOutOfRangeException exc)
                {
                    Console.WriteLine(exc.Message);
                    sURL = "https://player.me/api/v1/groups?_query=" + words[1];
                    wrGETURL = WebRequest.Create(sURL);
                    try
                    {
                        objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    objReader = new StreamReader(objStream);
                    sLine = "";
                    while (sLine != null)
                    {
                        sLine = objReader.ReadLine();
                        if (sLine != null)
                            break;
                    }
                    result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                    try
                    {
                        if (result.results[0] != null)
                            Console.Write("");
                        try
                        {
                            await client.SendMessage(e.Message.Channel, words[1] + "'s long bio: " + result.results[0].description.ToString());
                        }
                        catch (Exception exce)
                        {
                            Console.WriteLine(exce.Message);
                        }
                    }
                    catch (ArgumentOutOfRangeException exce)
                    {
                        Console.WriteLine(exce.Message);
                        try
                        {
                            await client.SendMessage(e.Message.Channel, words[1] + " doesn't exist.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
        }

        public static void Avatar(MessageEventArgs e)
        {
            List<User> users = new List<User> { };
            foreach (string mention in e.Message.MentionIds)
            {
                users.Add(client.GetUser(mention));
            }
            foreach (User user in users)
            {
                client.SendMessage(e.Message.Channel, user.AvatarUrl);
            }
        }

        public static async void PlayerAvatar(MessageEventArgs e)
        {
            string[] words = e.Message.Text.Substring(1).Split(' ');
            if (1 < words.Length)
            {
                string sURL;
                sURL = "https://player.me/api/v1/users?_query=" + words[1];
                WebRequest wrGETURL;
                wrGETURL = WebRequest.Create(sURL);
                Stream objStream = null;
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                StreamReader objReader = new StreamReader(objStream);
                string sLine = "";
                while (sLine != null)
                {
                    sLine = objReader.ReadLine();
                    if (sLine != null)
                        break;
                }
                dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                try
                {
                    if (result.results[0] != null)
                        Console.Write("");
                    try
                    {
                        await client.SendMessage(e.Message.Channel, words[1] + "'s avatar: http:" + result.results[0].avatar.original.ToString());
                    }
                    catch (Exception exc)
                    {
                        Console.WriteLine(exc.Message);
                    }
                }
                catch (ArgumentOutOfRangeException exc)
                {
                    Console.WriteLine(exc.Message);
                    sURL = "https://player.me/api/v1/groups?_query=" + words[1];
                    wrGETURL = WebRequest.Create(sURL);
                    try
                    {
                        objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    objReader = new StreamReader(objStream);
                    sLine = "";
                    while (sLine != null)
                    {
                        sLine = objReader.ReadLine();
                        if (sLine != null)
                            break;
                    }
                    result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
                    try
                    {
                        if (result.results[0] != null)
                            Console.Write("");
                        try
                        {
                            await client.SendMessage(e.Message.Channel, words[1] + "'s avatar: http:" + result.results[0].avatar.original.ToString());
                        }
                        catch (Exception exce)
                        {
                            Console.WriteLine(exce.Message);
                        }
                    }
                    catch (ArgumentOutOfRangeException exce)
                    {
                        Console.WriteLine(exce.Message);
                        try
                        {
                            await client.SendMessage(e.Message.Channel, words[1] + " doesn't exist.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
        }

        public static void Cosplay(MessageEventArgs e)
        {
            var files = from file in Directory.EnumerateFiles(@"E:\Github\Nekobot\Nekobot\bin\Release\cosplay") select new { File = file };
            Random rnd = new Random();
            int img = rnd.Next(0, Directory.GetFiles(@"E:\Github\Nekobot\Nekobot\bin\Release\cosplay").Length);
            int i = 0;
            foreach (var f in files)
            {
                if (img == i)
                {
                    client.SendFile(e.Message.Channel, f.File);
                    break;
                }
                i++;
            }
        }

        public static void Pitur(MessageEventArgs e)
        {
            var files = from file in Directory.EnumerateFiles(@"E:\Github\Nekobot\Nekobot\bin\Release\pitur") select new { File = file };
            Random rnd = new Random();
            int img = rnd.Next(0, Directory.GetFiles(@"E:\Github\Nekobot\Nekobot\bin\Release\pitur").Length);
            int i = 0;
            foreach (var f in files)
            {
                if (img == i)
                {
                    client.SendFile(e.Message.Channel, f.File);
                    break;
                }
                i++;
            }
        }

        public static void Gold(MessageEventArgs e)
        {
            var files = from file in Directory.EnumerateFiles(@"D:\Users\Kusoneko\Google Drive\KanColle") select new { File = file };
            Random rnd = new Random();
            int img = rnd.Next(0, Directory.GetFiles(@"D:\Users\Kusoneko\Google Drive\KanColle").Length);
            int i = 0;
            foreach (var f in files)
            {
                if (img == i)
                {
                    client.SendFile(e.Message.Channel, f.File);
                    break;
                }
                i++;
            }
        }

        public static async void Waifu(MessageEventArgs e)
        {
            int retry = 0;
            string sURL;
            sURL = "https://julxzs.website/api/random-waifu";
            WebRequest wrGETURL;
            wrGETURL = WebRequest.Create(sURL);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls12 | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            Stream objStream = null;
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    objStream = (await wrGETURL.GetResponseAsync()).GetResponseStream();
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
            StreamReader objReader = new StreamReader(objStream);
            string sLine = "";
            while (sLine != null)
            {
                sLine = objReader.ReadLine();
                if (sLine != null)
                    break;
            }
            dynamic result = Newtonsoft.Json.Linq.JObject.Parse(sLine);
            retry = 0;
            while (retry < 5)
            {
                try
                {
                    await client.SendMessage(e.Message.Channel, result.waifu.ToString());
                    break;
                }
                catch (WebException ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(5000);
                }
                retry++;
                if (retry == 5)
                    return;
            }
        }

        public static void Commands(MessageEventArgs e)
        {
            Discord.Channel channel = client.GetPMChannel(e.Message.UserId).Result;
            if (!e.Message.Channel.IsPrivate)
            {
                client.SendMessage(e.Message.Channel, "<@" + e.Message.UserId + "> I'm sending you the list of commands in PM!", new string[] { e.Message.UserId });
            }
            client.SendMessage(channel, @"List of all commands (optional command parameters are in square brackets [], aliases are in parenthesis ()):
 !status - Replies with I work if I'm not broken.
 !whereami - Replies with details about the channel and server where you did this command.
 !whois Username - Replies with the unique User ID of the specified user as well as his permission level. Warning: only works in a channel
 !leave - Why would you want to do this? Nekobot will leave the server this is posted in. Only happens if used by permission level 2 or higher.
 !die - Why would you want to do this? Kills the bot for all servers it's in. Only happens if used by permission level 3.
 !ping - Replies with Pong!.
 !quote - Replies with a random quote from https://julxzs.website/quotes
 !pet [@username] [@everyone] - Tells who petted who, if multiple people are mentioned, all of them are petted, if everyone is in, only everyone will be in the reply. Purrs if everyone or mentioned herself or no mentions at all (assumes that you're petting her in that case).
 !nya - Replies with Nyaaa~.
 !poi - Replies with Poi!.
 !rand [x] [y] - Generates a random number between x and y, both are optional: x defaults to 1 and y defaults to 100, if only 1 parameter is given, that parameter is y
 !neko - Grabs a random nekomimi image from https://lewdchan.com/neko Warning: can return a nsfw image
 !kitsune - Grabs a random kitsunemimi image from https://lewdchan.com/kitsune Warning: can return a nsfw image
 !lewd - Grabs a random lewd image from https://lewdchan.com/lewd Warning: can return a nsfw image
 !qt - Grabs a random 2d qt image from https://lewdchan.com/qt
 !uninstall - A great advice in any situation.
 !kys (!killyourself) - Another good advice.
 !roll [y] [x] [z] - Roll x y-faced dices z times. x and z are optional and both default to 1, y is also optional and default to 6 (for a 6-faced dice)
 !reverse - Replies with everything that follows the command reversed
 !playerpost id - Replies with the content of the post at https://player.me/feed/id
 !playercomment postid commentid - Replies with the content of the post and the highlighted comment at https://player.me/feed/postid?comment=commentid
 !playerbio username - Replies with the short bio of the user on player.me
 !playerlongbio username - Replies with the long bio of the user on player.me
 !avatar @username - Returns the user's discord avatar
 !playeravatar username - Returns the avatar of username on player.me
 !owner @username - Gives permission level 3 to a user with permission level 2 or lower. Requires permission level 3.
 !deowner @username - Removes any permission levels from a user and gets him/her to permission level 0. Requires permission level 3.
 !admin @username - Gives permission level 2 to a user with permission level 1 or lower. Requires permission level 3.
 !deadmin @username - Removes permission level 2 from a user and gets him/her to permission level 0. Requires permission level 3.
 !mod @username - Gives permission level 1 to a user with permission level 0. Requires permission level 2 or higher.
 !demod @username - Removes permission level 1 from a user and gets him/her to permission level 0. Requires permission level 2 or higher.
 !invite invitationcode - Nekobot joins the server that the invite represents. invitationcode corresponds to the 0Lv5NLFEoz3P07Aq part of https://discord.gg/0Lv5NLFEoz3P07Aq Requires permission level 1 or higher.
 !say - Replies with everything that follows the command.
 !kona tags - Grabs a random image fitting the tags from http://konachan.com/ Warning: can return a nsfw image, if nothing is returned, the tag is incorrect or doesn't have a minimum of 100 pictures
 !rule34 tags - Grabs a random image from http://rule34.xxx/ Warning: can return a nsfw image, if nothing is returned, tag is incorrect or doesn't have a minimum of 100 pictures
 !gelbooru tags - Grabs a random image from http://gelbooru.com/ Warning: can return a nsfw image, if nothing is returned, tag is incorrect or doesn't have a minimum of 100 pictures
 !cosplay - Grabs a random cosplay image from Salvy's folder - doesn't update, contains 157 images.
 !pitur - Grabs a random lewd image from Pitur's collection - doesn't update, contains 4396 images.
 !gold - Grabs a random kancolle image from Au-chan's collection - updates, though not in real time.
 !waifu - Replies with a random waifu from https://julxzs.website/api/random-waifu
 !nsfw on/off/status - (on/off)Enables or disables the use of nsfw commands in a particular channel. Requires permission level 1 or higher. (status)Tells whether the channel allows the use of nsfw commands. Doesn't require any permission level.
 !music on/off channelid - Enables or disables music streaming in a particular voice channel. Requires permission level 3 for now because doesn't seem to be able to stream to more than one channel at once. Will require permission level 1 or higher if it ever becomes possible to send voice data to more than one server at once.
 !cid channelname - Gets the ID of all channel with the same name.
 !skip - Votes to skip currently playing song, requires user to be in a Nekobot music streaming channel, votes reset at the end of a song. Requires half or more of the amount of people who where in the channel before the song began to vote to skip.
 !forceskip - Force to skip currently playing song, requires user to be in a Nekobot music streaming channel and permission level 1 or higher.
 !song - Returns the ID3 tag title and author if possible, else filename of the currently playing song.
 !replay (!encore) (!ankouru) - Votes to replay the currently playing song after it's done, requires user to be in a Nekobot music streaming channel, votes reset at the end of a song. Requires half or more of the amount of people who where in the channel before the song began to vote to replay.
 !notnow - How to rekt rin 101.
 !sadhorn (!icri) (!aicrai) (!aicraievritiem) (!aicraievritaim) - When sad things happen.
 !commands (!help) - How you got this to show up. Will still send them in PM if you ask in a channel.
That's all for now! Suggest ideas to Kusoneko, might add it at some point.");
/* removed temporarily due to a random bug
 !sidetail - Grabs a random sidetail image from https://danbooru.donmai.us/posts?tags=sidetail Warning: can return a nsfw image
 !futanari (!futa) - Grabs a random futa image from http://danbooru.donmai.us/posts?tags=futanari Warning: can return a nsfw image
 !wincest (!incest) - Grabs a random incest image from https://danbooru.donmai.us/posts?tags=incest Warning: can return a nsfw image
*/
        }

        public static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            //Attempt to stop the shitty "Could not create a secure SSL/TLS channel" error
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(AlwaysGoodCertificate);
            //Initialization
            Console.WriteLine("Initializing Nekobot...");
            LoadCredentials();
            Console.Title = "Nekobot";
            Console.WriteLine("Loading permissions...");
            LoadPermissionFiles();
            permissions[0] = normal;
            permissions[1] = mods;
            permissions[2] = admins;
            permissions[3] = owner;
            Console.WriteLine("Loading channel sfw settings...");
            LoadChannels();
            Console.WriteLine("Loading streaming channels...");
            LoadStreamChannels();
            Console.WriteLine("Connecting...");
            try
            {
                client.Connect(email, pass);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not connect: " + e.Message);
            }

            //Allows the ability to type in the console window while the bot runs, thus allowing to have some console commands and a bot running at the same time.
            Thread input = new Thread(InputThread);
            input.Start();

            //Start music streaming
            StartMusicThreads();

            //Bot events below

            client.Connected += ClientConnected;
            client.Disconnected += ClientDisconnected;
            client.MessageCreated += ClientMessageCreated;
            client.DebugMessage += ClientDebugMessage;
        }

        private static async void StartMusicThreads()
        {
            foreach (string s in streams)
            {
                //DiscordClient _client = new DiscordClient(new DiscordClientConfig() { EnableVoice = true });
                //await _client.Connect(email, pass);
                while (!client.IsConnected)
                    await Task.Delay(1000);
                if (client.GetChannel(s).Type == "voice")
                {
                    Thread music = new Thread(() => StreamMusic(s/*, _client*/));
                    music.Start();
                }
            }
        }

        private static bool AlwaysGoodCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private static void ClientDebugMessage(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine("/!\\DEBUG/!\\: " + e.Message);
        }
    }

    public static class StringExtension
    {
        public static string GetLast(this string source, int tail_length)
        {
            if (tail_length >= source.Length)
                return source;
            return source.Substring(source.Length - tail_length);
        }
    }
/* 64 bit stuffs, but voice doesn't support 64 bits so rolled back to 32 bit until it does
    static class RandomExtensions
    {
        static int NextInt32(this Random rg)
        {
            unchecked
            {
                int firstBits = rg.Next(0, 1 << 4) << 28;
                int lastBits = rg.Next(0, 1 << 28);
                return firstBits | lastBits;
            }
        }

        public static decimal NextDecimal(this Random rg)
        {
            bool sign = rg.Next(2) == 1;
            return rg.NextDecimal(sign);
        }

        static decimal NextDecimal(this Random rg, bool sign)
        {
            byte scale = (byte)rg.Next(29);
            return new decimal(rg.NextInt32(),
                               rg.NextInt32(),
                               rg.NextInt32(),
                               sign,
                               scale);
        }

        static decimal NextNonNegativeDecimal(this Random rg)
        {
            return rg.NextDecimal(false);
        }

        public static decimal NextDecimal(this Random rg, decimal maxValue)
        {
            return (rg.NextNonNegativeDecimal() / Decimal.MaxValue) * maxValue; ;
        }

        public static decimal NextDecimal(this Random rg, decimal minValue, decimal maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new InvalidOperationException();
            }
            decimal range = maxValue - minValue;
            return rg.NextDecimal(range) + minValue;
        }

        static long NextNonNegativeLong(this Random rg)
        {
            byte[] bytes = new byte[sizeof(long)];
            rg.NextBytes(bytes);
            // strip out the sign bit
            bytes[7] = (byte)(bytes[7] & 0x7f);
            return BitConverter.ToInt64(bytes, 0);
        }

        public static long NextLong(this Random rg, long maxValue)
        {
            return (long)((rg.NextNonNegativeLong() / (double)Int64.MaxValue) * maxValue);
        }

        public static long NextLong(this Random rg, long minValue, long maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new InvalidOperationException();
            }
            long range = maxValue - minValue;
            return rg.NextLong(range) + minValue;
        }
    }
*/
}
