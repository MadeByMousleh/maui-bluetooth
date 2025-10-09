using firmware_upgrade.BLE;

namespace firmware_upgrade.BLEComamnds.ActorBootPacket
{
    public class ActorBootPacketReply : BLEReply
    {
        public byte[] PacketData { get; private set; } = Array.Empty<byte>();

        public ActorBootPacketReply(ReadOnlySpan<byte> rawBytes) : base(rawBytes) { }

        protected override void ParsePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
                return;

            var reader = new SpanReader(payload);

            PacketData = reader.ReadBytes(reader.Remaining).ToArray();
        }

        public override string ToString()
        {
            return $"ActorBootPacketReply - (PacketData={BitConverter.ToString(PacketData)})";
        }

        public override bool IsAck()
        {
            return true; // Assuming all responses are acknowledged
        }
    }
}