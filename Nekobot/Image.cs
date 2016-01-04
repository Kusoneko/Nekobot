using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Nekobot
{
    class Image
    {
        class Board
        {
            Board(string link, string resource, string post)
            {
                Link = link;
                Resource = resource;
                Post = post;
            }
            static Board A(string link, string tags) =>
                new Board(link, $"index.php?page=dapi&s=post&q=index&limit=1&tags={tags}&pid=", "/index.php?page=post&s=view&id=");
            static Board B(string link, string tags) =>
                new Board(link, $"index.json?limit=1&tags={tags}&page=", "/show/");
            static Board Sankaku(string board, string tags) => B($"https://{board}.sankakucomplex.com/post", tags);

            public static Board Get(string booru, string tags)
            {
                var boardconf = (JObject)Program.config["Booru"].SelectToken(booru);
                if (boardconf != null)
                {
                    var default_tags = boardconf.Property("default_tags");
                    if (default_tags != null)
                        tags += string.Join(" ", default_tags.Values());
                }
                tags = System.Net.WebUtility.UrlEncode(tags);
                Board board =
                booru == "safebooru" ? A("http://safebooru.org", tags) :
                //booru == "gelbooru" ? A("http://gelbooru.com", tags) :
                booru == "rule34" ? A("http://rule34.xxx", tags) : null;
                if (board == null) // Type A has no auth in the api.
                {
                    if (boardconf != null)
                    {
                        var login = boardconf.Property("login");
                        if (login != null)
                        {
                            var prop = boardconf.Property("api_key");
                            tags += $"&login={login.Value}&{(prop != null ? $"api_key={prop.Value}" : $"password_hash={boardconf["password_hash"]}")}";
                        }
                    }
                    board =
                    booru == "konachan" ? B("http://konachan.com/post", tags) :
                    booru == "yandere" ? B("https://yande.re/post", tags) :
                    booru == "lolibooru" ? B("http://lolibooru.moe/post", tags) :
                    booru == "sankaku" ? Sankaku("chan", tags) :
                    //booru == "sankakuidol" ? Sankaku("idol", tags) :
                    booru == "e621" ? B("https://e621.net/post", tags)
                    : null;
                }
                return board;
            }

            bool IsSankaku => Link.Contains("sankaku");
            static bool Json(string resource) => resource.StartsWith("index.json");

            public JToken Common(string resource)
            {
                Program.rclient.BaseUrl = new Uri(Link);
                var content = Program.rclient.Execute(new RestRequest(resource, Method.GET)).Content;
                if (Json(resource)) return JObject.Parse(content.Substring(1).Trim(']'));
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(content);
                return JObject.Parse(JsonConvert.SerializeXmlNode(xml))["posts"];
            }

            public string GetImageLink(int rnd)
            {
                var res = Common(Resource + rnd.ToString());
                string prefix = "";
                if (!Json(Resource))
                {
                    res = (JObject)res["post"];
                    prefix = "@";
                }
                return $"**{Link}{Post}{res[$"{prefix}id"].ToString()}** {(IsSankaku ? "http:" : "")}{res[$"{prefix}file_url"].ToString().Replace(" ", "%20")}";
            }

            public int GetPostCount()
            {
                var sankaku = IsSankaku;
                var res = Common(!sankaku ? Resource.Replace("index.json", "index.xml") : Resource);
                return sankaku ? res.ToString() == "" ? 0 : 1000 : int.Parse(res["@count"].ToString());
            }

            public string Link;
            public string Resource;
            public string Post;
        }
        static async Task Booru(string booru, Commands.CommandEventArgs e)
        {
            var tags = string.Join(" ", e.Args);
            Board board = Board.Get(booru, tags);
            for (int i = 10; i != 0; --i)
            {
                try
                {
                    int posts = board.GetPostCount();
                    await e.Channel.SendMessage((posts == 0) ?
                        $"There is nothing under the tag(s):\n{tags}\non {booru}. Please try something else." :
                        board.GetImageLink(posts == 1 ? 0 : (new Random()).Next(1, posts - 1)));
                    return;
                }
                catch { }
            }
            await e.Channel.SendMessage($"Failed ten times, something must be broken with {booru}'s API.");
        }

        static string Folders(string folder)
        {
            string[] imgexts = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var files = from file in System.IO.Directory.EnumerateFiles($@"{folder}", "*.*").Where(s => imgexts.Contains(System.IO.Path.GetExtension(s.ToLower()))) select new { File = file };
            return files.ElementAt(new Random().Next(0, files.Count())).File;
        }

        static async Task LewdSX(string chan, Discord.Channel c)
        {
            Program.rclient.BaseUrl = new Uri("https://lewdchan.com");
            var request = new RestRequest($"{chan}/src/list.php", Method.GET);
            string result = Program.rclient.Execute(request).Content;
            List<string> list = result.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
            Regex re = new Regex(@"([^\s]+(\.(jpg|jpeg|png|gif|bmp)))");
            foreach (Match m in re.Matches(result))
                list.Add(m.Value);
            await c.SendMessage($"https://lewdchan.com/{chan}/src/{list[new Random().Next(0, list.Count())]}");
        }

        static void CreateLewdCommand(Commands.CommandGroupBuilder group, string chan)
        {
            group.CreateCommand(chan)
                .FlagNsfw(true)
                .Description($"I'll give you a random image from https://lewdchan.com/{chan}/")
                .Do(async e => await LewdSX(chan, e.Channel));
        }
        static void CreateFolderCommand(Commands.CommandGroupBuilder group, string name, string type, string owner)
        {
            string folder = Program.config[name].ToString();
            if (folder != "")
                group.CreateCommand(name)
                    .FlagNsfw(true)
                    .Description($"I'll give you a random {type} image from {owner}'s collection")
                    .Do(async e => await e.Channel.SendFile(Folders(folder)));
        }
        static void CreateBooruCommand(Commands.CommandGroupBuilder group, string booru, string alias) => CreateBooruCommand(group, booru, new string[]{alias});
        static void CreateBooruCommand(Commands.CommandGroupBuilder group, string booru, string[] aliases = null)
        {
            var cmd = group.CreateCommand(booru);
            cmd.Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description($"I'll give you a random image from {booru} (optionally with tags)");
            if (aliases != null) foreach (var alias in aliases) cmd.Alias(alias);
            cmd.Do(async e => await Booru(booru, e));
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            CreateLewdCommand(group, "neko");
            CreateLewdCommand(group, "qt");
            CreateLewdCommand(group, "kitsune");
            CreateLewdCommand(group, "lewd");

            CreateFolderCommand(group, "pitur", "lewd", "pitur");
            CreateFolderCommand(group, "gold", "kancolle", "gold");
            CreateFolderCommand(group, "cosplay", "cosplay", "Salvy");

            if (System.IO.Directory.Exists("images"))
            {
                group.CreateCommand("trash")
                    .Alias("worstgirl")
                    .Alias("onodera")
                    .Description("I'll upload an image of 'worst girl'. (WARNING: May cause nausea!)")
                    .Do(async e => await e.Channel.SendFile("images/trash.png"));

                group.CreateCommand("doit")
                    .Alias("justdoit")
                    .Alias("shia")
                    .Description("DON'T LET YOUR DREAMS JUST BE DREAMS!")
                    .Do(async e => await e.Channel.SendFile("images/shia.jpg"));

                group.CreateCommand("bulli")
                    .Alias("bully")
                    .Alias("dunbulli")
                    .Alias("dontbully")
                    .Description("DON'T BULLY!")
                    .Do(async e => await e.Channel.SendFile("images/bulli.jpg"));
            }

            group.CreateCommand("img")
                .Parameter("search query", Commands.ParameterType.Required)
                .Parameter("extended query", Commands.ParameterType.Multiple)
                .Description("I'll get a random image from Google!")
                .AddCheck((h, i, d) => false).Hide() // Until we can  update this to work
                .Do(async e =>
                {
                    Random rnd = new Random();
                    Program.rclient.BaseUrl = new Uri("https://ajax.googleapis.com/ajax/services/search");
                    var request = new RestRequest($"images?v=1.0&q={string.Join(" ", e.Args)}&rsz=8&start={rnd.Next(1, 12)}&safe=active", Method.GET);
                    JObject result = JObject.Parse(Program.rclient.Execute(request).Content);
                    List<string> images = new List<string>();
                    foreach (var element in result["responseData"]["results"])
                        images.Add(element["unescapedUrl"].ToString());
                    await e.Channel.SendMessage(images[rnd.Next(images.Count())].ToString());
                });

            group.CreateCommand("imgur")
                .Parameter("Reddit Board", Commands.ParameterType.Required)
                .Description("I'll pick out a random image from the day's best on an imgur reddit!")
                .Do(async e =>
                {
                    try
                    {
                        Program.rclient.BaseUrl = new Uri("http://imgur.com/r/");
                        var result = JObject.Parse(Program.rclient.Execute(new RestRequest($"{e.Args[0]}/top/day.json", Method.GET)).Content)["data"].First;
                        for (var i = new Random().Next(result.Parent.Count - 1); i != 0; --i, result = result.Next);
                        var part = $"imgur.com/{result["hash"]}";
                        await e.Channel.SendMessage($"**http://{part}** http://i.{part}{result["ext"]}");
                    }
                    catch { await e.Channel.SendMessage("Imgur says nope~"); }
                });

            CreateBooruCommand(group, "safebooru");
            //CreateBooruCommand(group, "gelbooru"); // Disabled without auth, which can't be done through api.
            CreateBooruCommand(group, "rule34");
            CreateBooruCommand(group, "konachan", "kona");
            CreateBooruCommand(group, "yandere");
            CreateBooruCommand(group, "lolibooru", "loli");
            if (Program.config["Booru"].ToObject<JObject>().Property("sankaku") != null)
                CreateBooruCommand(group, "sankaku", new string[]{"sankakuchan", "schan"});
            //CreateBooruCommand(group, "sankakuidol", "sidol"); // Idol disables their API for some reason.
            CreateBooruCommand(group, "e621", "furry");
        }
    }
}
