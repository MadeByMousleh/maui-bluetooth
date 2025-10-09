using firmware_upgrade.BLEComamnds.Interfaces;

namespace firmware_upgrade.BLEComamnds.ActorBootPacket
{
    public class ActorBootPacketRequest : IRequest
    {
        private readonly byte[] _command;

        public ActorBootPacketRequest(byte[] packetData)
        {
            if (packetData.Length > 71)
                throw new ArgumentException("Packet data cannot exceed 71 bytes.", nameof(packetData));

            _command = new byte[7 + packetData.Length];
            InitCommand(packetData);
        }

        private void InitCommand(byte[] packetData)
        {
            byte[] header = new byte[5];
            header[0] = 0x01; 
            header[1] = 0x14; 
            header[2] = 0x01; 
            header[3] = 0x4E;
            header[4] = 0x00; 

            ushort checksum = Helpers.BLE.CalculateCRC16(header);

            Array.Copy(header, _command, header.Length);
            _command[5] = (byte)(checksum & 0xFF);
            _command[6] = (byte)(checksum >> 8);

            Array.Copy(packetData, 0, _command, 7, packetData.Length);
        }

        public byte[] Create()
        {
            return _command;
        }
    }
}