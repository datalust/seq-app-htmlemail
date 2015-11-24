using System;
using System.Collections.Generic;
using Seq.App.EmailPlus.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Xunit;

namespace Seq.App.EmailPlus.Tests
{
    public class EmailReactorTests
    {
        [Fact]
        public void BuiltInPropertiesAreRenderedInTemplates()
        {
            var template = Handlebars.Handlebars.Compile("{{$Level}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal(data.Data.Level.ToString(), result);
        }

        [Fact]
        public void PayloadPropertiesAreRenderedInTemplates()
        {
            var template = Handlebars.Handlebars.Compile("See {{What}}");
            var data = Some.LogEvent(new Dictionary<string, object> { { "What", 10 } });
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("See 10", result);
        }

        [Fact]
        public void NoPropertiesAreRequiredOnASourceEvent()
        {
            var template = Handlebars.Handlebars.Compile("No properties");
            var id = Some.EventId();
            var timestamp = Some.UtcTimestamp();
            var data = new Event<LogEventData>(id, Some.EventType(), timestamp, new LogEventData
            {
                Exception = null,
                Id = id,
                Level = LogEventLevel.Fatal,
                LocalTimestamp = new DateTimeOffset(timestamp),
                MessageTemplate = "Some text",
                RenderedMessage = "Some text",
                Properties = null
            });
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("No properties", result);
        }
    }
}
