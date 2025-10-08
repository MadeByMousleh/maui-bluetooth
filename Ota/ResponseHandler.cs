using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


    namespace firmware_upgrade.Ota
    {
        public class ResponseHandler
        {
            private static readonly Dictionary<byte, string> ErrorCodes = new()
        {
            { 0x00, "The data sent is accepted" },
            { 0x01, "The provided key does not match the value" },
            { 0x02, "The verification of the flash failed" },
            { 0x03, "The amount of data available is outside the expected range" },
            { 0x04, "The data is not in proper form" },
            { 0x05, "The command is not recognized" },
            { 0x06, "The expected device does not match the detected device" },
            { 0x07, "The boot loader version detected is not supported" },
            { 0x08, "The checksum does not match the expected value" },
            { 0x09, "The flash array is not valid" },
            { 0x0A, "The flash row is not valid" },
            { 0x0B, "The flash row is protected and cannot be set as active" },
            { 0x0D, "The application is currently marked as active" },
            { 0x0E, "The callback function returns invalid data" },
            { 0x0F, "An unknown error occurred" },
        };

            public bool HandleResponse(byte[] response)
            {
                if (response == null || response.Length < 2)
                {
                    Console.WriteLine("⚠️ Invalid response received (too short)");
                    return false;
                }

                byte errorCode = response[1];

                if (errorCode != 0x00)
                {
                    Console.WriteLine($"❌ ERROR: {ErrorCodes.GetValueOrDefault(errorCode, "Unknown")} - Response: {BitConverter.ToString(response)}");
                    return false;
                }

                Console.WriteLine($"✅ SUCCESS: {ErrorCodes[0x00]} - Response: {BitConverter.ToString(response)}");
                return true;
            }
        }
    }

