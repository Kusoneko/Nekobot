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
    partial class Program
    {
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
                catch (Exception) { }
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
            return "**" + board.link + board.post + res["posts"]["post"]["@id"].ToString() + "** " + res["posts"]["post"]["@file_url"].ToString().Replace(" ", "%20");
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
            return $"https://lewdchan.com/{chan}/src/{list[new Random().Next(0, list.Count())]}";
        }
    }
}
