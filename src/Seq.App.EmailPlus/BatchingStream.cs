using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Seq.App.EmailPlus
{
    public class BatchingStream<TKey, TValue> : IBatchingStream<TValue>
    {
        private readonly TimeSpan? _delay;
        private readonly Func<TValue, TKey> _keySelector;
        private readonly TimeSpan? _maxDelay;
        private readonly int? _maxSize;
        private readonly IScheduler _scheduler;
        private readonly Subject<TValue> _stream;

        public BatchingStream(Func<TValue, TKey> keySelector, IScheduler scheduler, TimeSpan? delay = null, TimeSpan? maxDelay = null, int? maxSize = null)
        {
            _keySelector = keySelector;
            _scheduler = scheduler;
            _delay = delay;
            _maxDelay = maxDelay;
            _maxSize = maxSize;
            _stream = new Subject<TValue>();
        }

        public void Add(TValue value)
        {
            _stream.OnNext(value);
        }

        public IObservable<IList<TValue>> Batches
        {
            get
            {
                var grouped = _stream.GroupByUntil(v => _keySelector(v), group =>
                {
                    // Convert source to IObservable<long> so it can be merged with Observable<Timer> below.
                    // The particular value has no effect, it is just a signal.
                    var duration = group.Select(v => default(long));

                    if (!_delay.HasValue)
                        return duration.Take(1);

                    duration = duration.Throttle(_delay.Value, _scheduler);

                    if (_maxSize.HasValue)
                        duration = @group.Select(v => default(long)).Skip(_maxSize.Value - 1).Merge(duration);

                    if (_maxDelay.HasValue)
                        duration = duration.Merge(Observable.Timer(_maxDelay.Value, _scheduler));

                    return duration.Take(1);
                });

                return grouped.SelectMany(g => g.ToList(), (o, list) => list);
            }
        }
    }
}