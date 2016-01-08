using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace Nekobot.Commands
{
    using Permissions;
    public sealed class Command
    {
        private string[] _aliases;
        internal CommandParameter[] _parameters;
        private IPermissionChecker[] _checks;
        private Func<CommandEventArgs, Task> _runFunc;
        internal readonly Dictionary<string, CommandParameter> _parametersByName;

        public string Text { get; }
        public string Category { get; internal set; }
        public bool IsHidden { get; internal set; }
        public string Description { get; internal set; }
        public bool NsfwFlag { get; internal set; }
        public bool MusicFlag { get; internal set; }

        public IEnumerable<string> Aliases => _aliases;
        public IEnumerable<CommandParameter> Parameters => _parameters;

        internal Command(string text)
        {
            Text = text;
            IsHidden = false;
            _aliases = new string[0];
            _parameters = new CommandParameter[0];
            _parametersByName = new Dictionary<string, CommandParameter>();
        }

        public CommandParameter this[string name] => _parametersByName[name];

        internal void SetAliases(string[] aliases)
        {
            _aliases = aliases;
        }
        internal void SetParameters(CommandParameter[] parameters)
        {
            _parametersByName.Clear();
            for (int i = 0; i < parameters.Length; i++)
            {
                parameters[i].Id = i;
                _parametersByName[parameters[i].Name] = parameters[i];
            }
            _parameters = parameters;
        }
        internal void SetChecks(IPermissionChecker[] checks)
        {
            _checks = checks;
        }

        internal bool CanRun(User user, Channel channel, out string error)
        {
            for (int i = 0; i < _checks.Length; i++)
            {
                if (!_checks[i].CanRun(this, user, channel, out error))
                    return false;
            }
            error = null;
            return true;
        }

        // Copied from Discord.Net/Helpers/TaskHelper.cs
        internal static class TaskHelper
        {
            public static Task CompletedTask { get; }
            static TaskHelper()
            {
#if DOTNET54
                CompletedTask = Task.CompletedTask;
#else
                CompletedTask = Task.Delay(0);
#endif
            }

            public static Func<Task> ToAsync(Action action)
            {
                return () =>
                {
                    action(); return CompletedTask;
                };
            }
            public static Func<T, Task> ToAsync<T>(Action<T> action)
            {
                return x =>
                {
                    action(x); return CompletedTask;
                };
            }
        }

        internal void SetRunFunc(Func<CommandEventArgs, Task> func)
        {
            _runFunc = func;
        }
        internal void SetRunFunc(Action<CommandEventArgs> func)
        {
            _runFunc = TaskHelper.ToAsync(func);
        }
        internal Task Run(CommandEventArgs args)
        {
            var task = _runFunc(args);
            if (task != null)
                return task;
            else
                return TaskHelper.CompletedTask;
        }
    }
}
