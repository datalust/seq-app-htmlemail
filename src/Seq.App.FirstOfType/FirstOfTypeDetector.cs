using System.IO;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.FirstOfType
{
    [SeqApp(
        "First of Type",
        Description = "Emits an event whenever the first event of a new type is seen.")]
    public class FirstOfTypeDetector : SeqApp, ISubscribeTo<LogEventData>
    {
        const string StateFilename = "DetectedEventTypes.bin";
        const string StateWriteFilename = "DetectedEventTypes-new.bin";
        const string StateBackupFilename = "DetectedEventTypes-old.bin";

        UInt32BloomFilter _filter;

        public void On(Event<LogEventData> evt)
        {

            if (_filter == null)
            {
                var stateFile = Path.Combine(App.StoragePath, StateFilename);
                if (File.Exists(stateFile))
                    _filter = new UInt32BloomFilter(File.ReadAllBytes(stateFile));
                else
                    _filter = new UInt32BloomFilter();
            }

            if (!_filter.MayContain(evt.EventType))
            {
                Log.Information("First of {DetectedEventType}: {DetectedEventMessage} ({DetectedEventId})",
                    "$" + evt.EventType.ToString("X8"),
                    evt.Data.RenderedMessage,
                    evt.Id);

                _filter.Add(evt.EventType);

                var stateFile = Path.Combine(App.StoragePath, StateFilename);
                var writeFile = Path.Combine(App.StoragePath, StateWriteFilename);
                var backupFile = Path.Combine(App.StoragePath, StateBackupFilename);

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
