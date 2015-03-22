using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Seq.App.EmailPlus
{
    public class BatchingStream<TKey,TValue> : IBatchingStream<TValue>
    {
        private readonly Func<TValue, TKey> _keySelector;
        private readonly IScheduler _scheduler;
        private readonly TimeSpan? _delay;
        private readonly TimeSpan? _maxDelay;
        private readonly int? _maxSize;
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
                    Debug.WriteLine("[{0:O}] Starting group with key [{1}].", _scheduler.Now, group.Key);

                    // Convert source to IObservable<long> so it can be merged with Observable<Timer> below.
                    // The particular value has no effect, it is just a signal.
                    var duration = group.Select(v => default(long));

                    if (_delay.HasValue)
                    {
                        Debug.WriteLine("Batch throttle of [{0}]s set for group with key [{1}].", _delay.Value.TotalSeconds, group.Key);
                        duration = duration.Throttle(_delay.Value, _scheduler);

                        if (_maxSize.HasValue)
                        {
                            Debug.WriteLine("Batch limit of [{0}] items set for group with key [{1}].", _maxSize.Value, group.Key);
                            duration = group.Select(v => default(long)).Skip(_maxSize.Value - 1).Merge(duration);
                        }

                        if (_maxDelay.HasValue)
                        {
                            Debug.WriteLine("Batch timeout of [{0}]s set for group with key [{1}].", _maxDelay.Value.TotalSeconds, group.Key);
                            duration = duration.Merge(Observable.Timer(_maxDelay.Value, _scheduler));
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Batch limit of [1] item set for group with key [{0}].", group.Key);
                    }

                    return duration.Take(1).Do(_ => Debug.WriteLine("[{0:O}] Ending group [{1}].", _scheduler.Now, group.Key));
                });

                return grouped.SelectMany(g => g.ToList(), (o, list) => { Debug.WriteLine("[{0:O}] Emitting group of size [{1}].", _scheduler.Now, list.Count);
                                                                            return list;
                });
            }
        }
    }
}