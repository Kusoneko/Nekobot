﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

// Change this namespace if desired
namespace Nekobot
{
    // This customized service restarts nekobot instead of returning
    // Exit Code 1 (or any exit code) for a handler to restart.
    public class ReliabilityService
    {
        // --- Begin Configuration Section ---
        // How long should we wait on the client to reconnect before resetting?
        private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);

        // Should we attempt to reset the client? Set this to false if your client is still locking up.
        private static readonly bool _attemptReset = true;

        // Change log levels if desired:
        private static readonly LogSeverity _debug = LogSeverity.Debug;
        private static readonly LogSeverity _info = LogSeverity.Info;
        private static readonly LogSeverity _critical = LogSeverity.Critical;
        // --- End Configuration Section ---

        private readonly DiscordSocketClient _discord;
        private readonly Func<LogMessage, Task> _logger;
        private CancellationTokenSource _cts;

        public ReliabilityService(DiscordSocketClient discord, Func<LogMessage, Task> logger = null)
        {
            _cts = new CancellationTokenSource();
            _discord = discord;
            _logger = logger ?? (_ => Task.CompletedTask);

            _discord.Connected += ConnectedAsync;
            _discord.Disconnected += DisconnectedAsync;
        }

        public Task ConnectedAsync()
        {
            // Cancel all previous state checks and reset the CancelToken - client is back online
            _ = DebugAsync("Client reconnected, resetting cancel tokens...");
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            _ = DebugAsync("Client reconnected, cancel tokens reset.");

            return Task.CompletedTask;
        }

        public Task DisconnectedAsync(Exception _e)
        {
            // Check the state after <timeout> to see if we reconnected
            _ = InfoAsync("Client disconnected, starting timeout task...");
            _ = Task.Delay(_timeout, _cts.Token).ContinueWith(async _ =>
            {
                await DebugAsync("Timeout expired, continuing to check client state...");
                await CheckStateAsync();
                await DebugAsync("State came back okay");
            });

            return Task.CompletedTask;
        }

        private async Task CheckStateAsync()
        {
            // Client reconnected, no need to reset
            if (_discord.ConnectionState == ConnectionState.Connected) return;
            if (_attemptReset)
            {
                await InfoAsync("Attempting to reset the client");

                var timeout = Task.Delay(_timeout);
                var connect = _discord.StartAsync();
                var task = await Task.WhenAny(timeout, connect);

                if (task == timeout)
                {
                    await CriticalAsync("Client reset timed out (task deadlocked?), killing process");
                    FailFast();
                }
                else if (connect.IsFaulted)
                {
                    await CriticalAsync("Client reset faulted, killing process", connect.Exception);
                    FailFast();
                }
                else if (connect.Status == TaskStatus.RanToCompletion)
                    await InfoAsync("Client reset succesfully!");
            }

            await CriticalAsync("Client did not reconnect in time, killing process");
            FailFast();
        }

        private void FailFast()
            => Helpers.Restart();

        // Logging Helpers
        private const string LogSource = "Reliability";
        private Task DebugAsync(string message)
            => _logger.Invoke(new LogMessage(_debug, LogSource, message));
        private Task InfoAsync(string message)
            => _logger.Invoke(new LogMessage(_info, LogSource, message));
        private Task CriticalAsync(string message, Exception error = null)
            => _logger.Invoke(new LogMessage(_critical, LogSource, message, error));
    }
}