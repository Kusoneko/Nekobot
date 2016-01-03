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
            public Board(string link, string resource, string post, bool json = false)
            {
                Link = link;
                Resource = resource;
                Post = post;
                Json = json;
            }
            public static Board A(string link, string tags) =>
                new Board(link, $"index.php?page=dapi&s=post&q=index&limit=1&tags={tags}&pid=", "/index.php?page=post&s=view&id=");
            public static Board B(string link, string tags) =>
                new Board(link, $"/index.xml?limit=1&tags={tags}&page=", "/show/");
            public static Board Sankaku(string board, string tags) =>
                new Board($"https://{board}.sankakucomplex.com/post/index.json?limit=1&login=NekobotSearchAccount&password_hash=e43fb40bde1fbee79187504c47745ba03009738b&tags={tags}&page=", "/show/", tags, true);

            public Func<Board, int, JObject> Common => Json ? (Func<Board, int, JObject>)GetBooruCommonJson : GetBooruCommon;

            public string Link;
            public string Resource;
            public string Post;
            public bool Json;
        }
        static async Task Booru(string booru, Commands.CommandEventArgs e)
        {
            var tags = System.Net.WebUtility.UrlEncode(string.Join(" ", e.Args));
            Board board =
                booru == "safebooru" ? Board.A("http://safebooru.org", tags) :
                //booru == "gelbooru" ? Board.A("http://gelbooru.com", tags) :
                booru == "rule34" ? Board.A("http://rule34.xxx", tags) :
                booru == "konachan" ? Board.B("http://konachan.com/post", tags) :
                booru == "yandere" ? Board.B("https://yande.re/post", tags) :
                booru == "lolibooru" ? Board.B("http://lolibooru.moe/post", tags) :
                booru == "sankakuchan" ? Board.Sankaku("chan", tags) :
                //booru == "sankakuidol" ? Board.Sankaku("idol", tags) :
                booru == "e621" ? Board.B("https://e621.net/post", tags)
                : null;
            for (int i = 10; i != 0; --i)
            {
                try
                {
                    int posts = GetBooruPostCount(board);
                    await e.Channel.SendMessage((posts == 0) ?
                        $@"There is nothing under the tag(s):
{System.Net.WebUtility.UrlDecode(tags)}
on {booru}. Please try something else." :
                    GetBooruImageLink(board, posts == 1 ? 0 : (new Random()).Next(1, posts - 1)));
                    return;
                }
                catch { }
            }
            await e.Channel.SendMessage($"Failed ten times, something must be broken with {booru}'s API.");
        }

        static JObject GetBooruCommon(Board board, int rnd)
        {
            Program.rclient.BaseUrl = new Uri(board.Link);
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(Program.rclient.Execute(new RestRequest(board.Resource + rnd.ToString(), Method.GET)).Content);
            return (JObject)JObject.Parse(JsonConvert.SerializeXmlNode(xml))["posts"];
        }

        static JObject GetBooruCommonJson(Board board, int rnd)
        {
            Program.rclient.BaseUrl = new Uri(board.Link);
            return JObject.Parse(Program.rclient.Execute(new RestRequest(board.Resource + rnd.ToString(), Method.GET)).Content.Substring(1).Trim(']'));
        }

        static string GetBooruImageLink(Board board, int rnd)
        {
            var res = board.Common(board, rnd);
            if (!board.Json) res = (JObject)res["post"];
            return "**" + (board.Json ? board.Link.Substring(0, board.Link.LastIndexOf("/")+1) : board.Link) + board.Post + res[$"{(board.Json ? "" : "@")}id"].ToString() + "** " + (board.Json ? $"http:{res["file_url"]}" : res["@file_url"].ToString()).Replace(" ", "%20");
        }

        static int GetBooruPostCount(Board board)
        {
            var res = board.Common(board, 0);
            return board.Json ? res.ToString() == "" ? 0 : 1000 : int.Parse(res["@count"].ToString());
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

            CreateBooruCommand(group, "safebooru");
            //CreateBooruCommand(group, "gelbooru"); // Disabled because of them disabling their API
            CreateBooruCommand(group, "rule34");
            CreateBooruCommand(group, "konachan", "kona");
            CreateBooruCommand(group, "yandere");
            CreateBooruCommand(group, "lolibooru", "loli");
            CreateBooruCommand(group, "sankakuchan", "schan");
            //CreateBooruCommand(group, "sankakuidol", "sidol"); // Idol disables their API for some reason.
            CreateBooruCommand(group, "e621", "furry");
        }
    }
}
