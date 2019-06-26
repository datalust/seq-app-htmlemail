using System;
using Moq;
using Seq.App.Thresholds.Tests.Support;
using Seq.Apps;
using Seq.Apps.LogEvents;
using Serilog;
using Xunit;

namespace Seq.App.Thresholds.Tests
{
    public class ThresholdReactorTests
    {
        private Mock<ILogger> _logger;

        public ThresholdReactorTests()
        {
            _logger = new Mock<ILogger>();
        }

        [Fact]
        public void when_wrapped_before_threshold_reached_should_not_log()
        {
            const int SecondsBetweenLogs = 45;

            // arrange
            var sut = GetThresholdReactor(5);

            // act
            SendXEventsNSecondsApart(sut, 7, SecondsBetweenLogs);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
        }

        [Fact]
        public void when_threshold_exceeded_over_time_should_log_only_1_message()
        {
            const int SecondsBetweenLogs = 2;

            // arrange
            var sut = GetThresholdReactor(5);

            // act
            SendXEventsNSecondsApart(sut, 7, SecondsBetweenLogs);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
        }

        [Fact]
        public void when_threshold_exceeded_should_log_only_1_message()
        {
            const int SecondsBetweenLogs = 0;

            // arrange
            var sut = GetThresholdReactor(5);

            // act
            SendXEventsNSecondsApart(sut, 7, SecondsBetweenLogs);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Once());
        }

        [Fact]
        public void when_reset_disabled_and_threshold_exceeded_should_log_only_3_message()
        {
            const int ExpectLogs = 3;
            const int SecondsBetweenLogs = 0;

            // arrange
            var sut = GetThresholdReactor(5, false);

            // act
            SendXEventsNSecondsApart(sut, 7, SecondsBetweenLogs);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly(ExpectLogs));
        }

        [Fact]
        public void when_threshold_tripled_over_time_should_get_3_logs()
        {
            const int ExpectLogs = 3;
            const int Threshold = 5;
            const int SecondsBetweenLogs = 2;
            var sut = GetThresholdReactor(Threshold);

            // act
            SendXEventsNSecondsApart(sut, Threshold * ExpectLogs, SecondsBetweenLogs);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly(ExpectLogs));
        }

        [Fact]
        public void when_threshold_tripled_should_get_3_logs()
        {
            const int ExpectLogs = 3;
            const int Threshold = 5;
            const int SecondsBetweenLogs = 2;
            var sut = GetThresholdReactor(Threshold);

            // act
            SendXEventsNSecondsApart(sut, Threshold * ExpectLogs, SecondsBetweenLogs);

            // assert
            _logger.Verify(l => l.Information(It.IsAny<string>(), It.IsAny<object[]>()), Times.Exactly(ExpectLogs));
        }

        private void SendXEventsNSecondsApart(ISubscribeTo<LogEventData> sut, int numberOfEvents, int secondsBetweenEvents = 0)
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
        public void burn_in_fuzzing()
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

        private ThresholdReactor GetThresholdReactor(int threshold, bool resetOnThresholdReached = true)
        {
            var sut = new ThresholdReactor
            {
                    EventsInWindowThreshold = threshold,
                    ThresholdName = Guid.NewGuid().ToString(),
                    WindowSeconds = 120,
                    ResetOnThresholdReached = resetOnThresholdReached
                };

            var appHost = new Mock<IAppHost>();
            appHost.SetupGet(h => h.Host).Returns(new Host(new[] { "localhost" }, "test"));
            appHost.SetupGet(h => h.Logger).Returns(_logger.Object);

            sut.Attach(appHost.Object);

            return sut;
        }
    }
}