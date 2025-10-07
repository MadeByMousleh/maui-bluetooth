

namespace firmware_upgrade.BLEComamnds.Interfaces
{
    internal interface IReply
    {
        byte GetProtocolVersion();

        byte[] GetTotalLength();

        byte[] GetCrc16();


    }
}
