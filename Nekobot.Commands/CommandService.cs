using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace Nekobot.Commands
{
    public partial class CommandService
    {
        private readonly List<Command> _allCommands;
        private readonly Dictionary<string, CommandMap> _categories;
        private readonly CommandMap _map; //Command map stores all commands by their input text, used for fast resolving and parsing

        public CommandServiceConfig Config { get; }
        public CommandGroupBuilder Root { get; }
        public DiscordSocketClient Client { get; private set; }
        public Permissions.Levels.PermissionLevelService PermsService;

        //AllCommands store a flattened collection of all commands
        public IEnumerable<Command> AllCommands => _allCommands;

        private Func<IMessageChannel, bool> _getNsfwFlag;
        private Func<IVoiceState, bool> _getMusicFlag;
        private Func<IMessageChannel, IUser, bool> _getIgnoredChannelFlag;

        //Groups store all commands by their module, used for more informative help
        internal IEnumerable<CommandMap> Categories => _categories.Values;

        //Allow stuff to happen when we don't handle a command.
        public event Action<IMessage> NonCommands = delegate { };

        public event EventHandler<CommandEventArgs> CommandExecuted = delegate { };
        public event Func<CommandErrorEventArgs, Task> CommandErrored
        {
            add { _commandErrored.Add(value); }
            remove { _commandErrored.Remove(value); }
        }
        private readonly AsyncEvent<Func<CommandErrorEventArgs, Task>> _commandErrored = new AsyncEvent<Func<CommandErrorEventArgs, Task>>();

        private void OnCommand(CommandEventArgs args)
            => CommandExecuted(this, args);
        private async Task OnCommandError(CommandErrorType errorType, CommandEventArgs args, Exception ex = null)
            => await _commandErrored.InvokeAsync(new CommandErrorEventArgs(errorType, args, ex)).ConfigureAwait(false);

        public CommandService(CommandServiceConfig config, Func<IMessageChannel, bool> getNsfwFlag = null, Func<IVoiceState, bool> getMusicFlag = null, Func<IMessageChannel, IUser, bool> getIgnoredChannelFlag = null)
        {
            Config = config;

            _getNsfwFlag = getNsfwFlag;
            _getMusicFlag = getMusicFlag;
            _getIgnoredChannelFlag = getIgnoredChannelFlag;
            _allCommands = new List<Command>();
            _map = new CommandMap();
            _categories = new Dictionary<string, CommandMap>();
            Root = new CommandGroupBuilder(this);
        }

        public void Install(DiscordSocketClient client)
        {
            Client = client;
            var self = client.CurrentUser;
            Config.Lock();

            if (Config.HelpMode != HelpMode.Disabled)
            {
                CreateCommand("help")
                    .Parameter("command", ParameterType.Multiple)
                    .Hide()
                    .Description("Returns information about commands.")
                    .Do(async e =>
                    {
                        var replyChannel = Config.HelpMode == HelpMode.Public ? e.Channel : (IMessageChannel)await e.User.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        if (e.Args.Length > 0) //Show command help
                        {
                            var map = _map.GetItem(string.Join(" ", e.Args));
                            if (map != null)
                                await ShowCommandHelp(map, e.User, e.Channel, replyChannel).ConfigureAwait(false);
                            else
                                await replyChannel.SendMessageAsync("Unable to display help: Unknown command.").ConfigureAwait(false);
                        }
                        else //Show general help
                            await ShowGeneralHelp(e.User, e.Channel, replyChannel).ConfigureAwait(false);
                    });
            }

            client.MessageReceived += async e =>
            {
                if (_allCommands.Count == 0)  return;
                if (e.Author == null || e.Author.Id == Client.CurrentUser.Id) return;

                string msg = e.Content;
                if (msg.Length == 0) return;

                // Check ignored before doing work
                if (_getIgnoredChannelFlag != null ? _getIgnoredChannelFlag(e.Channel, e.Author) : false)
                    return;

                //Check for command char if one is provided
                var chars = Config.CommandChars;
                bool mentionreq = Config.MentionCommandChar >= 1;
                bool priv = e.Channel is IPrivateChannel;
                if (chars.Any() || mentionreq)
                {
                    bool hasCommandChar = chars.Contains(msg[0]);
                    if (!hasCommandChar && (priv ? Config.RequireCommandCharInPrivate : Config.RequireCommandCharInPublic))
                    {
                        if (mentionreq && e.MentionedUsers.Contains(self))
                        {
                            string neko = !priv && !string.IsNullOrEmpty((await (e.Channel as IGuildChannel).Guild.GetUserAsync(self.Id)).Nickname) ? $"<@!{client.CurrentUser.Id}>" : $"<@{client.CurrentUser.Id}>";
                            if (neko.Length+2 > msg.Length)
                            {
                                NonCommands(e);
                                return;
                            }
                            if (msg.StartsWith(neko))
                                msg = msg.Substring(neko.Length+1);
                            else
                            {
                                int index = Config.MentionCommandChar > 1 ? msg.LastIndexOf(neko) : -1;
                                if (index == -1)
                                {
                                    NonCommands(e);
                                    return;
                                }
                                msg = msg.Substring(0, index-1);
                            }
                            // Ideally, don't let the command know that we were mentioned, if this is the only mention
                            /*if (msg.IndexOf(neko) != -1)
                            {
                                e.Message.MentionedUsers = e.Message.MentionedUsers.Where(u => u == e.Server.CurrentUser);
                                e.Message.IsMentioningMe = false;
                            }*/
                        }
                        else
                        {
                            NonCommands(e);
                            return;
                        }
                    }
                    else if (hasCommandChar)
                        msg = msg.Substring(1);
                }

                //Parse command
                CommandParser.ParseCommand(msg, _map, out var commands, out int argPos);
                if (commands == null)
                {
                    CommandEventArgs errorArgs = new CommandEventArgs(e, null, null);
                    await OnCommandError(CommandErrorType.UnknownCommand, errorArgs);
                    NonCommands(e);
                    return;
                }
                else
                {
                    foreach (var command in commands)
                    {
                        //Parse arguments
                        var error = CommandParser.ParseArgs(msg, argPos, command, out var args);
                        if (error != null)
                        {
                            if (error == CommandErrorType.BadArgCount)
                                continue;
                            else
                            {
                                var errorArgs = new CommandEventArgs(e, command, null);
                                await OnCommandError(error.Value, errorArgs);
                                return;
                            }
                        }

                        var eventArgs = new CommandEventArgs(e, command, args);

                        // Check permissions
                        if (!command.CanRun(eventArgs.User, eventArgs.Channel, out var errorText))
                        {
                            await OnCommandError(CommandErrorType.BadPermissions, eventArgs, errorText != null ? new Exception(errorText) : null);
                            return;
                        }
                        // Check flags
                        if (!priv && command.NsfwFlag && (_getNsfwFlag != null ? _getNsfwFlag(e.Channel) : false))
                        {
                            await OnCommandError(CommandErrorType.BadPermissions, eventArgs, new NsfwFlagException());
                            return;
                        }
                        if (priv && command.MusicFlag && !(_getMusicFlag != null ? _getMusicFlag(e.Author as IVoiceState) : false))
                        {
                            await OnCommandError(CommandErrorType.BadPermissions, eventArgs, new MusicFlagException());
                            return;
                        }

                        // Run the command
                        try
                        {
                            OnCommand(eventArgs);
                            await command.Run(eventArgs).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            await OnCommandError(CommandErrorType.Exception, eventArgs, ex);
                        }
                        return;
                    }
                    var errorArgs2 = new CommandEventArgs(e, null, null);
                    await OnCommandError(CommandErrorType.BadArgCount, errorArgs2);
                }
            };
        }

        public Task ShowGeneralHelp(IUser user, IMessageChannel channel, IMessageChannel replyChannel = null)
        {
            if (replyChannel == null) replyChannel = channel;
            StringBuilder output = new StringBuilder();
            var tasks = new List<Task>();
            bool isFirstCategory = true;
            foreach (var category in _categories)
            {
                bool isFirstItem = true;
                foreach (var group in category.Value.SubGroups)
                {
                    if (group.IsVisible && (group.HasSubGroups || group.HasNonAliases) && group.CanRun(user, channel, out var error))
                    {
                        if (isFirstItem)
                        {
                            isFirstItem = false;
                            //This is called for the first item in each category. If we never get here, we dont bother writing the header for a category type (since it's empty)
                            if (isFirstCategory)
                            {
                                isFirstCategory = false;
                                //Called for the first non-empty category
                                output.AppendLine("These are the commands you can use:");
                            }
                            else
                                output.AppendLine();
                            if (category.Key != "")
                            {
                                output.Append(Format.Bold(category.Key));
                                output.Append(": ");
                            }
                        }
                        else
                            output.Append(", ");
                        output.Append('`');
                        output.Append(group.Name);
                        if (group.HasSubGroups)
                            output.Append("*");
                        output.Append('`');

                        if (output.Length >= 1900) // Allow 100 characters to avoid going over character limit
                        {
                            tasks.Add(replyChannel.SendMessageAsync(output.ToString()));
                            output.Clear();
                            isFirstItem = true;
                        }
                    }
                }
            }

            if (output.Length == 0)
                output.Append("There are no commands you have permission to run.");
            else
            {
                output.Append("\n\n");

                var chars = Config.CommandChars;
                bool has_chars = chars.Any();
                if (has_chars)
                    output.AppendLine($"You can use `{(chars.Length == 1 ? chars[0].ToString() : $"{string.Join(" ", chars.Take(chars.Length - 1))}` or `{chars.Last()}")}` to call a command.");
                if (Config.MentionCommandChar != 0)
                    output.AppendLine($"You can {(has_chars ? "also " : "")}@mention me before {(Config.MentionCommandChar == 1 ? "" : "or after ")}a command{(has_chars ? ", instead" : "")}.");
                output.AppendLine($"`{(has_chars ? chars[0].ToString() : "")}help <command>` can tell you more about how to use a command.");
            }

            tasks.Add(replyChannel.SendMessageAsync(output.ToString()));
            return Task.WhenAll(tasks);
        }

        private Task ShowCommandHelp(CommandMap map, IUser user, IMessageChannel channel, IMessageChannel replyChannel = null)
        {
            StringBuilder output = new StringBuilder();

            IEnumerable<Command> cmds = map.Commands;
            bool isFirstCmd = true;
            string error;
            if (cmds.Any())
            {
                foreach (var cmd in cmds)
                {
                    if (!cmd.CanRun(user, channel, out error)) { }
                        //output.AppendLine(error ?? DefaultPermissionError);
                    else
                    {
                        if (isFirstCmd)
                            isFirstCmd = false;
                        else
                            output.AppendLine();
                        ShowCommandHelpInternal(cmd, user, channel, output);
                    }
                }
            }
            else
            {
                output.Append('`');
                output.Append(map.FullName);
                output.Append("`\n");
            }

            bool isFirstSubCmd = true;
            foreach (var subCmd in map.SubGroups.Where(x => x.CanRun(user, channel, out error) && x.IsVisible))
            {
                if (isFirstSubCmd)
                {
                    isFirstSubCmd = false;
                    output.Append("**Sub Commands:** ");
                }
                else
                    output.Append(", ");
                output.Append('`');
                output.Append(subCmd.Name);
                if (subCmd.SubGroups.Any())
                    output.Append("*");
                output.Append('`');
            }

            if (isFirstCmd && isFirstSubCmd) //Had no commands and no subcommands
            {
                output.Clear();
                output.AppendLine("There are no commands you have permission to run.");
            }

            return (replyChannel ?? channel).SendMessageAsync(output.ToString());
        }
        public Task ShowCommandHelp(Command command, IUser user, IMessageChannel channel, IMessageChannel replyChannel = null)
        {
            var output = new StringBuilder();
            if (!command.CanRun(user, channel, out var error))
                output.AppendLine(error ?? "You do not have permission to access this command.");
            else
                ShowCommandHelpInternal(command, user, channel, output);
            return (replyChannel ?? channel).SendMessageAsync(output.ToString());
        }
        private void ShowCommandHelpInternal(Command command, IUser user, IMessageChannel channel, StringBuilder output)
        {
            output.Append('`');
            output.Append(command.Text);
            foreach (var param in command.Parameters)
            {
                switch (param.Type)
                {
                    case ParameterType.Required:
                        output.Append($" <{param.Name}>");
                        break;
                    case ParameterType.Optional:
                        output.Append($" [{param.Name}]");
                        break;
                    case ParameterType.Multiple:
                    case ParameterType.MultipleUnparsed:
                        output.Append($" [{param.Name}]");
                        break;
                    case ParameterType.Unparsed:
                        output.Append($" {param.Name}");
                        break;
                }
            }
            output.Append('`');
            output.AppendLine($": {command.Description ?? "No description."}");

            if (command.Aliases.Any())
                output.AppendLine($"**Aliases:** `" + string.Join("`, `", command.Aliases) + '`');

            if (command.NsfwFlag || command.MusicFlag)
            {
                string flags ="**Flags:** ";
                if (command.MusicFlag) flags += "Music ";
                if (command.NsfwFlag) flags += "NSFW ";
                flags = flags.TrimEnd(' ');
                output.AppendLine(flags);
            }
        }

        public void CreateGroup(string cmd, Action<CommandGroupBuilder> config = null) => Root.CreateGroup(cmd, config);
        public CommandBuilder CreateCommand(string cmd) => Root.CreateCommand(cmd);

        internal void AddCommand(Command command)
        {
            _allCommands.Add(command);

            //Get category
            string categoryName = command.Category ?? "";
            if (!_categories.TryGetValue(categoryName, out var category))
            {
                category = new CommandMap();
                _categories.Add(categoryName, category);
            }

            //Add main command
            category.AddCommand(command.Text, command, false);
            _map.AddCommand(command.Text, command, false);

            //Add aliases
            foreach (var alias in command.Aliases)
            {
                category.AddCommand(alias, command, true);
                _map.AddCommand(alias, command, true);
            }
        }
    }
}
