using System.Runtime.InteropServices;

namespace firmware_upgrade.BLE
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLEHeader
    {
        public byte ProtocolVersion;
        public ushort TelegramType;
        public ushort TotalLength;
        public ushort Crc16;
    }
}
