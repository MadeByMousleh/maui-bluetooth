
namespace firmware_upgrade.BLEComamnds
{
    public class PayloadReader
    {
        private readonly byte[] _data;
        private int _position;

        public PayloadReader(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _position = 0;
        }

        public bool HasMore => _position < _data.Length;
        public int Remaining => _data.Length - _position;

        public byte ReadByte()
        {
            EnsureAvailable(1);
            return _data[_position++];
        }

        public byte[] ReadBytes(int length)
        {
            EnsureAvailable(length);
            byte[] result = new byte[length];
            Array.Copy(_data, _position, result, 0, length);
            _position += length;
            return result;
        }

        public ushort ReadUInt16()
        {
            EnsureAvailable(2);
            ushort value = BitConverter.ToUInt16(_data, _position);
            _position += 2;
            return value;
        }

        private void EnsureAvailable(int length)
        {
            if (_position + length > _data.Length)
                throw new InvalidOperationException("Not enough bytes remaining in payload");
        }
    }
}
