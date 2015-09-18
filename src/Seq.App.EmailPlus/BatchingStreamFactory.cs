using System;
using System.Reactive.Concurrency;

namespace Seq.App.EmailPlus
{
    public class BatchingStreamFactory<TKey, TValue> : IBatchingStreamFactory<TKey, TValue>
    {
        public IBatchingStream<TValue> Create(Func<TValue, TKey> keySelector, IScheduler scheduler, TimeSpan? delay, TimeSpan? maxDelay, int? maxSize)
        {
            return new BatchingStream<TKey, TValue>(keySelector, scheduler, delay, maxDelay, maxSize);
        }
    }
}