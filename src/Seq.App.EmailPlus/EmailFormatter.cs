using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Handlebars;
using Newtonsoft.Json;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EmailPlus
{
    public class EmailFormatter : IEmailFormatter
    {
        private const string DefaultSubjectTemplate =
            @"[{{$Events.[0].$Level}}] {{{$Events.[0].$Message}}} (via Seq){{#if $MultipleEvents}} ({{$EventCount}}){{/if}}";

        private readonly Func<object, string> _bodyTemplate;
        private readonly string _instanceName;
        private readonly int? _maxSubjectLength;
        private readonly string _serverUri;
        private readonly Func<object, string> _subjectTemplate;

        public EmailFormatter(string instanceName, string serverUri, string bodyTemplate = null, string subjectTemplate = null, int? maxSubjectLength = null)
        {
            _instanceName = instanceName;
            _serverUri = serverUri;
            _maxSubjectLength = maxSubjectLength;
            Handlebars.Handlebars.RegisterHelper("pretty", PrettyPrint);

            _subjectTemplate = Handlebars.Handlebars.Compile(string.IsNullOrWhiteSpace(subjectTemplate) ? DefaultSubjectTemplate : subjectTemplate);
            _bodyTemplate = Handlebars.Handlebars.Compile(string.IsNullOrWhiteSpace(bodyTemplate) ? Resources.DefaultBodyTemplate : bodyTemplate);
        }

        public string FormatSubject(ICollection<Event<LogEventData>> events)
        {
            var subject = FormatTemplate(_subjectTemplate, events).Trim().Replace("\r", "").Replace("\n", "");
            return _maxSubjectLength.HasValue && subject.Length > _maxSubjectLength.Value ? subject.Substring(0, _maxSubjectLength.Value) : subject;
        }

        public string FormatBody(ICollection<Event<LogEventData>> events)
        {
            return FormatTemplate(_bodyTemplate, events);
        }

        private string FormatTemplate(Func<object, string> template, ICollection<Event<LogEventData>> events)
        {
            var payload = ToDynamic(new Dictionary<string, object>
            {
                {"$Instance", _instanceName},
                {"$ServerUri", _serverUri},
                {"$MultipleEvents", events.Count > 1},
                {"$EventCount", events.Count},
                {
                    "$Events", events.Select(evt => new Dictionary<string, object>
                    {
                        {"$Id", evt.Id},
                        {"$UtcTimestamp", evt.TimestampUtc},
                        {"$LocalTimestamp", evt.Data.LocalTimestamp},
                        {"$Level", evt.Data.Level},
                        {"$MessageTemplate", evt.Data.MessageTemplate},
                        {"$Message", evt.Data.RenderedMessage},
                        {"$Exception", evt.Data.Exception},
                        {"$Properties", ToDynamic(evt.Data.Properties)},
                        {"$EventType", "$" + evt.EventType.ToString("X8")}
                    })
                }
            });

            return template(payload);
        }

        private static void PrettyPrint(TextWriter output, object context, object[] arguments)
        {
            var value = arguments.FirstOrDefault();
            if (value == null)
                output.WriteSafeString("null");
            else if (value is IEnumerable<object> || value is IEnumerable<KeyValuePair<string, object>>)
                output.WriteSafeString(JsonConvert.SerializeObject(FromDynamic(value)));
            else
                output.WriteSafeString(value.ToString());
        }

        private static object FromDynamic(object o)
        {
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                return dictionary.ToDictionary(kvp => kvp.Key, kvp => FromDynamic(kvp.Value));
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(FromDynamic).ToArray();
            }

            return o;
        }

        private static object ToDynamic(object o)
        {
            var dictionary = o as IEnumerable<KeyValuePair<string, object>>;
            if (dictionary != null)
            {
                var result = new ExpandoObject();
                var asDict = (IDictionary<string, object>) result;
                foreach (var kvp in dictionary)
                    asDict.Add(kvp.Key, ToDynamic(kvp.Value));
                return result;
            }

            var enumerable = o as IEnumerable<object>;
            if (enumerable != null)
            {
                return enumerable.Select(ToDynamic).ToArray();
            }

            return o;
        }
    }
}