using System;
using System.Collections.Generic;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable MemberCanBePrivate.Global

namespace Seq.App.Thresholds.Tests.Support
{
    public static class Some
    {
        public static string String()
        {
            return Guid.NewGuid().ToString();
        }

        public static uint Uint()
        {
            return 5417u;
        }

        public static uint EventType()
        {
            return Uint();
        }

        public static Event<LogEventData> LogEvent(IDictionary<string, object> includedProperties = null, DateTime timestamp = default)
        {
            var id = EventId();

            if (timestamp == default)
            {
                timestamp = UtcTimestamp();
            }

            var properties = new Dictionary<string, object>
            {
                {"Who", "world"},
                {"Number", 42}
            };

            if (includedProperties != null)
            {
                foreach (var (key, value) in includedProperties)
                {
                    properties.Add(key, value);
                }
            }

            return new Event<LogEventData>(id, EventType(), timestamp, new LogEventData
            {
                Exception = null,
                Id = id,
                Level = LogEventLevel.Fatal,
                LocalTimestamp = new DateTimeOffset(timestamp),
                MessageTemplate = "Hello, {Who}",
                RenderedMessage = "Hello, world",
                Properties = properties
            });
        }

        public static string EventId()
        {
            return "event-" + String();
        }

        public static DateTime UtcTimestamp()
        {
            return DateTime.UtcNow;
        }
    }
}