using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
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
        public void SendsFromCorrectAddress()
        {
            var mailClient = new Mock<IMailClient>();
            mailClient.Setup(mc => mc.Send(It.Is<MailMessage>(mm => mm.From.Address.Equals("baz@qux.com"))))
                .Verifiable();

            var mailFactory = new Mock<IMailClientFactory>();
            mailFactory.Setup(mf => mf.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(mailClient.Object)
                .Verifiable();

            var reactor = GetEmailReactor(mailFactory.Object);
            reactor.On(LogEvent);

            mailFactory.Verify();
            mailClient.Verify();
        }

        [Test]
        public void SendsToCorrectAddress()
        {
            var mailClient = new Mock<IMailClient>();
            mailClient.Setup(mc => mc.Send(It.Is<MailMessage>(mm => mm.To.Any(a => a.Address.Equals("foo@bar.com")))))
                .Verifiable();

            var mailFactory = new Mock<IMailClientFactory>();
            mailFactory.Setup(mf => mf.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(mailClient.Object)
                .Verifiable();

            var reactor = GetEmailReactor(mailFactory.Object);
            reactor.On(LogEvent);

            mailFactory.Verify();
            mailClient.Verify();
        }

        [Test]
        public void CanHavePropertyInSubject()
        {
            var mailClient = new Mock<IMailClient>();
            mailClient.Setup(mc => mc.Send(It.Is<MailMessage>(mm => mm.Subject.Equals("[Information] [Security] Test (via Seq)"))))
                .Verifiable();

            var mailFactory = new Mock<IMailClientFactory>();
            mailFactory.Setup(mf => mf.Create(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>()))
                .Returns(mailClient.Object)
                .Verifiable();

            var reactor = GetEmailReactor(mailFactory.Object);
            reactor.On(LogEvent);

            mailFactory.Verify();
            mailClient.Verify();
        }

        private static Event<LogEventData> LogEvent
        {
            get
            {
                var logEventData = new LogEventData
                {
                    Id = "1",
                    Level = LogEventLevel.Information,
                    LocalTimestamp = DateTimeOffset.Now,
                    RenderedMessage = "Test",
                    Properties = new Dictionary<string, object> { { "Category", "Security" } }
                };
                return new Event<LogEventData>(logEventData.Id, 1, DateTime.UtcNow, logEventData);
            }
        }

        private static EmailReactor GetEmailReactor(IMailClientFactory mailClientFactory)
        {
            var appHost = new Mock<IAppHost>();
            appHost.SetupGet(h => h.Host).Returns(new Host(new[] { "localhost" }, "test"));

            var reactor = new EmailReactor(mailClientFactory)
            {
                From = "baz@qux.com",
                To = "foo@bar.com",
                SubjectTemplate = "[{{$Level}}] [{{$Properties.Category}}] {{{$Message}}} (via Seq)"
            };
            reactor.Attach(appHost.Object);

            return reactor;
        }
    }
}