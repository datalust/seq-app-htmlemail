using System;
using Moq;
using Seq.App.Thresholds.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog;
using Xunit;

// ReSharper disable RedundantArgumentDefaultValue

namespace Seq.App.Thresholds.Tests
{
    public class ThresholdReactorTests
    {
        readonly Mock<ILogger> _logger;

        public ThresholdReactorTests()
        {
            _logger = new Mock<ILogger>();
        }

        [Fact]
        public void WhenWrappedBeforeThresholdReachedShouldNotLog()
        {
            const int secondsBetweenLogs = 45;

            var sut = GetThresholdReactor(5);

            SendXEventsNSecondsApart(sut, 7, secondsBetweenLogs);
            
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Fact]
        public void WhenThresholdExceededOverTimeShouldLogOneMessage()
        {
            const int secondsBetweenLogs = 2;

            var sut = GetThresholdReactor(5);

            SendXEventsNSecondsApart(sut, 7, secondsBetweenLogs);

            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
        }

        [Fact]
        public void WhenThresholdExceededShouldLogOneMessage()
        {
            const int secondsBetweenLogs = 0;

            var sut = GetThresholdReactor(5);

            SendXEventsNSecondsApart(sut, 7, secondsBetweenLogs);

            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
        }

        [Fact]
        public void WhenResetDisabledAndThresholdExceededShouldLogThreeMessages()
        {
            const int expectLogs = 3;
            const int secondsBetweenLogs = 0;

            var sut = GetThresholdReactor(5, false);

            SendXEventsNSecondsApart(sut, 7, secondsBetweenLogs);

            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly(expectLogs));
        }

        [Fact]
        public void WhenThresholdTripledOverTimeShouldLogThreeMessages()
        {
            const int expectLogs = 3;
            const int threshold = 5;
            const int secondsBetweenLogs = 2;
            var sut = GetThresholdReactor(threshold);

            SendXEventsNSecondsApart(sut, threshold * expectLogs, secondsBetweenLogs);

            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly(expectLogs));
        }

        [Fact]
        public void WhenThresholdTripledShouldLogThreeMessages()
        {
            const int expectLogs = 3;
            const int threshold = 5;
            const int secondsBetweenLogs = 2;
            var sut = GetThresholdReactor(threshold);

            SendXEventsNSecondsApart(sut, threshold * expectLogs, secondsBetweenLogs);

            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly(expectLogs));
        }

        static void SendXEventsNSecondsApart(ISubscribeTo<LogEventData> sut, int numberOfEvents, int secondsBetweenEvents = 0)
        {
            var firstEventTime = Some.UtcTimestamp();

            for (var i = 0; i < numberOfEvents; i++)
            {
                var eventTime = firstEventTime + TimeSpan.FromSeconds(i * secondsBetweenEvents);
                var @event = Some.LogEvent(timestamp: eventTime);
                sut.On(@event);
            }
        }

        [Fact]
        public void BurnInFuzzing()
        {
            var now = DateTime.UtcNow;
            // ReSharper disable once AccessToModifiedClosure
            var rng = new Random();
            var sut = GetThresholdReactor(100001);

            for (var i = 0; i < 100000; ++i)
            {
                now = now.AddSeconds(rng.Next(0, 360) - 176);
                var @event = Some.LogEvent(timestamp: now);
                sut.On(@event);
            }
        }

        ThresholdReactor GetThresholdReactor(int threshold, bool resetOnThresholdReached = true)
        {
            var sut = new ThresholdReactor
            {
                    EventsInWindowThreshold = threshold,
                    ThresholdName = Guid.NewGuid().ToString(),
                    WindowSeconds = 120,
                    ResetOnThresholdReached = resetOnThresholdReached
                };

            var appHost = new Mock<IAppHost>();
            appHost.SetupGet(h => h.Host).Returns(new Host("https://seq.example.com", "test"));
            appHost.SetupGet(h => h.Logger).Returns(_logger.Object);

            sut.Attach(appHost.Object);

            return sut;
        }
    }
}