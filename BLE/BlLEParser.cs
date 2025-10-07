

namespace firmware_upgrade.BLE
{
    public static class BLEParser
    {
        public static IReply Parse(ReadOnlySpan<byte> rawBytes)
        {
            if (rawBytes.Length < 3) // minimum to read TelegramType
                throw new ArgumentException("Invalid BLE reply");

            ushort telegramType = BitConverter.ToUInt16(rawBytes.Slice(1, 2));

            if (BLEReplyRegistry.TryCreate(telegramType, rawBytes, out var reply))
                return reply;

            throw new InvalidOperationException($"No reply registered for TelegramType 0x{telegramType:X4}");
        }
    }
}
