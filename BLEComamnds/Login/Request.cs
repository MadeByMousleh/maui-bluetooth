using firmware_upgrade.BLEComamnds.Interfaces;

namespace firmware_upgrade.BLEComamnds.JumpToBoot
{

    public class LoginRequest: IRequest
    {
        private byte[] _command = new byte[9];
        public  LoginRequest() {
            InitCommand();
        }

        private void InitCommand()
        {
            byte[] bytes = new byte[5];
            bytes[0] = 0x01;
            bytes[1] = 0x10;
            bytes[2] = 0x00;
            bytes[3] = 0x09;
            bytes[4] = 0x00;

            ushort checksum = Helpers.BLE.CalculateCRC16(bytes);

            Array.Copy(bytes, _command, bytes.Length);

            _command[5] = (byte)(checksum & 0xFF);
            _command[6] = (byte)(checksum >> 8);
            _command[7] = 0x1D;
            _command[8] = 0x01;

        }

        public byte[] Create()
        {
            if (_command.Length == 0) return [];
            Console.WriteLine("helo" + BitConverter.ToString(_command).Replace("-", ""));
            return _command; 
        }
    }
}
