using System;

namespace Nekobot.Commands
{
    public class NsfwFlagException : Exception { public NsfwFlagException() : base("This channel doesn't allow nsfw commands.") { } }
    public class MusicFlagException : Exception { public MusicFlagException() : base("You need to be in a music streaming channel to use this command.") { } }

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
}
