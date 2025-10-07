
namespace firmware_upgrade.BLE
{
    public static class BLEReplyRegistry
    {
        private static readonly Dictionary<ushort, Func<ReadOnlySpan<byte>, IReply>> _factories = new();

        public static void Register(ushort telegramType, Func<ReadOnlySpan<byte>, IReply> factory)
        {
            _factories[telegramType] = factory;
        }

        public static bool TryCreate(ushort telegramType, ReadOnlySpan<byte> rawBytes, out IReply reply)
        {
            if (_factories.TryGetValue(telegramType, out var factory))
            {
                reply = factory(rawBytes);
                return true;
            }

            reply = null!;
            return false;
        }
    }
}
