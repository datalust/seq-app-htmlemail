using System;

namespace Seq.App.EmailPlus.Tests.Support
{
    class TestClock : IClock
    {
        public DateTime UtcNow { get; set; }

        public DateTime Advance(TimeSpan duration)
        {
            UtcNow = UtcNow.Add(duration);
            return UtcNow;
        }
    }
}