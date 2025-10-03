using System;
using System.Linq;

public class TelegramHeader
{
    private byte _protocolVersion;
    private byte[] _telegramType;
    private byte[] _totalLength;
    private byte[] _crc16;
    public byte[] Bytes { get; }

    public TelegramHeader(byte protocolVersion, ushort telegramType, ushort totalLength)
    {
        _protocolVersion = protocolVersion;
        _telegramType = Split16Bytes(telegramType).Reverse().ToArray();
        _totalLength = Split16Bytes(totalLength).Reverse().ToArray();

        var headerWithoutCrc = new byte[] { _protocolVersion }
            .Concat(_telegramType)
            .Concat(_totalLength)
            .ToArray();

        _crc16 = Split16Bytes(CalculateCrc(headerWithoutCrc)).Reverse().ToArray();

        Bytes = new byte[] { _protocolVersion }
            .Concat(_telegramType)
            .Concat(_totalLength)
            .Concat(_crc16)
            .ToArray();
    }

    private ushort CalculateCrc(byte[] message, ushort crc = 0x8005, ushort poly = 0x1021)
    {
        for (int i = 0; i < message.Length; i++)
        {
            ushort s = (ushort)(message[i] << 8);
            crc = (ushort)(crc ^ s);

            for (int j = 0; j < 8; j++)
            {
                if ((crc & 0x8000) > 0)
                {
                    crc = (ushort)(((crc << 1) ^ poly) & 0xFFFF);
                }
                else
                {
                    crc = (ushort)(crc << 1);
                }
            }
        }
        return crc;
    }

    private byte[] Split16Bytes(ushort value)
    {
        byte low = (byte)(value & 0xFF);
        byte high = (byte)(value >> 8);
        return new byte[] { high, low };
    }
}