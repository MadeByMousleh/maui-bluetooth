using firmware_upgrade.BLE;

namespace firmware_upgrade.BLEComamnds.GetSensorSwVersion
{
    public class GetSensorSwVersionReply : BLEReply
    {
        public string SoftwareVersions { get; private set; } = string.Empty;

        public GetSensorSwVersionReply(ReadOnlySpan<byte> rawBytes) : base(rawBytes) { }

        protected override void ParsePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
                return;

            var reader = new SpanReader(payload); // Skip header
            SoftwareVersions = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(reader.Remaining).ToArray());
        }

        public override string ToString()
        {
            return $"GetSensorSwVersionReply - (SoftwareVersions={SoftwareVersions})";
        }

        public override bool IsAck()
        {
            return true; // Assuming all responses are acknowledged
        }
    }
}