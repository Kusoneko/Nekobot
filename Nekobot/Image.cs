using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
                A_HTTP_NEEDED, // Stupid boards that violate reason when returning "file_url"
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
            static Board A(string link, bool needs_http = false, bool shorten = false) =>
                new Board(link, $"index.php?page=dapi&s=post&q=index&limit=1&pid=", "/index.php?page=post&s=view&id=", needs_http ? Type.A_HTTP_NEEDED : Type.A, shorten);
            static Board B(string link, bool shorten = true, Type type = Type.B) =>
                new Board(link, $"post/index.json?limit=1&page=", "/post/show/", type, shorten);
            static Board Sankaku(string board) => B($"https://{board}.sankakucomplex.com", false, Type.Sankaku);

            public static Board Get(string booru, string tags)
            {
                Board board =
                booru == "safebooru" ? A("http://safebooru.org") :
                //booru == "gelbooru" ? A("http://gelbooru.com") :
                booru == "rule34" ? A("http://rule34.xxx", true) :
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
                        tags += ' ' + string.Join(" ", default_tags.Values());
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
                board._rclient.AddDefaultParameter("tags", tags);
                return board;
            }

            public JToken Common(string resource, bool json)
            {
                var content = _rclient.Execute(new RestRequest(resource, Method.GET)).Content;
                if (json) return JObject.Parse(content.TrimStart('[').TrimEnd(']'));
                return Helpers.XmlToJson(content)["posts"];
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
                return $"**{Link}{Post}{res[$"{prefix}id"].ToString()}** {(_type == Type.A_HTTP_NEEDED || _type == Type.Sankaku ? "http:" : "")}{GetFileUrl(res, prefix)}";
            }

            public int GetPostCount()
            {
                var type_a = _type < Type.B;
                var sankaku = !type_a && _type == Type.Sankaku;
                var res = Common(!type_a && !sankaku ? "post/index.xml?limit=1" : Resource, sankaku);
                return sankaku ? res.ToString() == "" ? 0 : 1000
                    : res["@count"].ToObject<int>();
            }

            public static async Task Execute(string booru, Commands.CommandEventArgs e)
            {
                var tags = string.Join(" ", e.Args);
                var board = Get(booru, tags);
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

            public string Link;
            public string Resource;
            public string Post;
            private Type _type;
            private bool _shorten;
            private RestClient _rclient;
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
            cmd.Do(e => Task.Run(() => Board.Execute(booru, e)));
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            CreateLewdCommand(group, "neko");
            CreateLewdCommand(group, "qt");
            CreateLewdCommand(group, "kitsune");
            CreateLewdCommand(group, "lewd");

            var imagedir = Program.config["images"].ToString();
            if (imagedir == "") imagedir = "images";
            if (System.IO.Directory.Exists(imagedir))
            {
                string[] imgexts = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                foreach (var subdir in System.IO.Directory.EnumerateDirectories(imagedir))
                {
                    var cmd_data = Helpers.GetJsonFileIfExists($"{subdir}/command.json");
                    Helpers.CreateJsonCommand(group,
                        cmd_data != null ? cmd_data["command"].ToString() : subdir.Substring(subdir.LastIndexOf('\\') + 1),
                        cmd_data, cmd => {
                            if (cmd_data != null)
                            {
                                cmd.FlagNsfw(cmd_data["nsfw"].ToObject<bool>());
                            }
                            cmd.Do(async e =>
                            {
                                var files = from file in System.IO.Directory.EnumerateFiles($@"{subdir}", "*.*").Where(s => imgexts.Contains(System.IO.Path.GetExtension(s.ToLower()))) select new { File = file };
                                await e.Channel.SendFile(files.ElementAt(new Random().Next(0, files.Count())).File);
                            });
                        });
                }
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
            if (Helpers.FieldExists("Booru", "sankaku"))
                CreateBooruCommand(group, "sankaku", new[]{"sankakuchan", "schan"});
            //CreateBooruCommand(group, "sankakuidol", "sidol"); // Idol disables their API for some reason.
            CreateBooruCommand(group, "e621", "furry");

            group.CreateCommand("meme")
                .Parameter("Meme type (see memegen.link)")
                .Parameter("Top/Bottom", Commands.ParameterType.Multiple)
                .Description("http://memegen.link/xy/MakeAllTheMemes.jpg")
                .Do(e => e.Channel.SendMessage($"http://memegen.link/{e.Args[0]}{(e.Args.Length == 1 ? "" : $"/{string.Join("-", e.Args, 1, e.Args.Length - 1).ToLower()}")}.jpg"));
        }
    }
}
