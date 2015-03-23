using System;
using System.Collections.Generic;

namespace Seq.App.EmailPlus
{
    public interface IBatchingStream<TValue>
    {
        IObservable<IList<TValue>> Batches { get; }
        void Add(TValue value);
    }
}