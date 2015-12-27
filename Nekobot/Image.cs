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
                    .Do(async e => await e.Channel.SendFile(ImageFolders(folder)));
        }
        static Commands.CommandBuilder CreateBooruCommand(Commands.CommandGroupBuilder group, string booru)
        {
            var cmd = group.CreateCommand(booru);
                cmd
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description($"I'll give you a random image from {booru} (optionally with tags)")
                .Do(async e => await ImageBooru(booru, e));
            return cmd;
        }

        class Board : Tuple<string, string, string>
        {
            public Board(string link, string resource, string post) : base(link, resource, post) { }
            public static Board A(string link, string tags) =>
                new Board(link, $"index.php?page=dapi&s=post&q=index&limit=1&tags={tags}&pid=", "/index.php?page=post&s=view&id=");
            public static Board B(string link, string tags) =>
                new Board(link, $"/index.xml?limit=1&tags={tags}&page=", "/show/");
            public string link { get { return Item1; } }
            public string resource { get { return Item2; } }
            public string post { get { return Item3; } }
        }
        static async Task ImageBooru(string booru, Commands.CommandEventArgs e)
        {
            var tags = System.Net.WebUtility.UrlEncode(string.Join(" ", e.Args));
            Board board =
                booru == "safebooru" ? Board.A("http://safebooru.org", tags) :
                booru == "gelbooru" ? Board.A("http://gelbooru.com", tags) :
                booru == "rule34" ? Board.A("http://rule34.xxx", tags) :
                booru == "konachan" ? Board.B("http://konachan.com/post", tags) :
                booru == "yandere" ? Board.B("https://yande.re/post", tags) :
                booru == "lolibooru" ? Board.B("http://lolibooru.moe/post", tags) :
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
            Program.rclient.BaseUrl = new Uri(board.link);
            XmlDocument xml = new XmlDocument();
            xml.LoadXml(Program.rclient.Execute(new RestRequest(board.resource + rnd.ToString(), Method.GET)).Content);
            return JObject.Parse(JsonConvert.SerializeXmlNode(xml));
        }

        static string GetBooruImageLink(Board board, int rnd)
        {
            JObject res = GetBooruCommon(board, rnd);
            return "**" + board.link + board.post + res["posts"]["post"]["@id"].ToString() + "** " + res["posts"]["post"]["@file_url"].ToString().Replace(" ", "%20");
        }

        static int GetBooruPostCount(Board board) => int.Parse(GetBooruCommon(board, 0)["posts"]["@count"].ToString());

        static string ImageFolders(string folder)
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
            CreateBooruCommand(group, "gelbooru").AddCheck((h, i, d) => false).Hide(); // Disabled because of them disabling their API
            CreateBooruCommand(group, "rule34");
            CreateBooruCommand(group, "konachan").Alias("kona");
            CreateBooruCommand(group, "yandere");
            CreateBooruCommand(group, "lolibooru").Alias("loli");
            CreateBooruCommand(group, "e621").Alias("furry");
        }
    }
}
