using firmware_upgrade.BLE;

namespace firmware_upgrade.BLEComamnds.GetActorSwVersion
{
    public class GetActorSwVersionReply : BLEReply
    {
        public string SoftwareVersions { get; private set; } = string.Empty;

        public GetActorSwVersionReply(ReadOnlySpan<byte> rawBytes) : base(rawBytes) { }

        protected override void ParsePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 7)
                return;

            var reader = new SpanReader(payload.Slice(7)); // Skip header
            SoftwareVersions = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.Remaining).ToArray());
        }

        public override string ToString()
        {
            return $"GetActorSwVersionReply - (SoftwareVersions={SoftwareVersions})";
        }

        public override bool IsAck()
        {
            return true; // Assuming all responses are acknowledged
        }
    }
}