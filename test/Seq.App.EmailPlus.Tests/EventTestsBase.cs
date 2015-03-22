using System;
using System.Collections.Generic;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EmailPlus.Tests
{
    public class EventTestsBase
    {
        protected static Event<LogEventData> GetLogEvent(int id = 0, LogEventLevel level = LogEventLevel.Information)
        {
            var logEventData = new LogEventData
            {
                Id = id.ToString(),
                Level = level,
                LocalTimestamp = DateTimeOffset.Now,
                RenderedMessage = "Test",
                Properties = new Dictionary<string, object> { { "Category", "Security" } }
            };
            return new Event<LogEventData>(logEventData.Id, 1, DateTime.UtcNow, logEventData);
        } 
    }
}