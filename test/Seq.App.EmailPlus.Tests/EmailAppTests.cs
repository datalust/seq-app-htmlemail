﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Security;
using MimeKit;
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
            var to = Assert.Single(sent.Message.To);
            Assert.Equal("test@example.com", to.ToString());
        }

        [Fact]
        public async Task OptionalAddressesAreTemplated()
        {
            var mail = new CollectingMailGateway();
            var app = new EmailApp(mail, new SystemClock())
            {
                From = "from@example.com",
                ReplyTo = "{{Name}}@example.com",
                To = "{{Name}}@example.com",
                Cc = "{{Name}}@example.com",
                Bcc = "{{Name}}@example.com",
                Host = "example.com"
            };

            app.Attach(new TestAppHost());

            var data = Some.LogEvent(includedProperties: new Dictionary<string, object> { { "Name", "test" } });
            await app.OnAsync(data);

            var sent = Assert.Single(mail.Sent);
            Assert.Equal("test@example.com", sent.Message.ReplyTo.ToString());
            Assert.Equal("test@example.com", sent.Message.Cc.ToString());
            Assert.Equal("test@example.com", sent.Message.Bcc.ToString());
        }

        [Fact]
        public void FallbackHostsCalculated()
        {
            var mail = new CollectingMailGateway();
            var reactor = new EmailApp(mail, new SystemClock())
            {
                From = "from@example.com",
                To = "{{Name}}@example.com",
                Host = "example.com,example2.com"
            };

            reactor.Attach(new TestAppHost());
            Assert.True(reactor.GetOptions().Host.Count() == 2);
        }

        [Fact]
        public void ParseDomainTest()
        {
            var mail = new DirectMailGateway();
            var domains = DirectMailGateway.GetDomains(new MimeMessage(
                new List<InternetAddress> {InternetAddress.Parse("test@example.com")},
                new List<InternetAddress> {InternetAddress.Parse("test2@example.com"), InternetAddress.Parse("test3@example.com"), InternetAddress.Parse("test@example2.com")}, "Test",
                (new BodyBuilder {HtmlBody = "test"}).ToMessageBody()));
            Assert.True(domains.Count() == 2);
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
        
        [Fact]
        public async Task ToAddressesCanBeCommaSeparated()
        {
            var mail = new CollectingMailGateway();
            var app = new EmailApp(mail, new SystemClock())
            {
                From = "from@example.com",
                To = "{{To}}",
                Host = "example.com"
            };

            app.Attach(new TestAppHost());

            var data = Some.LogEvent(includedProperties: new Dictionary<string, object> { { "To", ",first@example.com,,second@example.com, third@example.com," } });
            await app.OnAsync(data);

            var sent = Assert.Single(mail.Sent);
            Assert.Equal(3, sent.Message.To.Count);
        }

        [Theory]
        [InlineData(25, null, null, TlsOptions.Auto)]
        [InlineData(25, true, null, TlsOptions.StartTls)]
        [InlineData(25, false, TlsOptions.None, TlsOptions.None)]
        [InlineData(25, false, TlsOptions.StartTlsWhenAvailable, TlsOptions.StartTlsWhenAvailable)]
        [InlineData(587, true, TlsOptions.StartTls, TlsOptions.StartTls)]
        [InlineData(587, false, TlsOptions.None, TlsOptions.None)]
        [InlineData(587, false, TlsOptions.StartTlsWhenAvailable, TlsOptions.StartTlsWhenAvailable)]
        [InlineData(465, true, TlsOptions.None, TlsOptions.SslOnConnect)]
        [InlineData(465, false, TlsOptions.Auto, TlsOptions.SslOnConnect)]
        [InlineData(465, false, TlsOptions.SslOnConnect, TlsOptions.SslOnConnect)]
        public void CorrectSecureSocketOptionsAreChosenForPort(int port, bool? enableSsl, TlsOptions? enableTls, TlsOptions expected)
        {
            Assert.Equal(expected, SmtpOptions.GetSocketOptions(port, enableSsl, enableTls));
        }
    }
}
