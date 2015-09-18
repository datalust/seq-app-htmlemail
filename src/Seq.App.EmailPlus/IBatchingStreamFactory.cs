using System;
using System.Reactive.Concurrency;

namespace Seq.App.EmailPlus
{
    public interface IBatchingStreamFactory<in TKey, TValue>
    {
        IBatchingStream<TValue> Create(
            Func<TValue, TKey> keySelector, IScheduler scheduler, TimeSpan? delay, TimeSpan? maxDelay, int? maxSize);
    }
}