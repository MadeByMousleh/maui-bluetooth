using firmware_upgrade.BLE;

namespace firmware_upgrade.BLEComamnds.CheckPinCode
{
    public class CheckPinCodeReply : BLEReply
    {
        public byte Result { get; private set; }

        public CheckPinCodeReply(ReadOnlySpan<byte> rawBytes) : base(rawBytes) { }

        protected override void ParsePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 8)
                return;

            var reader = new SpanReader(payload);
            Result = reader.ReadByte();
        }

        public override string ToString()
        {
            return $"CheckPinCodeReply - (Result={Result})";
        }

        public override bool IsAck()
        {
            return Result == 0; // 0 indicates success
        }
    }
}