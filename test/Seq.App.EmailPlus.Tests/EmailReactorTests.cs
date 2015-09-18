using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using Microsoft.Reactive.Testing;
using Moq;
using Moq.Language.Flow;
using NUnit.Framework;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog;

namespace Seq.App.EmailPlus.Tests
{
    [TestFixture]
    public class EmailReactorTests : EventTestsBase
    {
        private Mock<IEmailFormatter> _emailFormatter;
        private Mock<IEmailFormatterFactory> _emailFormatterFactory;
        private Mock<IBatchingStream<Event<LogEventData>>> _eventStream;
        private Mock<IBatchingStreamFactory<string, Event<LogEventData>>> _eventStreamFactory;
        private IReturnsResult<IBatchingStreamFactory<string, Event<LogEventData>>> _eventStreamFactorySetup;
        private Mock<IMailClient> _mailClient;
        private Mock<IMailClientFactory> _mailClientFactory;
        private Mock<ILogger> _logger; 
        private TestScheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new TestScheduler();

            _mailClient = new Mock<IMailClient>();
            _mailClientFactory = new Mock<IMailClientFactory>();
            _mailClientFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>())).Returns(() => _mailClient.Object);

            _emailFormatter = new Mock<IEmailFormatter>();
            _emailFormatterFactory = new Mock<IEmailFormatterFactory>();
            _emailFormatterFactory.Setup(f => f.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>()))
                .Returns(() => _emailFormatter.Object);

            _eventStream = new Mock<IBatchingStream<Event<LogEventData>>>();
            _eventStream.Setup(es => es.Batches).Returns(new Mock<IObservable<IList<Event<LogEventData>>>>().Object);

            _eventStreamFactory = new Mock<IBatchingStreamFactory<string, Event<LogEventData>>>();
            _eventStreamFactorySetup = _eventStreamFactory.Setup(
                f =>
                    f.Create(It.IsAny<Func<Event<LogEventData>, string>>(), It.IsAny<IScheduler>(), It.IsAny<TimeSpan?>(), It.IsAny<TimeSpan?>(),
                        It.IsAny<int?>()))
                .Returns(() => _eventStream.Object);

            _logger = new Mock<ILogger>();
        }

        [Test]
        public void SendsFromCorrectAddress()
        {
            var actual = string.Empty;

            _mailClient.Setup(mc => mc.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message => actual = message.From.Address);

            var eventSubject = new Subject<List<Event<LogEventData>>>();
            _eventStream.SetupGet(es => es.Batches).Returns(eventSubject);
            _scheduler.Schedule(() => eventSubject.OnNext(new List<Event<LogEventData>> {GetLogEvent()}));

            GetEmailReactor();
            _scheduler.Start();

            Assert.AreEqual("baz@qux.com", actual, "Email from address was not the configured value.");
        }

        [Test]
        public void SendsToCorrectAddress()
        {
            var actual = new List<string>();

            _mailClient.Setup(mc => mc.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message => actual = message.To.Select(ma => ma.Address).ToList());

            var eventSubject = new Subject<List<Event<LogEventData>>>();
            _eventStream.SetupGet(es => es.Batches).Returns(eventSubject);
            _scheduler.Schedule(() => eventSubject.OnNext(new List<Event<LogEventData>> {GetLogEvent()}));

            GetEmailReactor();
            _scheduler.Start();

            Assert.Contains("foo@bar.com", actual, "Email to address list did not contain the configured value.");
        }

        [Test]
        public void SendsMessagesAsHtml()
        {
            var actual = false;

            _mailClient.Setup(mc => mc.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message => actual = message.IsBodyHtml);

            var eventSubject = new Subject<List<Event<LogEventData>>>();
            _eventStream.SetupGet(es => es.Batches).Returns(eventSubject);
            _scheduler.Schedule(() => eventSubject.OnNext(new List<Event<LogEventData>> {GetLogEvent()}));

            GetEmailReactor();
            _scheduler.Start();

            Assert.IsTrue(actual, "Message body was not configured as HTML.");
        }

        [Test]
        public void AddsEventsToEventStream()
        {
            var @event = GetLogEvent();

            var reactor = GetEmailReactor();
            reactor.On(@event);

            _eventStream.Verify(es => es.Add(It.Is<Event<LogEventData>>(evt => evt == @event)), Times.Once, "Event was not added to the event stream.");
        }

        [Test]
        public void UsesDelaySetting()
        {
            TimeSpan? actual = null;
            _eventStreamFactorySetup.Callback<Func<Event<LogEventData>, string>, IScheduler, TimeSpan?, TimeSpan?, int?>(
                (func, scheduler, delay, maxDelay, maxSize) => { actual = delay; }).Verifiable();

            GetEmailReactor(30);

            _eventStreamFactory.Verify();
            Assert.NotNull(actual, "Delay setting was not used.");
            Assert.AreEqual(30, actual.Value.TotalSeconds, "Incorrect delay setting was used.");
        }

        [Test]
        public void UsesMaxDelaySetting()
        {
            TimeSpan? actual = null;
            _eventStreamFactorySetup.Callback<Func<Event<LogEventData>, string>, IScheduler, TimeSpan?, TimeSpan?, int?>(
                (func, scheduler, delay, maxDelay, maxSize) => { actual = maxDelay; }).Verifiable();

            GetEmailReactor(maxDelay: 300);

            _eventStreamFactory.Verify();
            Assert.NotNull(actual, "Max delay setting was not used.");
            Assert.AreEqual(300, actual.Value.TotalSeconds, "Incorrect max delay setting was used.");
        }

        [Test]
        public void UsesMaxSizeSetting()
        {
            int? actual = null;
            _eventStreamFactorySetup.Callback<Func<Event<LogEventData>, string>, IScheduler, TimeSpan?, TimeSpan?, int?>(
                (func, scheduler, delay, maxDelay, maxSize) => { actual = maxSize; }).Verifiable();

            GetEmailReactor(maxSize: 50);

            _eventStreamFactory.Verify();
            Assert.NotNull(actual, "Max size was not used.");
            Assert.AreEqual(50, actual.Value, "Incorrect max size setting was used.");
        }

        [Test]
        public void SmtpExceptionIsHandled()
        {
            _mailClient.Setup(mc => mc.Send(It.IsAny<MailMessage>())).Throws<SmtpException>();

            var eventSubject = new Subject<List<Event<LogEventData>>>();
            _eventStream.SetupGet(es => es.Batches).Returns(eventSubject);
            _scheduler.Schedule(() => eventSubject.OnNext(new List<Event<LogEventData>> { GetLogEvent() }));

            GetEmailReactor();
            _scheduler.Start();

            _logger.Verify(l => l.Warning(It.IsAny<SmtpException>(), It.IsAny<string>()), Times.Once());
        }

        private EmailReactor GetEmailReactor(double? delay = null, double? maxDelay = null, int? maxSize = null)
        {
            var appHost = new Mock<IAppHost>();
            appHost.SetupGet(h => h.Host).Returns(new Host(new[] {"localhost"}, "test"));
            appHost.SetupGet(h => h.Logger).Returns(_logger.Object);

            var reactor = new EmailReactor(_mailClientFactory.Object, _emailFormatterFactory.Object, _eventStreamFactory.Object, _scheduler)
            {
                From = "baz@qux.com",
                To = "foo@bar.com",
                BatchDelay = delay,
                BatchMaxDelay = maxDelay,
                BatchMaxAmount = maxSize
            };
            reactor.Attach(appHost.Object);

            return reactor;
        }
    }
}