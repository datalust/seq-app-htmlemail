using System.IO;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.Email.QuickStache
{
    abstract class TemplateToken
    {
        public abstract void Render(TextWriter output, Event<LogEventData> evt); 
    }
}