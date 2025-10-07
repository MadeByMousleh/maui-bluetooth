using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace firmware_upgrade.BLE
{
    /// <summary>
    /// High-performance span-based reader for BLE payloads.
    /// </summary>
    public ref struct SpanReader
    {
        private ReadOnlySpan<byte> _span;
        private int _position;

        public SpanReader(ReadOnlySpan<byte> span)
        {
            _span = span;
            _position = 0;
        }

        public bool HasMore => _position < _span.Length;
        public int Remaining => _span.Length - _position;

        public byte ReadByte() => _span[_position++];

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if (_position + length > _span.Length)
                throw new ArgumentOutOfRangeException(nameof(length));
            var slice = _span.Slice(_position, length);
            _position += length;
            return slice;
        }

        public ushort ReadUInt16()
        {
            if (_position + 2 > _span.Length)
                throw new ArgumentOutOfRangeException();
            ushort val = BitConverter.ToUInt16(_span.Slice(_position, 2));
            _position += 2;
            return val;
        }

        public T ReadStruct<T>() where T : unmanaged
        {
            int size = Marshal.SizeOf<T>();
            if (_position + size > _span.Length)
                throw new ArgumentOutOfRangeException(nameof(T));
            T value = MemoryMarshal.Read<T>(_span.Slice(_position, size));
            _position += size;
            return value;
        }
    }
}
