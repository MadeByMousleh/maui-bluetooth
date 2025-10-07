
using System.Runtime.InteropServices;

namespace firmware_upgrade.BLE
{
    
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct RequestHeader
        {
            public byte ProtocolVersion;
            public ushort TelegramType; // 2 bytes, little-endian

            public RequestHeader(byte protocolVersion, ushort telegramType)
            {
                ProtocolVersion = protocolVersion;
                TelegramType = telegramType;
            }

            /// <summary>
            /// Write the header to a buffer (no allocations)
            /// </summary>
            public void WriteTo(Span<byte> buffer)
            {
                if (buffer.Length < 3)
                    throw new ArgumentException("Buffer too small for header");

                buffer[0] = ProtocolVersion;
                buffer[1] = (byte)(TelegramType & 0xFF); // low byte
                buffer[2] = (byte)((TelegramType >> 8) & 0xFF); // high byte
            }
        }
    

}
