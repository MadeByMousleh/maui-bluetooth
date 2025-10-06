namespace firmware_upgrade.BLEComamnds.JumpToBoot
{
    public enum JumpToBootPayload
    {
        Sensor = 0x01,
        Actor = 0x02
    }
    public class Request
    {
        private byte[] _command = new byte[8];
        public Request(JumpToBootPayload payload) {
        
        }

        private void InitCommand()
        {
            byte[] bytes = new byte[5];
            _command[0] = 0x01;
            _command[1] = 0x00;
            _command[2] = 0x01;
            _command[3] = 0x00;
            _command[4] = 0x08;

        }
    }
}
