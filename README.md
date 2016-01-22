# Nekobot
Version 2.5.32

Bot for Discord using the [Discord.Net](https://github.com/RogueException/Discord.Net) Library.

Please read [this](Configure.md) to learn how to configure the bot.

To add new commands to the bot, look at the GenerateCommands method's content in the Program.cs file, all the commands are being created from there at startup, therefore you should easily be able to add your own commands to it, by copying the way the official commands are made.

Server where the bot is being tested: https://discord.gg/0Lv5NLFEoz3P07Aq

Requires the following NuGet Packages (Latest official version will do):
- [RestSharp](https://www.nuget.org/packages/RestSharp)
- [System.Data.SQLite](https://www.nuget.org/packages/System.Data.SQLite/)
- [Inflatable.Lastfm](https://www.nuget.org/packages/Inflatable.Lastfm/) [From this nuget repo](https://ci.appveyor.com/nuget/lastfm)
- [NAudio](https://www.nuget.org/packages/NAudio)
- [NAudio.Vorbis](https://www.nuget.org/packages/NAudio.Vorbis)
- [NVorbis](https://www.nuget.org/packages/NVorbis)
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)
- [TagLib#](https://www.nuget.org/packages/taglib)
- [VideoLibrary](https://www.nuget.org/packages/VideoLibrary)
