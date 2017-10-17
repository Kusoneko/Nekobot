When updating, be sure to check this file to be sure you'll be set up appropriately.

# Configuration
You'll need to make a config.json file in the same place as the executable using this template:
```javascript
{
    "token" : "MySuperSecretOAuth2Token",
    "SoundCloud" : {},
    "LastFM" : {},
    "CleverBot" : {},
    "WolframAlpha" : {},
    "GoogleCalendar" : {},
    "Booru" : {},
    "master" : 63296013791666176,
    "server" : "https://discord.gg/0Lv5NLFEoz3P07Aq",
    "prefix" : "!",
    "prefixprivate" : true,
    "prefixpublic" : true,
    "mentioncommand" : 1,
    "color" : [232, 1, 255],
    "helpmode" : "private",
    "loglevel" : 2,
    "game" : "Happy Nekobot Fun Times",
    "musicFolder" : "",
    "musicUseSubfolders" : false,
    "gestures" : "",
    "images" : "",
    "roles" : {}
}
```
Quick links to descriptions: [Credentials](#credentials) | [master](#master) | [server](#server) | [prefix, prefixprivate, prefix and mentioncommand](#prefix) | [color](#color) | [helpmode](#helpmode) | [loglevel](#log-level) | [game](#game) | [musicFolder, and musicUseSubfolders](#music-folder) | [gestures](#gesture-folder) | [images](#image-folder) | [Calendar](#google-calendar)

[Further Setup Instructions](#other-setup)

### Credentials
The token line is your bot's password, how do you get one?
Like so: Go to [Discord's Apps page](https://discordapp.com/developers/applications/me) and create a new application, configure it however you like, then hit the button to create it. It'll take you to a page that offers you to create a bot user, do so. You'll be offered a link to click to reveal your bot token, click it and use it for this field.
You might be wondering how to invite your bot to a server, have no fear, all you need to do is create a link `https://discordapp.com/oauth2/authorize?scope=bot&client_id=` and add the Client ID from the App Details section of that page. (If you don't have permissions on the server, give this link to someone who does.)
The email and password lines will go away in the future, so to avoid breakage, do this as soon as you can.

The SoundCloud line holds credentials for SoundCloud, enabling the soundcloud commands. [You'll need to register the app](https://soundcloud.com/you/apps/). At the moment all that's needed is the Client ID `"client_id" : "Put Client ID here"`

The LastFM line holds credentials for LastFM. [Create an API account](http://www.last.fm/api/account/create). You'll need to add in `"apikey" : "Your API key", "apisecret" : "Your Shared secret"`.

The CleverBot line holds credentials for CleverBot.io. [Create an API account](https://cleverbot.io/keys). You'll need to add in `"user" : "Your API User", "key" : "Your API Key"`.

The WolframAlpha line holds credentials for Wolfram Alpha [Sign up for an API account](http://developer.wolframalpha.com/portal/apisignup.html), [Get an AppId](https://developer.wolframalpha.com/portal/myapps/index.html), and add in `"appid" : "Your APPID"` to be get access to the `wolfram` command.

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

### Color
The RGB of the color you would like used with embed responses to commands.
In the format [R, G, B].

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

# Other setup
## Dynamic Commands
These are commands you can add yourself, all of them that are configured via json follow the same basic structure:
```javascript
{
  "Command name here" :
  {
    "aliases" : [],
    "description" : "Some words to describe"
    "permissions" : 0
  }
}
```
All default lines inside the command map are optional, all that's needed from here is the command name, obviously. Use permissions to limit a command to people you've authorized(`setauth`).

### Gestures Folder
Set this to a folder in which you will place for sound clips for commands you wish to played by commands of their name on voice.
If this is an empty string, this feature is turned off.
Include the file gestures.json (as shown below) in this folder if you wish for more granular control over it, or if you want to be able to play soundclips not in this folder (or from youtube). Files in this folder should not be listed in gestures.json.
```javascript
{
  "Command name here" :
  {
    "uris" : ["filename or youtube link1", "filename or youtube link2"]
  }
}
```

### Image Folder
Set this to a folder for which you will create subfolders for commands uploading images you wish to have. If this is an empty string, she'll look in her running directory for "images" subfolder.
Include the file command.json (as shown below) in each subfolder to configure its command, otherwise it'll be sfw and use the subfolder name as its name.
```javascript
{
  "Command name here" :
  {
    "nsfw" : false
  }
}
```

### Google Calendar
[Set up an application to use with the Google Calendar API](https://console.developers.google.com/start/api?id=calendar). Once you've done that, download the json file and put it in the folder with config.json, rename it `calendar_client_secret.json`.
For every calendar you want to have a command for, get its calendar id from [here](http://calendar.google.com/) by clicking the dropdown next to its name and selecting settings (it'll be on the Calendar Address line); unless it's your primary calendar, then you can just put primary.
```javascript
{
  "Command name here" :
  {
    "calendarId" : "primary",
    "hideDescription" : false
  }
}
```
`hideDescription` will determine if the description is to be hidden.
You'll be prompted via a webpage on your browser, on the initial run, to grant permissions to Nekobot.

### Custom Response Commands
If you want to have custom commands that say something (or different things, randomly):
+ Create a file in Nekobot's output directory called custom\_response\_commands.json
+ Model it after the response\_commands.json already in this directory.
If you want to add such commands to the project itself, or tweak existing ones, please contribute to the project's response\_commands.json and let us know!

### Roles Commands
If you want users to be able to obtain and remove certain roles on their own, you may wish to use the Roles submap, its format is, for the most part, straight forward:
```json
{
    "id of a server":
    {
        "Human readable name of role":
	{
	    id: 33333
	    permissions: 2,
	    description: "This is an example role, it will require a minimum permission level of 2 to obtain, its id is 33333."
	},
	"The NSFW role":
	{
	    id: 666666666666666,
	    description: "Sacrifice your soul, become forever tainted!"
	}
    }
}
```
Multiple servers may be specified, the actual name of the role isn't important, it' just what the user will use with the command.`permission` is optional, `description` is necessary.
IDs for roles are obtainable by making the role mentionable and then mentioning the role with a `\` just before it. We highly suggest doing this in a channel that isn't accessible to most users in the role already, so as to avoid pinging them. (Don't forget to make it unmentionable again, if you need to).

For `ytrequest` to work with webm videos, please install the [Required codecs](https://tools.google.com/dlpage/webmmf/).
