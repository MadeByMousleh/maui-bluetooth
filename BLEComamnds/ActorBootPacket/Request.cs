using firmware_upgrade.BLEComamnds.Interfaces;
using System.Linq;

namespace firmware_upgrade.BLEComamnds.ActorBootPacket
{
    public class ActorBootPacketRequest : IRequest
    {
        public readonly byte[] _command;

        public ActorBootPacketRequest(byte[] packetData)
        {
            if (packetData.Length > 71)
                throw new ArgumentException("Packet data cannot exceed 71 bytes.", nameof(packetData));

            // Calculate dynamic command size: header(7) + packetData length
            _command = new byte[7 + packetData.Length];
            InitCommand(packetData);
        }

        private void InitCommand(byte[] packetData)
        {
            byte startCommand = 0x01;
            ushort telegramType = 0x0014;   // 2 bytes (0x14, 0x00 in little endian)
            
            int offset = 0;

            // Header: start(1) + type(2) + length(2) + checksum(2) = 7 bytes total
            byte[] header = new byte[1 + 2 + 2 + 2];

            // Calculate total length: header(7) + packetData
            ushort totalLength = (ushort)(header.Length + packetData.Length);

            // --- Build header ---
            header[offset++] = startCommand;

            // telegramType (little endian)
            header[offset++] = (byte)(telegramType & 0xFF);
            header[offset++] = (byte)(telegramType >> 8);

            // totalLength (little endian) - DYNAMICALLY CALCULATED
            header[offset++] = (byte)(totalLength & 0xFF);
            header[offset++] = (byte)(totalLength >> 8);

            // Calculate CRC for header so far (before adding checksum itself)
            ushort checksum = Helpers.BLE.CalculateCRC16(header.Take(offset).ToArray());

            // append checksum (little endian)
            header[offset++] = (byte)(checksum & 0xFF);
            header[offset++] = (byte)(checksum >> 8);

            // --- Build full command ---
            // Copy header to command array
            Array.Copy(header, 0, _command, 0, header.Length);

            // Copy packet data after header
            Array.Copy(packetData, 0, _command, header.Length, packetData.Length);
        }

        public byte[] Create()
        {
            return _command;
        }
    }
}