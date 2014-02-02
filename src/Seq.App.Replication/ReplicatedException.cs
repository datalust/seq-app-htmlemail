using System;

namespace Seq.App.Replication
{
    class ReplicatedException : Exception
    {
        readonly string _asString;

        public ReplicatedException(string asString)
        {
            _asString = asString;
        }

        public override string ToString()
        {
            return _asString;
        }
    }
}
