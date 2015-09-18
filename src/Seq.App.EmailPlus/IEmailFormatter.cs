using System.Collections.Generic;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EmailPlus
{
    public interface IEmailFormatter
    {
        string FormatSubject(ICollection<Event<LogEventData>> events);
        string FormatBody(ICollection<Event<LogEventData>> events);
    }
}