using System;

namespace Seq.App.FirstOfType
{
    class UInt32BloomFilter
    {
        readonly byte[] _bytes;

        public const int ByteLength = 1024;

        public UInt32BloomFilter()
            : this(new byte[ByteLength])
        {
        }

        public UInt32BloomFilter(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException("bytes");
            if (bytes.Length != ByteLength) throw new ArgumentOutOfRangeException("bytes");
            _bytes = bytes;
        }

        public byte[] Bytes
        {
            get { return _bytes; }
        }

        public void Add(uint value)
        {
            Set(Hash1(value));
            Set(Hash2(value));
            Set(Hash3(value));
        }

        public bool MayContain(uint value)
        {
            return Has(Hash1(value)) &&
                   Has(Hash2(value)) &&
                   Has(Hash3(value));
        }

        void Set(uint hash)
        {
            int bitInByte;
            int byteIndex;
            Locate(hash, out byteIndex, out bitInByte);
            var b = _bytes[byteIndex];
            var set = 1 << bitInByte;
            b = (byte)(b | set);
            _bytes[byteIndex] = b;
        }

        bool Has(uint hash)
        {
            int bitInByte;
            int byteIndex;
            Locate(hash, out byteIndex, out bitInByte);
            var b = _bytes[byteIndex];
            var i = b >> bitInByte;
            return (i & 1) != 0;
        }

        void Locate(uint hash, out int byteIndex, out int bitInByte)
        {
            var bitLength = _bytes.Length * 8;
            var index = (int)(hash % bitLength);
            byteIndex = index / 8;
            bitInByte = index % 8;
        }

        static uint Hash1(uint value)
        {
            return value;
        }

        static uint Hash2(uint value)
        {
            // c/o Bob Jenkins, via http://burtleburtle.net/bob/hash/integer.html
            value = (value + 0x7ed55d16) + (value << 12);
            value = (value ^ 0xc761c23c) ^ (value >> 19);
            value = (value + 0x165667b1) + (value << 5);
            value = (value + 0xd3a2646c) ^ (value << 9);
            value = (value + 0xfd7046c5) + (value << 3);
            value = (value ^ 0xb55a4f09) ^ (value >> 16);
            return value;
        }

        static uint Hash3(uint value)
        {
            // c/o Thomas Wang, via http://burtleburtle.net/bob/hash/integer.html
            value = (value ^ 61) ^ (value >> 16);
            value = value + (value << 3);
            value = value ^ (value >> 4);
            value = value * 0x27d4eb2d;
            value = value ^ (value >> 15);
            return value;
        }
    }
}
