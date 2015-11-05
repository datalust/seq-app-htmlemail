using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Seq.App.Thresholds;

using NUnit.Framework;
using Moq;

using Seq.App.Thresholds.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;

using Serilog;

namespace Seq.App.Thresholds.Tests
{
    [TestFixture]
    public class ThresholdReactorTests
    {
        private Mock<ILogger> _logger; 

        [Test]
        public void when_threshold_exceeded_only_one_message_should_be_logged()
        {
            // arrange
            var sut = new ThresholdReactor()
            {
                EventsInWindowThreshold = 5, 
                ThresholdName = Guid.NewGuid().ToString(), 
                WindowSeconds = 20
            };

            _logger = new Mock<ILogger>();

            var appHost = new Mock<IAppHost>();
            appHost.SetupGet(h => h.Host).Returns(new Host(new[] { "localhost" }, "test"));
            appHost.SetupGet(h => h.Logger).Returns(_logger.Object);

            sut.Attach(appHost.Object);
            
            // act
            SendEvents(sut, 7);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
        }

        private static void SendEvents(ISubscribeTo<LogEventData> sut, int numberOfEvents)
        {
            for (var i = 0; i < numberOfEvents; i++)
            {
                sut.On(Some.LogEvent());
            }
        }
    }
}
