using System;
using Discord;

namespace Nekobot.Commands
{
    public class CommandEventArgs : EventArgs
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
}
