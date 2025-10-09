using firmware_upgrade.BLE;

namespace firmware_upgrade.BLEComamnds.ActorBootState
{
    public class ActorBootStateReply : BLEReply
    {
        public byte BootState { get; private set; }

        public ActorBootStateReply(ReadOnlySpan<byte> rawBytes) : base(rawBytes) { }

        protected override void ParsePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
                return;

            var reader = new SpanReader(payload);
            BootState = reader.ReadByte();
        }

        public override string ToString()
        {
            return $"ActorBootStateReply - (BootState={BootState})";
        }

        public override bool IsAck()
        {
            return true; // Assuming all responses are acknowledged
        }
    }
}