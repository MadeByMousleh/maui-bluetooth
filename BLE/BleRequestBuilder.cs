using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace firmware_upgrade.BLE
{
    public static class BLERequestBuilder
    {
        /// <summary>
        /// Builds a BLE request packet with header, payload, and CRC16.
        /// </summary>
        /// <param name="buffer">Pre-allocated buffer. Must be at least 3 + 2 + payload.Length + 2 bytes.</param>
        /// <param name="protocolVersion">BLE protocol version.</param>
        /// <param name="telegramType">BLE telegram type (command identifier).</param>
        /// <param name="payload">Payload bytes (can be empty).</param>
        public static void Build(Span<byte> buffer, byte protocolVersion, ushort telegramType, ReadOnlySpan<byte> payload)
        {
            const int headerSize = 3;    // protocolVersion + telegramType
            const int totalLengthSize = 2; // TotalLength field
            const int crcSize = 2;       // CRC16

            int requiredLength = headerSize + totalLengthSize + payload.Length + crcSize;
            if (buffer.Length < requiredLength)
                throw new ArgumentException($"Buffer too small. Required: {requiredLength}");

            // --- Header ---
            buffer[0] = protocolVersion;
            buffer[1] = (byte)(telegramType & 0xFF);       // low byte
            buffer[2] = (byte)((telegramType >> 8) & 0xFF); // high byte

            // --- TotalLength field (header + payload, excluding CRC) ---
            ushort totalLength = (ushort)(headerSize + totalLengthSize + payload.Length);
            buffer[3] = (byte)(totalLength & 0xFF);       // low byte
            buffer[4] = (byte)((totalLength >> 8) & 0xFF); // high byte

            // --- Payload ---
            payload.CopyTo(buffer.Slice(5));

            // --- CRC16 over header + totalLength + payload ---
            //ushort checksum = Helpers.BLE.CalculateChecksum(buffer.Slice(0, 5 + payload.Length));
            //buffer[5 + payload.Length] = (byte)(checksum & 0xFF);       // CRC low
            //buffer[6 + payload.Length] = (byte)(checksum >> 8);          // CRC high
        }
    }
}
