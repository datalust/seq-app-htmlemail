using System.IO;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.FirstOfType
{
    [SeqApp(
        "First of Type",
        Description = "Emits an event whenever the first event of a new type is seen. Currently only suitable when the event stream is Serilog-based.")]
    public class FirstOfTypeDetector : Reactor, ISubscribeTo<LogEventData>
    {
        const string StateFilename = "DetectedEventTypes.bin";
        const string StateWriteFilename = "DetectedEventTypes-new.bin";
        const string StateBackupFilename = "DetectedEventTypes-old.bin";

        UInt32BloomFilter _filter;

        public void On(Event<LogEventData> evt)
        {

            if (_filter == null)
            {
                var stateFile = Path.Combine(StoragePath, StateFilename);
                if (File.Exists(stateFile))
                    _filter = new UInt32BloomFilter(File.ReadAllBytes(StateFilename));
                else
                    _filter = new UInt32BloomFilter();
            }

            if (!_filter.MayContain(evt.EventType))
            {
                Log.Information("First of {DetectedEventType}: {DetectedEventMessage} ({DetectedEventId})",
                    evt.EventType,
                    evt.Data.RenderedMessage,
                    evt.Id);

                _filter.Add(evt.EventType);

                var stateFile = Path.Combine(StoragePath, StateFilename);
                var writeFile = Path.Combine(StoragePath, StateWriteFilename);
                var backupFile = Path.Combine(StoragePath, StateBackupFilename);

                if (File.Exists(backupFile))
                    File.Delete(backupFile);

                File.WriteAllBytes(writeFile, _filter.Bytes);

                if (File.Exists(stateFile))
                    File.Replace(writeFile, stateFile, backupFile);
                else
                    File.Move(writeFile, stateFile);
            }
        }
    }
}
