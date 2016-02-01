When updating, be sure to check this file to be sure you'll be set up appropriately.

# Configuration
You'll need to make a config.json file in the same place as the executable using this template:
```javascript
{
    "email" : "myemail@example.com",
    "password" : "MySuperSecretPassword123",
    "SoundCloud" : {},
    "LastFM" : {},
    "Booru" : {},
    "master" : 63296013791666176,
    "server" : "https://discord.gg/0Lv5NLFEoz3P07Aq",
    "prefix" : "!",
    "prefixprivate" : true,
    "prefixpublic" : true,
    "mentioncommand" : 1,
    "helpmode" : "private",
    "loglevel" : 2,
    "game" : "Happy Nekobot Fun Times",
    "musicFolder" : "",
    "musicUseSubfolders" : false,
    "images" : ""
}
```
Quick links to descriptions: [Credentials](#credentials) | [master](#master) | [server](#server) | [prefix, prefixprivate, prefix and mentioncommand](#prefix) | [helpmode](#helpmode) | [loglevel](#log-level) | [game](#game) | [musicFolder, and musicUseSubfolders](#music-folder) | [images](#image-folder)

[Further Setup Instructions](#other-setup)

### Credentials
The email and password lines are self explanatory.

The SoundCloud line holds credentials for SoundCloud, enabling the soundcloud commands. [You'll need to register the app](https://soundcloud.com/you/apps/). At the moment all that's needed is the Client ID `"client_id" : "Put Client ID here"`

The LastFM line holds credentials for LastFM. [Create an API account](http://www.last.fm/api/account/create). You'll need to add in `"apikey" : "Your API key", "apisecret" : "Your Shared secret"`.

The Booru map holds submaps of information(credentials and default tags) for the boorus we connect to, this allows you to increase operability depending on the booru.
+ If you specify a `"default_tags" : []`, you'll be able to require or blacklist certain tags in booru queries.
+ Some apis need credentials to work at all, or to work better.
  + The names of the subarrays can be found in Image.cs inside the Image.Board.Get function.
  + the properties needed in these arrays are `"login"` and either `"api_key"` or `"password_hash"`, the booru's site should tell you what should go here.

### Master
The 17-digits id of the Discord account that should be recognized as the master. (The master id gets a level 10 permission level, although no official commands are above level 4.)

### Server
Allows you to make the bot join a server on startup using that. The value can either be the full invite link as shown above, or only the characters at the end (ex.: the "0Lv5NLFEoz3P07Aq" part of "https://discord.gg/0Lv5NLFEoz3P07Aq".)

If empty, the bot will not join any new servers on startup.

### Prefix
A list of characters that can be the character used in front of literally every command.
+ The prefixprivate setting requires the use of prefix in PMs when true.
+ The prefixpublic setting requires the use of prefix in channels when true.

The mentioncommand setting allows @mentioning the bot instead of using prefix in channels when 1, when 2 allows you to mention after the command and its args as well.

### HelpMode
There are three settings, "public", "private", and disabled, if it's disabled, there'll be no help. If it's public, the help command will be responded to in the channel it's issued. If it's private, responses will be in PM.

### Log Level
How verbose your console output should be, between 1 and 5; 5 being the noisiest, you probably don't want that.
Nothing above 3 will show in Release builds.

### Game
The game Nekobot will be shown as playing by default. (Empty string will be no game).

### Music Folder
The full path to a folder containing the music files to be used for music streaming.
If empty, the `request` command will be disabled, and music will only be played when a command is issued to queue music from elsewhere.

The musicUseSubfolders setting is whether or not to include files buried in folders within your musicFolder.

### Image Folder
Set this to a folder for which you will create subfolders for commands uploading images you wish to have. If this is an empty string, she'll look in her running directory for "images" subfolder.
Include the file command.json (as shown below) in each subfolder to configure its command, otherwise it'll be sfw and use the subfolder name as its name.
```javascript
{
  "command" : "name",
  "aliases" : [],
  "description" : "Some words to describe",
  "nsfw" : false
}
```

# Other setup
If you want to have custom commands that say something (or different things, randomly):
+ Create a file in Nekobot's output directory called custom\_response\_commands.json
+ Model it after the response\_commands.json already in this directory.
If you want to add such commands to the project itself, or tweak existing ones, please contribute to the project's response\_commands.json and let us know!

For `ytrequest` to work with webm videos, please install the [Required codecs](https://tools.google.com/dlpage/webmmf/).
