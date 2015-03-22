using System;
using System.Collections.Generic;

namespace Seq.App.EmailPlus
{
    public interface IBatchingStream<TValue>
    {
        void Add(TValue value);
        IObservable<IList<TValue>> Batches { get; }
    }
}