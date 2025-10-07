using System.Runtime.InteropServices;


namespace firmware_upgrade.BLE
{
    /// <summary>
    /// Base class for all BLE reply packets.
    /// Handles parsing of header and provides the payload to derived classes.
    /// </summary>
    /// <summary>
    /// Base class for all BLE replies.
    /// Parses header and provides payload to derived classes.
    /// </summary>
    public abstract class BLEReply : IReply
    {
        private readonly BLEHeader _header;
        private readonly ReadOnlyMemory<byte> _payload;

        protected BLEReply(ReadOnlySpan<byte> rawBytes)
        {
            int headerSize = Marshal.SizeOf<BLEHeader>();
            if (rawBytes.Length < headerSize)
                throw new ArgumentException("Invalid BLE reply: too short");

            var reader = new SpanReader(rawBytes);

            // Read header in one shot
            _header = reader.ReadStruct<BLEHeader>();

            // Remaining bytes are payload
            _payload = reader.ReadBytes(reader.Remaining).ToArray();

            // Let derived class parse its payload
            ParsePayload(_payload.Span);
        }

        /// <summary>
        /// Derived classes implement payload parsing
        /// </summary>
        protected abstract void ParsePayload(ReadOnlySpan<byte> payload);


        // Header accessors
        public byte ProtocolVersion => _header.ProtocolVersion;
        public ushort TelegramType => _header.TelegramType;
        public ushort TotalLength => _header.TotalLength;
        public ushort Crc16 => _header.Crc16;

        public abstract bool IsAck();

        public ReadOnlyMemory<byte> Payload => _payload;

        public override string ToString()
        {
            return $"BLEReply(Type=0x{TelegramType:X4}, Len={TotalLength}, CRC=0x{Crc16:X4})";
        }
    }
}
