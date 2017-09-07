using System;
using Albatross.Expression;
using Ninject;
using RollGen;

namespace Nekobot
{
    static class RPG
    {
        class DiceKernel : StandardKernel
        {
            public DiceKernel Init()
            {
                new RollGen.Domain.IoC.RollGenModuleLoader().LoadModules(this);
                return this;
            }
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
                        if (!int.TryParse(s, out int dummy))
                        {
                            await e.Channel.SendMessageAsync($"{s} is not a number!");
                            return;
                        }
                    }
                    int min = e.Args.Length > 1 ? int.Parse(e.Args[0]) : 1;
                    int max = e.Args.Length > 0 ? int.Parse(e.Args[e.Args.Length == 1 ? 0 : 1]) : 100;
                    if (min == max)
                    {
                        await e.Channel.SendMessageAsync($"You're joking right? It's {min}.");
                        return;
                    }
                    if (min > max)
                    {
                        int z = min;
                        min = max;
                        max = z;
                    }
                    await e.Channel.SendMessageAsync($"Your number is **{new Random().Next(min,max+1)}**.");
                });

            var dk = new DiceKernel().Init();
            var dd = dk.Get<IDice>();
            group.CreateCommand("roll")
                .Parameter("[times]t [dice expressions]", Commands.ParameterType.Unparsed)
                .Description("I'll roll a dice expression([count]d[sides]k[kept][mods...]...) as many `times` as you ask(default 1). (If empty or just `times`, will roll default: 1d6.)")
                .Do(async e =>
                {
                    var chan = e.Channel;
                    if (e.Args[0].ToLower() == "rick")
                    {
                        await chan.SendMessageAsync("https://youtu.be/dQw4w9WgXcQ");
                        return;
                    }
                    var args = string.Join(" ", e.Args);
                    int times;
                    if ((times = args.IndexOf("t")) != -1)
                    {
                        int t = times;
                        if (int.TryParse(args.Substring(0, t), out times))
                        {
                            if (times <= 0)
                            {
                                await chan.SendMessageAsync($"0, baka!");
                                return;
                            }
                            args = args.Substring(t+1);
                        }
                        else times = 1;
                    }
                    else times = 1;

                    string response = "";
                    double? total = times > 1 ? (int?)0 : null;
                    bool do_default = args.Length == 0; // Default roll.
                    for (; times != 0; --times)
                    {
                        double val;
                        if (do_default)
                        {
                            val = dd.Roll().D6().AsSum();
                            response += $"{val} {(total == null ? "" : times == 1 ? "=" : "+")} ";
                        }
                        else
                        {
                            try
                            {
                                var roll = dd.ReplaceRollsWithSumExpression(args);
                                var eval = dk.Get<IParser>().Compile(roll).EvalValue(null);
                                val = Utils.ChangeType<double>(eval);
                                if (response != "") response += '\n';
                                var str = Utils.BooleanOrType<double>(eval);
                                if (roll != str) response += $"{Discord.Format.Code(roll)} = ";
                                response += $"**{str}**.";
                            }
                            catch (Exception ex)
                            {
                                await chan.SendMessageAsync($"Invalid Arguments: {ex.Message}");
                                return;
                            }
                        }
                        if (total != null) total += val;
                    }
                    if (total != null)
                    {
                        if (!do_default) response += "\nTotal Result = ";
                        response += $"**{total}**.";
                    }
                    await chan.SendMessageAsync(response);
                });

            group.CreateCommand("rollsentence")
                .Parameter("[sentence]", Commands.ParameterType.Unparsed)
                .Description("I'll replace all instances of dice expressions wrapped like {1d4} with their resolutions. (see ` help roll` for info on dice expressions)")
                .Do(async e => await e.Channel.SendMessageAsync(e.Args[0].Length == 0 ? "" : dd.ReplaceWrappedExpressions<double>(e.Args[0])));
        }
    }
}
