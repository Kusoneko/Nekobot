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
            enum Type
            {
                A, // Sends XML responses, doesn't offer JSON
                B, // Anything >= this uses json, api is more clearly defined. We'll use xml to get count for this type.
                Sankaku, // Nasty, doesn't support xml response (needed for count), we'll just consider there to be 1000 pages to choose from if there are any at all.
            }
            Board(string link, string resource, string post, Type type, bool shorten)
            {
                Link = link;
                Resource = resource;
                Post = post;
                _type = type;
                _shorten = shorten;
                _rclient = Helpers.GetRestClient(Link);
            }
            static Board A(string link, bool shorten = false) =>
                new Board(link, $"index.php?page=dapi&s=post&q=index&limit=1&pid=", "/index.php?page=post&s=view&id=", Type.A, shorten);
            static Board B(string link, bool shorten = true, Type type = Type.B) =>
                new Board(link, $"post/index.json?limit=1&page=", "/post/show/", type, shorten);
            static Board Sankaku(string board) => B($"https://{board}.sankakucomplex.com", false, Type.Sankaku);

            public static Board Get(string booru, string tags)
            {
                Board board =
                booru == "safebooru" ? A("http://safebooru.org") :
                //booru == "gelbooru" ? A("http://gelbooru.com") :
                booru == "rule34" ? A("http://rule34.xxx") :
                booru == "konachan" ? B("http://konachan.com") :
                booru == "yandere" ? B("https://yande.re") :
                booru == "lolibooru" ? B("http://lolibooru.moe") :
                booru == "sankaku" ? Sankaku("chan") :
                //booru == "sankakuidol" ? Sankaku("idol") :
                booru == "e621" ? B("https://e621.net", false)
                : null;

                var boardconf = (JObject)Program.config["Booru"].SelectToken(booru);
                if (boardconf != null)
                {
                    var default_tags = boardconf.Property("default_tags");
                    if (default_tags != null)
                        tags += string.Join(" ", default_tags.Values());
                    if (board?._type >= Type.B) // Type A has no auth in the api.
                    {
                        var login = boardconf.Property("login");
                        if (login != null)
                        {
                            board._rclient.AddDefaultParameter("login", login.Value);
                            var prop = boardconf.Property("api_key");
                            if (prop != null)
                                board._rclient.AddDefaultParameter("api_key", prop.Value);
                            else
                                board._rclient.AddDefaultParameter("password_hash", boardconf["password_hash"]);
                        }
                    }
                }
                board._rclient.AddDefaultParameter("tags", System.Net.WebUtility.UrlEncode(tags));
                return board;
            }

            public JToken Common(string resource, bool json)
            {
                var content = _rclient.Execute(new RestRequest(resource, Method.GET)).Content;
                if (json) return JObject.Parse(content.TrimStart('[').TrimEnd(']'));
                XmlDocument xml = new XmlDocument();
                xml.LoadXml(content);
                return JObject.Parse(JsonConvert.SerializeXmlNode(xml))["posts"];
            }

            private string GetFileUrl(JToken res, string prefix)
            {
                var ret = res[$"{prefix}file_url"].ToString();
                if (!_shorten) return ret;
                var md5 = res[$"{prefix}md5"].ToString();
                return ret.Substring(0, ret.IndexOf(md5)+md5.Length) + ret.Substring(ret.LastIndexOf('.'));
            }

            public string GetImageLink(int rnd)
            {
                var json = _type >= Type.B;
                var res = Common(Resource + rnd.ToString(), json);
                string prefix = !json ? "@" : "";
                if (!json) res = (JObject)res["post"];
                return $"**{Link}{Post}{res[$"{prefix}id"].ToString()}** {(_type == Type.Sankaku ? "http:" : _type == Type.Danbooru ? _rclient.BaseUrl.ToString() : "")}{GetFileUrl(res, prefix)}";
            }

            public int GetPostCount()
            {
                var sankaku = _type == Type.Sankaku;
                var res = Common(!sankaku ? "post/index.xml?limit=1" : Resource, sankaku);
                return sankaku ? res.ToString() == "" ? 0 : 1000
                    : res["@count"].ToObject<int>();
            }

            public string Link;
            public string Resource;
            public string Post;
            private Type _type;
            private bool _shorten;
            private RestClient _rclient;
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
                    await e.Channel.SendMessage(posts == 0 ?
                        $"There is nothing under the tag(s):\n{tags}\non {booru}. Please try something else." :
                        board.GetImageLink(posts == 1 ? 0 : new Random().Next(1, posts - 1)));
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
            string result = Helpers.GetRestClient("https://lewdchan.com").Execute(new RestRequest($"{chan}/src/list.php", Method.GET)).Content;
            List<string> list = result.Split(new[]{ Environment.NewLine }, StringSplitOptions.None).ToList();
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
                    .Do(e => e.Channel.SendFile(Folders(folder)));
        }
        static void CreateBooruCommand(Commands.CommandGroupBuilder group, string booru, string alias) => CreateBooruCommand(group, booru, new[]{alias});
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
                    .Do(e => e.Channel.SendFile("images/trash.png"));

                group.CreateCommand("doit")
                    .Alias("justdoit")
                    .Alias("shia")
                    .Description("DON'T LET YOUR DREAMS JUST BE DREAMS!")
                    .Do(e => e.Channel.SendFile("images/shia.jpg"));

                group.CreateCommand("bulli")
                    .Alias("bully")
                    .Alias("dunbulli")
                    .Alias("dontbully")
                    .Description("DON'T BULLY!")
                    .Do(e => e.Channel.SendFile("images/bulli.jpg"));
            }

            group.CreateCommand("img")
                .Parameter("search query", Commands.ParameterType.Required)
                .Parameter("extended query", Commands.ParameterType.Multiple)
                .Description("I'll get a random image from Google!")
                .AddCheck((h, i, d) => false).Hide() // Until we can  update this to work
                .Do(e =>
                {
                    Random rnd = new Random();
                    var request = new RestRequest($"images?v=1.0&q={string.Join(" ", e.Args)}&rsz=8&start={rnd.Next(1, 12)}&safe=active", Method.GET);
                    JObject result = JObject.Parse(Helpers.GetRestClient("https://ajax.googleapis.com/ajax/services/search").Execute(request).Content);
                    List<string> images = new List<string>();
                    foreach (var element in result["responseData"]["results"])
                        images.Add(element["unescapedUrl"].ToString());
                    e.Channel.SendMessage(images[rnd.Next(images.Count())].ToString());
                });

            group.CreateCommand("imgur")
                .Parameter("Reddit Board", Commands.ParameterType.Required)
                .Description("I'll pick out a random image from the day's best on an imgur reddit!")
                .Do(e =>
                {
                    try
                    {
                        var result = JObject.Parse(Helpers.GetRestClient("http://imgur.com/r/").Execute(new RestRequest($"{e.Args[0]}/top/day.json", Method.GET)).Content)["data"].First;
                        for (var i = new Random().Next(result.Parent.Count - 1); i != 0; --i, result = result.Next);
                        var part = $"imgur.com/{result["hash"]}";
                        e.Channel.SendMessage($"**http://{part}** http://i.{part}{result["ext"]}");
                    }
                    catch { e.Channel.SendMessage("Imgur says nope~"); }
                });

            CreateBooruCommand(group, "safebooru");
            //CreateBooruCommand(group, "gelbooru"); // Disabled without auth, which can't be done through api.
            CreateBooruCommand(group, "rule34");
            CreateBooruCommand(group, "konachan", "kona");
            CreateBooruCommand(group, "yandere");
            CreateBooruCommand(group, "lolibooru", "loli");
            if (Program.config["Booru"].ToObject<JObject>().Property("sankaku") != null)
                CreateBooruCommand(group, "sankaku", new[]{"sankakuchan", "schan"});
            //CreateBooruCommand(group, "sankakuidol", "sidol"); // Idol disables their API for some reason.
            CreateBooruCommand(group, "e621", "furry");
        }
    }
}
