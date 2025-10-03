using System;
using System.Collections.Generic;
using System.Diagnostics;

public class ResponseHandler
{
    private readonly Dictionary<string, string> ErrorCodes = new()
    {
        { "00", "The data send is accepted" },
        { "01", "The provided key does not match the value" },
        { "02", "The verification of the flash failed" },
        { "03", "The amount of data available is outside the expected range" },
        { "04", "The data is not in proper form" },
        { "05", "The command is not recognized" },
        { "06", "The expected device does not match the detected device" },
        { "07", "The boot loader version detected is not supported" },
        { "08", "The checksum does not match the expected value" },
        { "09", "The flash array is not valid" },
        { "0a", "The flash row is not valid" },
        { "0b", "The flash row is protected and cannot be set as active" },
        { "0d", "The application is currently marked as active" },
        { "0e", "The callback function returns invalid data" },
        { "0f", "An unknown error ocurred" }
    };

    public ResponseHandler()
    {
    }

    public bool HandleResponse(byte[] response)
    {
        if (response == null || response.Length < 2)
        {
            Debug.WriteLine("ERROR: Invalid response format.");
            return false;
        }

        string errorCode = response[1].ToString("x2");
        string hexResponse = BitConverter.ToString(response).Replace("-", "").ToLower();

        if (errorCode != "00")
        {
            Debug.WriteLine($"ERROR: {ErrorCodes.GetValueOrDefault(errorCode, "Unknown error code")} - RESPONSE RECEIVED: {hexResponse}");
            return false;
        }
        Debug.WriteLine($"SUCCESS: {ErrorCodes[errorCode]} - RESPONSE RECEIVED: {hexResponse}");
        return true;
    }
}