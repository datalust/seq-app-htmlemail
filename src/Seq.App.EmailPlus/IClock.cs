using System;

namespace Seq.App.EmailPlus
{
    interface IClock
    {
        DateTime UtcNow { get; }
    }
}
