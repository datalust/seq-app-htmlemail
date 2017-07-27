using System;
using System.Collections.Generic;
using System.IO;
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

        [SeqAppSetting(
            DisplayName = "API key",
            InputType = SettingInputType.Password,
            IsOptional = true,
            HelpText = "The API key to use when writing to the second server, if required.")]
        public string ApiKey { get; set; }

        [SeqAppSetting(
            DisplayName = "Use durable log shipping",
            IsOptional = true,
            HelpText = "If set, logs will be buffered durably (local disk) before forwarding;" +
                       " otherwise only memory buffering is used and message loss may be more common.")]
        public bool IsDurable { get; set; }

        protected override void OnAttached()
        {
            var apiKey = string.IsNullOrWhiteSpace(ApiKey) ? null : ApiKey.Trim();

            var bufferBase = IsDurable ? Path.Combine(App.StoragePath, "Buffer", "replicate") : null;

            _replicaLogger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Seq(
                    ServerUrl,
                    batchPostingLimit: 1000,
                    period: TimeSpan.FromMilliseconds(100),
                    apiKey: apiKey,
                    bufferBaseFilename: bufferBase)
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
            var d = value as IReadOnlyDictionary<string, object>;
            if (d != null)
            {
                object tt;
                var _ = d.TryGetValue("$typeTag", out tt) || d.TryGetValue("_typeTag", out tt) || d.TryGetValue("$type", out tt);
                return new StructureValue(
                    d.Where(kvp => kvp.Key != "$typeTag" && kvp.Key != "_typeTag" && kvp.Key != "$type")
                        .Select(kvp => CreateProperty(kvp.Key, kvp.Value)),
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
