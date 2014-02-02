using System;
using System.Collections.Generic;
using System.Linq;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using System.Collections;

namespace Seq.App.Replication
{
    [SeqApp(
        "Replicator",
        Description = "Forwards events to a second Seq server instance.")]
    public class Replicator : Reactor, ISubscribeTo<LogEventData>, IDisposable
    {
        ILogger _replicaLogger;

        [SeqAppSetting(
            DisplayName = "Server URL",
            HelpText = "The URL of the second Seq server instance, e.g. http://backup-seq.")]
        public string ServerUrl { get; set; }

        protected override void OnAttached()
        {
            _replicaLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Seq(ServerUrl, batchPostingLimit: 1000, period: TimeSpan.FromMilliseconds(100))
                .CreateLogger();
        }

        public void Dispose()
        {
            var disp = _replicaLogger as IDisposable;
            if (disp != null)
                disp.Dispose();
        }

        public void On(Event<LogEventData> evt)
        {
            var mtp = new MessageTemplateParser();

            var properties = (evt.Data.Properties ?? new Dictionary<string, object>())
                .Select(kvp => CreateProperty(kvp.Key, kvp.Value));

            var sle = new LogEvent(
                evt.Data.LocalTimestamp,
                (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), evt.Data.Level.ToString()),
                evt.Data.Exception != null ? new ReplicatedException(evt.Data.Exception) : null,
                mtp.Parse(evt.Data.MessageTemplate),
                properties);

            _replicaLogger.Write(sle);
        }

        LogEventProperty CreateProperty(string name, object value)
        {
            return new LogEventProperty(name, CreatePropertyValue(value));
        }


        LogEventPropertyValue CreatePropertyValue(object value)
        {
            var d = value as IDictionary<string, object>;
            if (d != null)
            {
                object tt;
                d.TryGetValue("$typeTag", out tt);
                return new StructureValue(
                    d.Where(kvp => kvp.Key != "$typeTag").Select(kvp => CreateProperty(kvp.Key, kvp.Value)),
                    tt as string);
            }

            var dd = value as IDictionary;
            if (dd != null)
            {
                return new DictionaryValue(dd.Keys
                    .Cast<object>()
                    .Select(k => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                        (ScalarValue)CreatePropertyValue(k),
                        CreatePropertyValue(dd[k]))));
            }

            if (value == null || value is string || !(value is IEnumerable))
            {
                return new ScalarValue(value);
            }

            var enumerable = (IEnumerable)value;
            return new SequenceValue(enumerable.Cast<object>().Select(CreatePropertyValue));
        }
    }
}
