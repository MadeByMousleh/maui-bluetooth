namespace firmware_upgrade.BLEComamnds.Interfaces
{
    public class IGeneralReply
    {
        public bool IsAck { get; set; }
        public object Data { get; set; }

        public IGeneralReply() {
        IsAck = false;
            Data = null;
        }
    }
}
