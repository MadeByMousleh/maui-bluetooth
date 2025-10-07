using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace firmware_upgrade.BLE
{
    public interface IReply
    {
        byte ProtocolVersion { get; }
        ushort TelegramType { get; }
        ushort TotalLength { get; }
        ushort Crc16 { get; }

        public bool IsAck();
        ReadOnlyMemory<byte> Payload { get; }
    }
}
