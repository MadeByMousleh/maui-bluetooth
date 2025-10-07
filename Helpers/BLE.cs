

namespace firmware_upgrade.Helpers
{
    public static class BLE
    {

        public static ushort CalculateChecksum(byte[] buffer)
        {
            ushort sum = 0;

            foreach (byte b in buffer)
                sum += b;

            ushort crc = (ushort)((65535 - sum) + 1);

            return crc;
        }


            public static ushort CalculateCRC16(byte[] message, ushort initialCrc = 0x8005, ushort poly = 0x1021)
            {
                ushort crc = initialCrc;

                foreach (byte b in message)
                {
                    // Move byte into the upper 8 bits
                    crc ^= (ushort)(b << 8);

                    // Process each bit in the byte
                    for (int i = 0; i < 8; i++)
                    {
                        if ((crc & 0x8000) != 0) // if the top bit is set
                        {
                            crc = (ushort)((crc << 1) ^ poly);
                        }
                        else
                        {
                            crc <<= 1;
                        }

                        // Keep crc 16-bit
                        crc &= 0xFFFF;
                    }
                }

                return crc;
            }
        


    }
}
