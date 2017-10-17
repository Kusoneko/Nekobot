using System;
using Discord;

namespace Nekobot.Commands
{
    public class CommandServiceConfig
    {
        public char? CommandChar
        {
            get
            {
                return _commandChars.Length > 0 ? _commandChars[0] : (char?)null;
            }
            set
            {
                CommandChars = value != null ? new char[] { value.Value } : new char[0];
            }
        }
        public char[] CommandChars { get { return _commandChars; } set { SetValue(ref _commandChars, value); } }
        private char[] _commandChars = new char[] { '!' };

        public bool RequireCommandCharInPrivate, RequireCommandCharInPublic;
        public short MentionCommandChar;

        public HelpMode HelpMode { get { return _helpMode; } set { SetValue(ref _helpMode, value); } }

        public Color EmbedColor { get; set; }

        private HelpMode _helpMode = HelpMode.Disabled;

        //Lock
        protected bool _isLocked;
        internal void Lock() { _isLocked = true; }
        protected void SetValue<T>(ref T storage, T value)
        {
            if (_isLocked)
                throw new InvalidOperationException("Unable to modify a service's configuration after it has been created.");
            storage = value;
        }
    }
}
