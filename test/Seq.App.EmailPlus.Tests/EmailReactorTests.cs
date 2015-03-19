using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Moq;
using NUnit.Framework;
using Seq.Apps;
using Seq.Apps.LogEvents;

namespace Seq.App.EmailPlus.Tests
{
    [TestFixture]
    public class EmailReactorTests
    {
        [Test]
        public void CanHavePropertyInSubject()
        {
            var events = Observable.Return(GetLogEvent());
            Process(events,
                setup: reactor => reactor.SubjectTemplate = "[{{$Events.[0].$Properties.Category}}]",
                callback: message => Assert.IsTrue(message.Subject.Contains("[Security]")));
        }

        [Test]
        public void SendsFromCorrectAddress()
        {
            var events = Observable.Return(GetLogEvent());
            Process(events, callback: message => Assert.AreEqual("baz@qux.com", message.From.Address));
        }

        [Test]
        public void SendsToCorrectAddress()
        {
            var events = Observable.Return(GetLogEvent());
            Process(events, callback: message => Assert.Contains("foo@bar.com", message.To.Select(ma => ma.Address).ToList()));
        }

        [Test]
        public void WillBatchSendsWhenDelayIsSet()
        {
            var events = Observable.Range(1, 3).Select(id => GetLogEvent(id));
            Process(events, 3, 1, reactor => reactor.BatchDuplicateSubjectsDelay = 0.25);
        }

        [Test]
        public void WillNotBatchSendsWhenDelayIsNotSet()
        {
            var events = Observable.Range(1, 3).Select(id => GetLogEvent(id));
            Process(events, 3, 3);
        }

        [Test]
        public void WillNotBatchSendsWithDifferentSubjects()
        {
            var events = Observable.Range(1, 3).Select(id => GetLogEvent(id, id == 1 ? LogEventLevel.Warning : LogEventLevel.Information));
            Process(events, 3, 2, reactor => reactor.BatchDuplicateSubjectsDelay = 0.25);
        }

        [Test]
        public void WillHonorBatchMaxAmount()
        {
            var events = Observable.Range(1, 3).Select(id => GetLogEvent(id));
            Process(events, 3, 2, reactor =>
            {
                reactor.BatchDuplicateSubjectsDelay = 0.25;
                reactor.BatchMaxAmount = 2;
            });
        }

        [Test]
        // This test is fairly slow, but does not run reliably due to the events getting emitted and bunched together before they are consumed.
        public void WillHonorBatchMaxDelay()
        {
            var events = Observable.Interval(TimeSpan.FromSeconds(1)).Zip(Observable.Range(1, 6), (_, id) => GetLogEvent(id));
            Process(events, 6, 2, reactor =>
            {
                reactor.BatchDuplicateSubjectsDelay = 1.5;
                reactor.BatchMaxDelay = 3;
            });
        }

        [Test]
        public void DefaultTemplateBodyContainsAllBatchedEvents()
        {
            var events = Observable.Range(1, 3).Select(id => GetLogEvent(id));
            Process(events, 3, 1, reactor => reactor.BatchDuplicateSubjectsDelay = 0.25, message => Assert.AreEqual(3, CountSubstrings(message.Body, "div class=\"email-body\"")));
        }

        [Test]
        public void DefaultTemplateSubjectEndsWithCountOnBatchedEvents()
        {
            var events = Observable.Range(1, 3).Select(id => GetLogEvent(id));
            Process(events, 3, 1, reactor => reactor.BatchDuplicateSubjectsDelay = 0.25, message => Assert.IsTrue(message.Subject.EndsWith("(3)")));
        }

        [Test]
        public void DefaultTemplateSubjectDoesNotEndWithCountOnNormalEvents()
        {
            var events = Observable.Return(GetLogEvent());
            Process(events, callback: message => Assert.IsFalse(message.Subject.EndsWith("(1)")));
        }

        private static int CountSubstrings(string source, string substring)
        {
            return (source.Length - source.Replace(substring, string.Empty).Length)/substring.Length;
        }

        private static Event<LogEventData> GetLogEvent(int id = 0, LogEventLevel level = LogEventLevel.Information)
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

        private static EmailReactor GetEmailReactor(IMailClientFactory mailClientFactory)
        {
            var appHost = new Mock<IAppHost>();
            appHost.SetupGet(h => h.Host).Returns(new Host(new[] { "localhost" }, "test"));

            var reactor = new EmailReactor(mailClientFactory)
            {
                From = "baz@qux.com",
                To = "foo@bar.com",
            };
            reactor.Attach(appHost.Object);

            return reactor;
        }

        private static int GetEventsInMessage(MailMessage message)
        {
            var regex = new Regex(@"\((\d+)\)$");
            var eventsInMessage = 1;
            var regexGroups = regex.Match(message.Subject).Groups;
            if (regexGroups.Count > 1)
            {
                int.TryParse(regexGroups[1].Value, out eventsInMessage);
            }
            return eventsInMessage;
        }

        private static void MessageCallback(MailMessage message, ISubject<int, int> received, int expectedCount)
        {
            received.OnNext(GetEventsInMessage(message));
            if (received.TakeUntil(DateTimeOffset.UtcNow).Sum().Timeout(TimeSpan.FromSeconds(15)).Wait() >= expectedCount)
            {
                received.OnCompleted();
            }
        }

        private static void Process(IObservable<Event<LogEventData>> events, int eventCount = 1, int messageCount = 1, Action<EmailReactor> setup = null, Action<MailMessage> callback = null)
        {
            var mailClient = new Mock<IMailClient>();
            var mailFactory = new Mock<IMailClientFactory>();

            mailFactory.Setup(mf => mf.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(mailClient.Object)
                .Verifiable();

            var reactor = GetEmailReactor(mailFactory.Object);
            if (setup != null)
                setup(reactor);

            var received = new ReplaySubject<int>();
            mailClient.Setup(mc => mc.Send(It.IsAny<MailMessage>()))
                .Callback<MailMessage>(message =>
                {
                    MessageCallback(message, received, eventCount);
                    if (callback != null)
                        callback(message);
                });

            events.Subscribe(evt => reactor.On(evt));
            Assert.AreEqual(eventCount, received.Sum().Timeout(TimeSpan.FromSeconds(20)).Wait());
            mailFactory.Verify();
            mailClient.Verify(mc => mc.Send(It.IsAny<MailMessage>()), Times.Exactly(messageCount));
        }
    }
}