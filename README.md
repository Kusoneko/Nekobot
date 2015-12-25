# Nekobot
Version 2.3.74

Bot for Discord using the [Discord.Net](https://github.com/RogueException/Discord.Net) Library.

You'll need to make a config.json file in the same place as the executable using this template:

```javascript
{
    "email" : "myemail@example.com",
    "password" : "MySuperSecretPassword123",
    "SoundCloud" : {},
    "prefix" : "!",
    "master" : 63296013791666176,
    "server" : "https://discord.gg/0Lv5NLFEoz3P07Aq",
    "helpmode" : "private",
    "prefixprivate" : true,
    "prefixpublic" : true,
    "mentioncommand" : 1,
    "musicFolder" : "",
    "musicUseSubfolders" : false,
    "pitur" : "",
    "gold" : "",
    "cosplay" : ""
}
```

The email and password lines are self explanatory.
The SoundCloud line holds credentials for SoundCloud, enabling the soundcloud commands. [You'll need to register the app](https://soundcloud.com/you/apps/). At the moment all that's needed is the Client ID `"client_id" : "Put Client ID here"`
The prefix is a list of characters that can be the character used in front of literally every command.
As for the master, it's value should be the 17-digits id of the Discord account that should be recognized as the master. (The master id gets a level 10 permission level, although no official commands are above level 3.)
The server line allows you to make the bot join a server on startup using that. The value can either be the full invite link as shown above, or only the characters at the end (ex.: the "0Lv5NLFEoz3P07Aq" part of "https://discord.gg/0Lv5NLFEoz3P07Aq".)
If the server line is empty, the bot will not join any channel, and to make it be of use, you'll have to manually connect into the bot's account and join a channel.
The helpmode setting has three settings "public", "private", and disabled, if it's disabled, there'll be no help. If it's public, the help command will be responded to in the channel it's issued. If it's private, responses will be in PM.
The prefixprivate setting requires the use of prefix in PMs when true
The prefixpublic setting requires the use of prefix in channels when true
The mentioncommand setting allows @mentioning the bot instead of using prefix in channels when 1, when 2 allows you to mention after the command and its args as well.
The musicFolder setting should be set to the full path to a folder containing a bunch of music files to be used for music streaming.
The musicUseSubfolders setting is whether or not to include files buried in folders within your musicFolder.
The pitur, gold and cosplay settings should be set to full paths to image folders containing whatever images you please. If they're not set, the commands will be disabled.

To add new commands to the bot, look at the GenerateCommands method's content in the Program.cs file, all the commands are being created from there at startup, therefore you should easily be able to add your own commands to it, by copying the way the official commands are made.

Server where the bot is being tested: https://discord.gg/0Lv5NLFEoz3P07Aq

Requires the following NuGet Packages (Latest official version will do):
- [RestSharp](https://www.nuget.org/packages/RestSharp)
- [System.Data.SQLite](https://www.nuget.org/packages/System.Data.SQLite/)
- [NAudio](https://www.nuget.org/packages/NAudio)
- [NAudio.Vorbis](https://www.nuget.org/packages/NAudio.Vorbis)
- [NVorbis](https://www.nuget.org/packages/NVorbis)
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json)
- [SoundCloud.NET](https://www.nuget.org/packages/SoundCloud.NET)
- [TagLib#](https://www.nuget.org/packages/taglib)
- [VideoLibrary](https://www.nuget.org/packages/VideoLibrary)

For `ytrequest` to work with webm videos, please install the [Required codecs](https://tools.google.com/dlpage/webmmf/).
