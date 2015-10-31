using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nekobot.Commands
{
    public sealed class Command
    {
        public string Text { get; }
        public string Description { get; internal set; }
        public string Syntax { get; internal set; }
        public int? MinArgs { get; internal set; }
        public int? MaxArgs { get; internal set; }
        public int MinPerms { get; internal set; }
        public bool NsfwFlag { get; internal set; }
        public bool MusicFlag { get; internal set; }
        internal readonly string[] Parts;
        internal Func<CommandEventArgs, Task> Handler;

        internal Command(string text)
        {
            Text = text;
            Parts = text.ToLowerInvariant().Split(' ');
        }
    }
}
