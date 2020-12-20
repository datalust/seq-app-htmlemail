using System;
using Seq.Apps;
using Serilog;
using Serilog.Events;
using Serilog.Core;
using Serilog.Formatting.Compact;

// ReSharper disable MemberCanBePrivate.Global

namespace Seq.App.FileArchive
{
    [SeqApp(
        "File Archive",
        Description = "Writes events to a set of rolling log files for long-term archival storage.")]
    public class ArchiveWriterApp : SeqApp, ISubscribeTo<LogEvent>, IDisposable
    {
        Logger _archiveLogger;

        [SeqAppSetting(
            DisplayName = "Path",
            HelpText = "The location of the log files.")]
        public string Path { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Output template",
            HelpText = "The output template to use. The default value is \"{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}\"")]
        public string OutputTemplate { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Use compact JSON formatter",
            HelpText = "If checked, the compact JSON formatter will be used instead of the \"output template\".")]
        public bool? UseCompactJsonFormatter { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Rolling interval",
            HelpText = "The interval at which logging will roll over to a new file. Use one of the following values: Hour, Minute, Day, Month, Year or Infinite. The default is Infinite.")]
        public string RollingInterval { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "File size limit (in bytes)",
            HelpText = "The approximate maximum size, in bytes, to which a log file will be allowed to grow. The default is 1 GB. To avoid writing partial events, the last event within the limit will be written in full even if it exceeds the limit.")]
        public int? FileSizeLimitBytes { get; set; }

        [SeqAppSetting(
            DisplayName = "Roll on file size limit",
            HelpText = "If checked, a new file will be created when the file size limit is reached. Filenames will have a number appended in the format _NNN, with the first filename given no number.")]
        public bool? RollOnFileSizeLimit { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Retained file count limit",
            HelpText = "The maximum number of log files that will be retained, including the current log file. For unlimited retention leave this field empty.")]
        public int? RetainedFileCountLimit { get; set; } = 31;

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Buffered",
            HelpText = "If checked, flushing to the output file can be buffered.")]
        public bool? Buffered { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Shared",
            HelpText = "If checked, allows the log file to be shared by multiple processes.")]
        public bool? Shared { get; set; }

        [SeqAppSetting(
            IsOptional = true,
            DisplayName = "Flush to disk interval (in milliseconds)",
            HelpText = "If provided, a full disk flush will be performed periodically at the specified interval.")]
        public int? FlushToDiskInterval { get; set; }

        protected override void OnAttached()
        {
            // Initialize logger configuration values.
            var outputTemplate = string.IsNullOrWhiteSpace(OutputTemplate) ? "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}" : OutputTemplate;

            var rollingInterval = default(RollingInterval);

            if (!Enum.TryParse<RollingInterval>(RollingInterval, out rollingInterval))
            {
                rollingInterval = Serilog.RollingInterval.Infinite;
            }

            var fileSizeLimitBytes = FileSizeLimitBytes.HasValue && FileSizeLimitBytes.Value > 0 ? FileSizeLimitBytes : 1073741824;
            var rollOnFileSizeLimit = RollOnFileSizeLimit.HasValue && RollOnFileSizeLimit.Value;
            var retainedFileCountLimit = RetainedFileCountLimit.HasValue && RetainedFileCountLimit.Value > 0 ? RetainedFileCountLimit : null;
            var buffered = Buffered.HasValue && Buffered.Value;
            var shared = Shared.HasValue && Shared.Value;
            var flushToDiskInterval = FlushToDiskInterval.HasValue && FlushToDiskInterval.Value > 0 ? TimeSpan.FromMilliseconds(FlushToDiskInterval.Value) : (TimeSpan?)null;

            // Initialize logger configuration.
            var archiveLoggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Verbose();

            // If compact JSON formatter needs to be used, then...
            if (this.UseCompactJsonFormatter.HasValue && this.UseCompactJsonFormatter.Value)
            {
                // Logger with compact JSON formatter.
                archiveLoggerConfiguration.WriteTo.File(
                    new CompactJsonFormatter(),
                    Path,
                    rollingInterval: rollingInterval,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    rollOnFileSizeLimit: rollOnFileSizeLimit,
                    retainedFileCountLimit: retainedFileCountLimit,
                    buffered: buffered,
                    shared: shared,
                    flushToDiskInterval: flushToDiskInterval);
            }
            else
            {
                // Logger with app setting output template.
                archiveLoggerConfiguration.WriteTo.File(
                    Path,
                    outputTemplate: outputTemplate,
                    rollingInterval: rollingInterval,
                    rollOnFileSizeLimit: rollOnFileSizeLimit,
                    fileSizeLimitBytes: fileSizeLimitBytes,
                    retainedFileCountLimit: retainedFileCountLimit,
                    buffered: buffered,
                    shared: shared,
                    flushToDiskInterval: flushToDiskInterval);
            }

            // Create archive logger.
            _archiveLogger = archiveLoggerConfiguration.CreateLogger();
        }

        public void Dispose()
        {
            _archiveLogger.Dispose();
        }

        public void On(Event<LogEvent> evt)
        {
            _archiveLogger.Write(evt.Data);
        }
    }
}
