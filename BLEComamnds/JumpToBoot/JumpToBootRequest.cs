
namespace firmware_upgrade.BLEComamnds.JumpToBoot
{

    public enum JumpToBootPayload
    {
        Sensor = 0x01,
        Actor = 0x02
    }
    public class JumpToBootRequest
    {
        private byte[] _command = new byte[8];

        public JumpToBootRequest(JumpToBootPayload payloadData = JumpToBootPayload.Sensor)
        {
            InitCommand(payloadData);
        }

        private void InitCommand(JumpToBootPayload payload)
        {
            byte[] bytes = new byte[5];
            bytes[0] = 0x01;
            bytes[1] = 0x01;
            bytes[2] = 0x00;
            bytes[3] = 0x08;
            bytes[4] = 0x00;

            ushort checksum = Helpers.BLE.CalculateCRC16(bytes);

            Array.Copy(bytes, _command, bytes.Length);

            _command[5] = (byte)(checksum & 0xFF);
            _command[6] = (byte)(checksum >> 8);
            _command[7] = (byte)payload;

        }

        public byte[] Create()
        {
            if (_command.Length == 0) return [];
            Console.WriteLine("helo" + BitConverter.ToString(_command).Replace("-", ""));
            return _command;
        }
    }
}
