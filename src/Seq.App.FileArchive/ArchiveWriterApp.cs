using System;
using Seq.Apps;
using Serilog;
using Serilog.Events;
using Serilog.Core;

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
