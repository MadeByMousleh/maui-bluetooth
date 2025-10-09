using firmware_upgrade.BLEComamnds.Interfaces;

namespace firmware_upgrade.BLEComamnds.GetActorSwVersion
{
    public class GetActorSwVersionRequest : IRequest
    {
        private readonly byte[] _command = new byte[7];

        public GetActorSwVersionRequest()
        {
            InitCommand();
        }

        private void InitCommand()
        {
            byte[] bytes = new byte[5];
            bytes[0] = 0x01; // Protocol Version
            bytes[1] = 0x2B; // Telegram Type
            bytes[2] = 0x01; // Reserved
            bytes[3] = 0x07; // Total Length
            bytes[4] = 0x00; // Reserved

            ushort checksum = Helpers.BLE.CalculateCRC16(bytes);

            Array.Copy(bytes, _command, bytes.Length);
            _command[5] = (byte)(checksum & 0xFF);
            _command[6] = (byte)(checksum >> 8);
        }

        public byte[] Create()
        {
            return _command;
        }
    }
}