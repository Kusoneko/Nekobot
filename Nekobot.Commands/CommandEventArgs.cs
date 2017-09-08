using System;
using Discord;

namespace Nekobot.Commands
{
    public class CommandEventArgs : EventArgs
    {
        private readonly string[] _args;

        public IMessage Message { get; }
        public Command Command { get; }

        public IUser User => Message.Author;
        // Standard channel for messages
        public IMessageChannel Channel => Message.Channel;
        // Channel in a Guild where messages can be sent
        public ITextChannel TextChannel => Channel as ITextChannel;
        // Voice Channel in a Guild
        public IVoiceChannel VoiceChannel => (User as IVoiceState).VoiceChannel;
        public IGuild Server => (Message.Channel is IGuildChannel) ? (Message.Channel as IGuildChannel).Guild : null;

        public CommandEventArgs(IMessage message, Command command, string[] args)
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
