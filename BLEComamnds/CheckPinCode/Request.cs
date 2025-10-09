using firmware_upgrade.BLEComamnds.Interfaces;

namespace firmware_upgrade.BLEComamnds.CheckPinCode
{
    public class CheckPinCodeRequest : IRequest
    {
        private readonly byte[] _command;

        public CheckPinCodeRequest(ushort pinCode)
        {
            if (pinCode < 1000 || pinCode > 9999)
                throw new ArgumentException("PinCode must be between 1000 and 9999.", nameof(pinCode));

            _command = new byte[9];
            InitCommand(pinCode);
        }

        private void InitCommand(ushort pinCode)
        {
            byte[] header = new byte[5];
            header[0] = 0x01; // Protocol Version
            header[1] = 0x31; // Telegram Type
            header[2] = 0x01; // Reserved
            header[3] = 0x09; // Total Length
            header[4] = 0x00; // Reserved

            ushort checksum = Helpers.BLE.CalculateCRC16(header);

            Array.Copy(header, _command, header.Length);
            _command[5] = (byte)(checksum & 0xFF);
            _command[6] = (byte)(checksum >> 8);

            _command[7] = (byte)(pinCode & 0xFF);
            _command[8] = (byte)(pinCode >> 8);
        }

        public byte[] Create()
        {
            return _command;
        }
    }
}