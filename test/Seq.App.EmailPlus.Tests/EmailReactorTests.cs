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
        static EmailReactorTests()
        {
            // Ensure the handlebars helpers are registered.
            GC.KeepAlive(new EmailReactor());
        }

        [Fact]
        public void BuiltInPropertiesAreRenderedInTemplates()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{$Level}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal(data.Data.Level.ToString(), result);
        }

        [Fact]
        public void PayloadPropertiesAreRenderedInTemplates()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("See {{What}}");
            var data = Some.LogEvent(new Dictionary<string, object> { { "What", 10 } });
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("See 10", result);
        }

        [Fact]
        public void NoPropertiesAreRequiredOnASourceEvent()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("No properties");
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

        [Fact]
        public void IfEqHelperDetectsEquality()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{#if_eq $Level \"Fatal\"}}True{{/if_eq}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("True", result);
        }

        [Fact]
        public void IfEqHelperDetectsInequality()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{#if_eq $Level \"Warning\"}}True{{/if_eq}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("", result);
        }

        [Fact]
        public void TrimStringHelper1Arg()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("Fatal", result);
        }

        [Fact]
        public void TrimStringHelper2Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level 2}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("tal", result);
        }

        [Fact]
        public void TrimStringHelper3Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level 2 1}}");
            var data = Some.LogEvent();
            var result = EmailReactor.FormatTemplate(template, data, Some.Host());
            Assert.Equal("t", result);
        }

    }
}
