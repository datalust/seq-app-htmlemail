using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Seq.App.EmailPlus.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Xunit;

namespace Seq.App.EmailPlus.Tests
{
    public class EmailAppTests
    {
        static EmailAppTests()
        {
            // Ensure the handlebars helpers are registered, since we test them before we're
            // sure of having created an instance of the app.
            GC.KeepAlive(new EmailApp());
        }

        [Fact]
        public void BuiltInPropertiesAreRenderedInTemplates()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{$Level}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal(data.Data.Level.ToString(), result);
        }

        [Fact]
        public void PayloadPropertiesAreRenderedInTemplates()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("See {{What}}");
            var data = Some.LogEvent(includedProperties:new Dictionary<string, object> { { "What", 10 } });
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
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
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("No properties", result);
        }

        [Fact]
        public void IfEqHelperDetectsEquality()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{#if_eq $Level \"Fatal\"}}True{{/if_eq}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("True", result);
        }

        [Fact]
        public void IfEqHelperDetectsInequality()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{#if_eq $Level \"Warning\"}}True{{/if_eq}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("", result);
        }

        [Fact]
        public void TrimStringHelper0Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("", result);
        }

        [Fact]
        public void TrimStringHelper1Arg()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("Fatal", result);
        }

        [Fact]
        public void TrimStringHelper2Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level 2}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("tal", result);
        }

        [Fact]
        public void TrimStringHelper3Args()
        {
            var template = HandlebarsDotNet.Handlebars.Compile("{{substring $Level 2 1}}");
            var data = Some.LogEvent();
            var result = EmailApp.FormatTemplate(template, data, Some.Host());
            Assert.Equal("t", result);
        }


        [Fact]
        public async Task ToAddressesAreTemplated()
        {
            var mail = new CollectingMailGateway();
            var app = new EmailApp(mail, new SystemClock())
            {
                From = "from@example.com",
                To = "{{Name}}@example.com",
                Host = "example.com"
            };

            app.Attach(new TestAppHost());

            var data = Some.LogEvent(includedProperties: new Dictionary<string, object> { { "Name", "test" } });
            await app.OnAsync(data);

            var sent = Assert.Single(mail.Sent);
            Assert.Equal("test@example.com", sent.Message.To.ToString());
        }

        [Fact]
        public async Task EventsAreSuppressedWithinWindow()
        {
            var mail = new CollectingMailGateway();
            var clock = new TestClock();
            var app = new EmailApp(mail, clock)
            {
                From = "from@example.com",
                To = "to@example.com",
                Host = "example.com",
                SuppressionMinutes = 10
            };

            app.Attach(new TestAppHost());

            await app.OnAsync(Some.LogEvent(eventType: 99));
            clock.Advance(TimeSpan.FromMinutes(1));
            await app.OnAsync(Some.LogEvent(eventType: 99));
            await app.OnAsync(Some.LogEvent(eventType: 99));

            Assert.Single(mail.Sent);
            mail.Sent.Clear();

            clock.Advance(TimeSpan.FromHours(1));

            await app.OnAsync(Some.LogEvent(eventType: 99));

            Assert.Single(mail.Sent);
        }

        [Fact]
        public async Task EventsAreSuppressedByType()
        {
            var mail = new CollectingMailGateway();
            var app = new EmailApp(mail, new SystemClock())
            {
                From = "from@example.com",
                To = "to@example.com",
                Host = "example.com",
                SuppressionMinutes = 10
            };

            app.Attach(new TestAppHost());

            await app.OnAsync(Some.LogEvent(eventType: 1));
            await app.OnAsync(Some.LogEvent(eventType: 2));
            await app.OnAsync(Some.LogEvent(eventType: 1));

            Assert.Equal(2, mail.Sent.Count);
        }
    }
}
