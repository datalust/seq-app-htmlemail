using System;
using System.IO;
using Seq.Apps;
using Serilog;
using Serilog.Events;
using Serilog.Core;

// ReSharper disable UnusedAutoPropertyAccessor.Global, UnusedType.Global, MemberCanBePrivate.Global

namespace Seq.App.Replication
{
    [SeqApp(
        "Replicator",
        Description = "Forwards events to a second Seq server instance.")]
    public class Replicator : SeqApp, ISubscribeTo<LogEvent>, IDisposable
    {
        Logger _replicaLogger;

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
            _replicaLogger.Dispose();
        }

        public void On(Event<LogEvent> evt)
        {
            evt.Data.RemovePropertyIfPresent("@i");
            evt.Data.RemovePropertyIfPresent("@seqid");
            _replicaLogger.Write(evt.Data);
        }
    }
}
