using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Nekobot
{
    class Image
    {
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
        static string ImageBooru(string booru, string[] targs)
        {
            var tags = System.Net.WebUtility.UrlEncode(string.Join(" ", targs));
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
                    if (posts == 0)
                        return $@"There is nothing under the tag(s):
{System.Net.WebUtility.UrlDecode(tags)}
on {booru}. Please try something else.";
                    return GetBooruImageLink(board, posts == 1 ? 0 : (new Random()).Next(1, posts - 1));
                }
                catch (Exception) { }
            }
            return $"Failed ten times, something must be broken with {booru}'s API.";
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

        static string LewdSX(string chan)
        {
            Program.rclient.BaseUrl = new Uri("https://lewdchan.com");
            var request = new RestRequest($"{chan}/src/list.php", Method.GET);
            string result = Program.rclient.Execute(request).Content;
            List<string> list = result.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList();
            Regex re = new Regex(@"([^\s]+(\.(jpg|jpeg|png|gif|bmp)))");
            foreach (Match m in re.Matches(result))
            {
                list.Add(m.Value);
            }
            return $"https://lewdchan.com/{chan}/src/{list[new Random().Next(0, list.Count())]}";
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("neko")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/neko/")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, LewdSX("neko"));
                });

            group.CreateCommand("qt")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/qt/")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, LewdSX("qt"));
                });

            group.CreateCommand("kitsune")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/kitsune/")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, LewdSX("kitsune"));
                });

            group.CreateCommand("lewd")
                .FlagNsfw(true)
                .Description("I'll give you a random image from https://lewdchan.com/lewd/")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, LewdSX("lewd"));
                });

            string pitur = Program.config["pitur"].ToString();
            if (pitur != "")
            {
                group.CreateCommand("pitur")
                    .FlagNsfw(true)
                    .Description("I'll give you a random lewd image from pitur's hentai collection")
                    .Do(async e =>
                    {
                        await Program.client.SendFile(e.Channel, ImageFolders(pitur));
                    });
            }

            string gold = Program.config["gold"].ToString();
            if (gold != "")
            {
                group.CreateCommand("gold")
                    .FlagNsfw(true)
                    .Description("I'll give you a random kancolle image from gold's collection")
                    .Do(async e =>
                    {
                        await Program.client.SendFile(e.Channel, ImageFolders(gold));
                    });
            }

            string cosplay = Program.config["cosplay"].ToString();
            if (cosplay != "")
            {
                group.CreateCommand("cosplay")
                    .FlagNsfw(true)
                    .Description("I'll give you a random cosplay image from Salvy's collection")
                    .Do(async e =>
                    {
                        await Program.client.SendFile(e.Channel, ImageFolders(cosplay));
                    });
            }

            if (System.IO.Directory.Exists("images"))
            {
                group.CreateCommand("trash")
                    .Alias("worstgirl")
                    .Alias("onodera")
                    .Description("I'll upload an image of 'worst girl'. (WARNING: May cause nausea!)")
                    .Do(async e =>
                    {
                        await Program.client.SendFile(e.Channel, "images/trash.png");
                    });

                group.CreateCommand("doit")
                    .Alias("justdoit")
                    .Alias("shia")
                    .Description("DON'T LET YOUR DREAMS JUST BE DREAMS!")
                    .Do(async e =>
                    {
                        await Program.client.SendFile(e.Channel, "images/shia.jpg");
                    });

                group.CreateCommand("bulli")
                    .Alias("bully")
                    .Alias("dunbulli")
                    .Alias("dontbully")
                    .Description("DON'T BULLY!")
                    .Do(async e =>
                    {
                        await Program.client.SendFile(e.Channel, "images/bulli.jpg");
                    });
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
                    {
                        images.Add(element["unescapedUrl"].ToString());
                    }
                    var imageURL = images[rnd.Next(images.Count())].ToString();
                    await Program.client.SendMessage(e.Channel, imageURL);
                });

            group.CreateCommand("safebooru")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from safebooru.")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, ImageBooru("safebooru", e.Args));
                });

            group.CreateCommand("gelbooru")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .AddCheck((h, i, d) => false).Hide() // Disabled because of them disabling their API
                .Description("I'll give you a random image of the tags you entered from gelbooru.")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, ImageBooru("gelbooru", e.Args));
                });

            group.CreateCommand("rule34")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from rule34.")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, ImageBooru("rule34", e.Args));
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
                    await Program.client.SendMessage(e.Channel, ImageBooru("konachan", e.Args));
                });

            group.CreateCommand("yandere")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from yandere.")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, ImageBooru("yandere", e.Args));
                });

            group.CreateCommand("lolibooru")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image of the tags you entered from lolibooru.")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, ImageBooru("lolibooru", e.Args));
                });

            group.CreateCommand("e621")
                .Parameter("[-]tag1", Commands.ParameterType.Optional)
                .Parameter("[-]tag2", Commands.ParameterType.Optional)
                .Parameter("[-]tagn", Commands.ParameterType.Multiple)
                .FlagNsfw(true)
                .Description("I'll give you a random image from e621 (optionally with tags)")
                .Do(async e =>
                {
                    await Program.client.SendMessage(e.Channel, ImageBooru("e621", e.Args));
                });
        }
    }
}
