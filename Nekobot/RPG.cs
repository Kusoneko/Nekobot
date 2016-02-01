using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;

namespace Nekobot
{
    static class RPG
    {
        static int[] RollValues(GroupCollection groups = null)
        {
            int times = groups != null && groups[1].Length != 0 ? int.Parse(groups[1].Value) : 1;
            int count = groups != null && groups[2].Length != 0 ? int.Parse(groups[2].Value) : 1;
            int sides = groups != null && groups[3].Length != 0 ? int.Parse(groups[3].Value) : 6;
            return new[]{ times, count, sides };
        }
        static async Task DoRoll(Channel channel, int[] t)
        {
            int times = t[0], count = t[1], sides = t[2];
            string response = (count <= 0 || sides <= 1 || times <= 0) ? $"{sides*times*count}, baka!" : "";
            if (!response.Any())
            {
                var total = 0;
                Random rnd = new Random();
                for (int i = times; i > 0; i--)
                {
                    var subtotal = 0;
                    for (int j = count; j > 0; j--)
                    {
                        var roll = rnd.Next(1, sides + 1);
                        if (count > 1) response += $"{roll}{(j == 1 ? "" : ", ")}";
                        subtotal += roll;
                    }

                    if (times > 1) response += count > 1 ? $" = {subtotal}.\n" : $"{subtotal}{(i == 1 ? "" : ", ")}";
                    total += subtotal;
                }
                response += $"{(times == 1 || count == 1 ? "" : "Total Result")} = {total}.";
            }
            await channel.SendMessage(response);
        }

        internal static void AddCommands(Commands.CommandGroupBuilder group)
        {
            group.CreateCommand("rand")
                .Parameter("min", Commands.ParameterType.Optional)
                .Parameter("max", Commands.ParameterType.Optional)
                .Description("I'll give you a random number between *min* and *max*. Both are optional. If only one is given, it's *max*. (defaults: 1-100)")
                .Do(async e =>
                {
                    foreach (string s in e.Args)
                    {
                        int dummy = 0;
                        if (!int.TryParse(s, out dummy))
                        {
                            await e.Channel.SendMessage($"{s} is not a number!");
                            return;
                        }
                    }
                    int min = e.Args.Length > 1 ? int.Parse(e.Args[0]) : 1;
                    int max = e.Args.Length > 0 ? int.Parse(e.Args[e.Args.Length == 1 ? 0 : 1]) : 100;
                    if (min == max)
                    {
                        await e.Channel.SendMessage($"You're joking right? It's {min}.");
                        return;
                    }
                    if (min > max)
                    {
                        int z = min;
                        min = max;
                        max = z;
                    }
                    await e.Channel.SendMessage($"Your number is **{new Random().Next(min,max+1)}**.");
                });

            group.CreateCommand("roll")
                .Parameter("[times] [count]d[sides]...", Commands.ParameterType.Multiple)
                .Description("I'll roll `count` `sides` sided dice and add each `mod` to the result `times`. All params are optional. (defaults: 1 *dice*, 6 *sides*, 1 *times*)\nyou can batch roll with different params if you repeat the arguments.")
                .Do(async e =>
                {
                    var chan = e.Channel;
                    if (e.Args.Length == 0) // Default roll.
                    {
                        await DoRoll(chan, RollValues());
                        return;
                    }
                    if (e.Args[0].ToLower() == "rick")
                    {
                        await chan.SendMessage("https://youtu.be/dQw4w9WgXcQ");
                        return;
                    }

                    MatchCollection m = !e.Args.Any() ? null : Regex.Matches(string.Join(" ", e.Args), "(?:([0-9]*) ){0,1}([0-9]*){0,1}d([0-9]*)");
                    if (m.Count == 0)
                        await chan.SendMessage("Incorrect Argument Syntax!");
                    else
                    {
                        foreach (var groups in from Match match in m select match.Groups)
                            if (groups[0].Length != 0)
                                await DoRoll(chan, RollValues(groups));
                    }
                });
        }
    }
}
