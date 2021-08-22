using System;

namespace Seq.App.EmailPlus
{
    class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}