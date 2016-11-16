using System;
using System.IO;
using System.Threading;
using Nekobot.Commands;
using Newtonsoft.Json.Linq;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;

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
                        GoogleClientSecrets.Load(stream).Secrets, new[]{CalendarService.Scope.CalendarReadonly}, Program.Config.AppName, CancellationToken.None).Result;
                }
                foreach (var calendar_cmd in (JObject)Program.config["GoogleCalendar"])
                {
                    Helpers.CreateJsonCommand(group, calendar_cmd, (cmd, val) =>
                    {
                        var include_desc = Helpers.FieldExistsSafe<bool>(val, "includeDescription");
                        cmd.Parameter("count or event id", ParameterType.Optional)
                        .Do(async e =>
                        {
                            var service = new CalendarService(new BaseClientService.Initializer()
                            {
                                HttpClientInitializer = credential,
                                ApplicationName = Program.Config.AppName
                            });
                            Func<Event, string> header = item =>
                              $"**{item.Summary}:**\n{(item.Start.DateTime == null ? $"All day {(item.Start.Date.Equals(DateTime.Now.ToString("yyyy-MM-dd")) ? "today" : $"on {item.Start.Date}")}" : $"{(DateTime.Now > item.Start.DateTime ? $"Happening until" : $"Will happen {item.Start.DateTime} and end at")} {item.End.DateTime}")}.\n";
                            Func<Event, string> header_desc = item => $"{header(item)}{item.Description}";
                            int results = 1;
                            if (Helpers.HasArg(e.Args))
                                if (!int.TryParse(e.Args[0], out results)) // Must be an event ID
                                {
                                    var r = await service.Events.Get(val["calendarId"].ToString(), e.Args[0]).ExecuteAsync();
                                    await e.Channel.SendMessage(header_desc(r));
                                    return;
                                }
                            var request = service.Events.List(val["calendarId"].ToString());
                            request.TimeMin = DateTime.Now;
                            request.SingleEvents = true;
                            request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
                            request.MaxResults = results;
                            var events = await request.ExecuteAsync();
                            if (events.Items?.Count > 0)
                                foreach (var item in events.Items)
                                    await e.Channel.SendMessage(include_desc ? header_desc(item) : $"{header(item)}ID: {item.Id}");
                            else await e.Channel.SendMessage("Apparently, there's nothing coming up nor taking place right now...");
                        });
                    });
                }
            }
        }
    }
}
