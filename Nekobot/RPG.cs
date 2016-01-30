using System;
using System.Linq;

namespace Nekobot
{
    static class RPG
    {
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
                .Parameter("dice", Commands.ParameterType.Optional)
                .Parameter("sides", Commands.ParameterType.Optional)
                .Parameter("times", Commands.ParameterType.Optional)
                .Description("I'll roll a few sided dice for a given number of times. All params are optional. (defaults: 1 *dice*, 6 *sides*, 1 *times*)")
                .Do(async e =>
                {
                    if (e.Args.Length != 0 && e.Args[0] == "rick")
                    {
                        await e.Channel.SendMessage("https://youtu.be/dQw4w9WgXcQ");
                        return;
                    }

                    try
                    {
                        int dice = e.Args.Length >= 1 ? int.Parse(e.Args[0]): 1;
                        int sides = e.Args.Length >= 2 ? int.Parse(e.Args[1]): 6;
                        int times = e.Args.Length >= 3 ? int.Parse(e.Args[2]): 1;

                        string response = (dice <= 0 || sides <= 1 || times <= 0) ? $"{sides*times*dice}, baka!" : "";
                        if (!response.Any())
                        {
                            var total = 0;
                            Random rnd = new Random();
                            for (int i = times; i > 0; i--)
                            {
                                var subtotal = 0;
                                for (int j = dice; j > 0; j--)
                                {
                                    var roll = rnd.Next(1, sides + 1);
                                    if (dice > 1) response += $"{roll}{(j == 1 ? "" : ", ")}";
                                    subtotal += roll;
                                }
                                if (times > 1) response += dice > 1 ? $" = {subtotal}.\n" : $"{subtotal}{(i == 1 ? "" : ", ")}";
                                total += subtotal;
                            }
                            response += $"{(times == 1 || dice == 1 ? "" : "Total Result")} = {total}.";
                        }
                        await e.Channel.SendMessage(response);
                    } catch { await e.Channel.SendMessage("Arguments are not all numbers!"); }
                });
        }
    }
}
