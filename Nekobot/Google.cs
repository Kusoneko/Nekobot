using System;
using System.IO;
using System.Threading;
using Nekobot.Commands;
using Newtonsoft.Json.Linq;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Discord;

namespace Nekobot
{
    class Google
    {
        internal static void AddCommands(CommandGroupBuilder group)
        {
            var secret_file = "calendar_client_secret.json";
            if (File.Exists(secret_file))
            {
                UserCredential credential;
                using (var stream = new FileStream(secret_file, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets, new[]{CalendarService.Scope.CalendarReadonly}, Program.AppName, CancellationToken.None).Result;
                }
                Func<string> timezone = () =>
                {
                    var offset = TimeZoneInfo.Local.BaseUtcOffset.ToString();
                    return $"(UTC{offset.Substring(0, offset.LastIndexOf(':'))})";
                };
                foreach (var calendar_cmd in (JObject)Program.config["GoogleCalendar"])
                {
                    Helpers.CreateJsonCommand(group, calendar_cmd, (cmd, val) =>
                    {
                        var hide_desc = Helpers.FieldExistsSafe<bool>(val, "hideDescription");
                        cmd.Parameter("count or event id", ParameterType.Optional)
                        .Do(async e =>
                        {
                            var service = new CalendarService(new BaseClientService.Initializer()
                            {
                                HttpClientInitializer = credential,
                                ApplicationName = Program.AppName
                            });
                            Func<Event, string> desc = item => item.Start.DateTime == null ? $"All day {(item.Start.Date.Equals(DateTime.Now.ToString("yyyy-MM-dd")) ? "today" : $"on {item.Start.Date}")}" : $"{(DateTime.Now > item.Start.DateTime ? $"Happening until" : $"Will happen {item.Start.DateTime} and end at")} {item.End.DateTime} {timezone()}.{((hide_desc || string.IsNullOrEmpty(item.Description)) ? "" : $"\n{item.Description}")}";
                            int results = 1;
                            if (Helpers.HasArg(e.Args))
                                if (!int.TryParse(e.Args[0], out results)) // Must be an event ID
                                {
                                    var r = await service.Events.Get(val["calendarId"].ToString(), e.Args[0]).ExecuteAsync();
                                    await e.Channel.SendMessageAsync(string.Empty, embed: new EmbedBuilder().WithTitle(r.Summary).WithDescription(desc(r)).Build());
                                    return;
                                }
                            var request = service.Events.List(val["calendarId"].ToString());
                            request.TimeMin = DateTime.Now;
                            request.SingleEvents = true;
                            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                            request.MaxResults = results;
                            var events = await request.ExecuteAsync();
                            if (events.Items?.Count > 0)
                            {
                                var builder = new EmbedBuilder();
                                foreach (var item in events.Items)
                                {
                                    builder.AddField(new EmbedFieldBuilder().WithName(item.Summary).WithValue(desc(item)));
                                    Helpers.SendEmbedEarly(e.Channel, ref builder);
                                }
                                await Helpers.SendEmbed(e.Channel, builder);
                            }
                            else await e.Channel.SendMessageAsync("Apparently, there's nothing coming up nor taking place right now...");
                        });
                    });
                }
            }
        }
    }
}
