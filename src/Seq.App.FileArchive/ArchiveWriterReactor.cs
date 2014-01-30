using System;
using System.Collections.Generic;
using System.Linq;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using System.Collections;

namespace Seq.App.FileArchive
{
    [SeqApp(
        "File Archive",
        Description = "Writes events to a set of rolling log files for long-term archival storage.")]
    public class ArchiveWriterReactor : Reactor, ISubscribeTo<LogEventData>
    {
        ILogger _archiveLogger;

        [SeqAppSetting(
            DisplayName = "Path format",
            HelpText = "The location of the log files, with {Date} in the place of the file date. E.g. " +
                       "'C:\\Logs\\myapp-{Date}.log' will result in log files such as 'myapp-2013-10-20.log'," +
                       " 'myapp-2013-10-21.log' and so on.")]
        public string PathFormat { get; set; }

        protected override void OnAttached()
        {
            _archiveLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.RollingFile(
                    PathFormat,
                    retainedFileCountLimit: null,
                    outputTemplate: "{Timestamp} [{Level}] {Message:l}{NewLine:l}{ArchiveWriterException:l}")
                .CreateLogger();
        }

        public void On(Event<LogEventData> evt)
        {
            var mtp = new MessageTemplateParser();

            var properties = (evt.Data.Properties ?? new Dictionary<string, object>())
                .Concat(new[]{ new KeyValuePair<string,object>("ArchiveWriterException", evt.Data.Exception ?? "")})
                .Select(kvp => CreateProperty(kvp.Key, kvp.Value));

            var sle = new LogEvent(
                evt.Data.LocalTimestamp,
                (Serilog.Events.LogEventLevel)Enum.Parse(typeof(Serilog.Events.LogEventLevel), evt.Data.Level.ToString()),
                null,
                mtp.Parse(evt.Data.MessageTemplate),
                properties);

            _archiveLogger.Write(sle);
        }

        LogEventProperty CreateProperty(string name, object value)
        {
            return new LogEventProperty(name, CreatePropertyValue(value));
        }


        LogEventPropertyValue CreatePropertyValue(object value)
        {
            var d = value as IDictionary<string,object>;
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
