

using firmware_upgrade.BLE;

namespace firmware_upgrade.BLEComamnds
{
    public class LoginReply : BLEReply
    {
        public byte Result { get; private set; }

        public LoginReply(ReadOnlySpan<byte> rawBytes) : base(rawBytes) { }

        protected override void ParsePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
                return;

            var reader = new SpanReader(payload);
            Result = reader.ReadByte();

            //if (reader.HasMore)
            //    Hours = reader.ReadByte();

            //if (reader.HasMore)
            //    Timers = reader.ReadBytes(reader.Remaining).ToArray();

        }

        public override string ToString()
        {
            return $"LoginReply - (Result={Result})";
        }

        public override bool IsAck()
        {
            switch(Result)
            {
                case 0: return false;
            }
            return true;

        }
    }
}
