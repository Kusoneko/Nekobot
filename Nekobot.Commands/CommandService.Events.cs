using System;
using Discord;

namespace Nekobot.Commands
{
    public class NsfwFlagException : Exception { public NsfwFlagException() : base("This channel doesn't allow nsfw commands.") { } }
    public class MusicFlagException : Exception { public MusicFlagException() : base("You need to be in a music streaming channel to use this command.") { } }
    public class CommandEventArgs
    {
        private readonly string[] _args;

        public Message Message { get; }
        public Command Command { get; }

        public User User => Message.User;
        public Channel Channel => Message.Channel;
        public Server Server => Message.Channel.Server;

        public CommandEventArgs(Message message, Command command, string[] args)
        {
            Message = message;
            Command = command;
            _args = args;
        }

        public string[] Args => _args;
        /*We only add strings of use to us, no empty Optionals, so these can't be used
        public string GetArg(int index) => _args[index];
        public string GetArg(string name) => _args[Command[name].Id];*/
    }

    public enum CommandErrorType { Exception, UnknownCommand, BadPermissions, BadArgCount, InvalidInput }
    public class CommandErrorEventArgs : CommandEventArgs
    {
        public CommandErrorType ErrorType { get; }
        public Exception Exception { get; }

        public CommandErrorEventArgs(CommandErrorType errorType, CommandEventArgs baseArgs, Exception ex)
            : base(baseArgs.Message, baseArgs.Command, baseArgs.Args)
        {
            Exception = ex;
            ErrorType = errorType;
        }
    }

    public partial class CommandService
    {
        public event EventHandler<CommandEventArgs> RanCommand;
        private void RaiseRanCommand(CommandEventArgs args)
        {
            if (RanCommand != null)
                RanCommand(this, args);
        }
        public event EventHandler<CommandErrorEventArgs> CommandError;
        private void RaiseCommandError(CommandErrorType errorType, CommandEventArgs args, Exception ex = null)
        {
            if (CommandError != null)
                CommandError(this, new CommandErrorEventArgs(errorType, args, ex));
        }
    }
}
