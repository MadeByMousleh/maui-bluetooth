using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

public class BootLoaderPacketGen
{
    // Private variables
    private const byte StartOfPacket = 0x01;
    private const byte EndOfPacket = 0x17;
    private readonly byte[] SecurityId = { 0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79 };
    private const byte EnterBootLoaderCommand = 0x38;

    // Sizes of each part of the boot-loader packet structure
    private const int StartOfPacketByteSize = 1;
    private const int CommandByteSize = 1;
    private const int DataLengthByteSize = 2;
    private const int ChecksumByteSize = 2;
    private const int EndOfPacketLength = 1;

    public BootLoaderPacketGen() { }

    // Calculates the message's CRC16 (simple sum-based, not CCITT)
    public ushort CalcChecksum(byte[] message)
    {
        int sum = 0;
        for (int i = 0; i < message.Length; i++)
            sum += message[i];

        int crc = (1 + (~sum & 0xFFFF)) & 0xFFFF;
        if (crc > 65535) crc = crc - 65535 - 1;
        return (ushort)crc;
    }

    // CRC16-CCITT (reverse, as in JS)
    public ushort CalcCrc16(byte[] message)
    {
        ushort crc = 0xFFFF;
        if (message.Length == 0) return (ushort)~crc;

        foreach (var b in message)
        {
            int tmp = 0x00FF & b;
            for (int j = 0; j < 8; j++, tmp >>= 1)
            {
                if (((crc & 0x0001) ^ (tmp & 0x0001)) != 0)
                    crc = (ushort)((crc >> 1) ^ 0x8408);
                else
                    crc >>= 1;
            }
        }
        crc = (ushort)~crc;
        ushort tmp2 = crc;
        crc = (ushort)((crc << 8) | ((tmp2 >> 8) & 0xFF));
        return crc;
    }

    // Swap 16-bit value
    public ushort Swap16(ushort val)
    {
        return (ushort)(((val & 0xFF) << 8) | ((val >> 8) & 0xFF));
    }

    // Swap 4-char hex string (e.g. "1234" -> "3412")
    public string Swap16String(string hex)
    {
        if (hex.Length != 4) throw new ArgumentException("Hex string must be 4 characters.");
        return $"{hex[2]}{hex[3]}{hex[0]}{hex[1]}";
    }

    // Convert int to hex string, padded
    public string D2H(int d)
    {
        var h = d.ToString("x");
        return h.Length % 2 == 1 ? "0" + h : h;
    }

    // Calculate data length as swapped hex buffer
    public byte[] CalculateDataLength(int byteLength)
    {
        string result = byteLength.ToString("x4");
        result = Swap16String(result);
        return Enumerable.Range(0, result.Length / 2)
            .Select(i => byte.Parse(result.Substring(i * 2, 2), NumberStyles.HexNumber))
            .ToArray();
    }

    // Calculate checksum for a packet
    public byte[] CalculateChecksum(byte command, byte[] dataLengthBuffer, byte[] dataBuffer)
    {
        var checkSumBuffer = new List<byte> { StartOfPacket, command };
        checkSumBuffer.AddRange(dataLengthBuffer);
        checkSumBuffer.AddRange(dataBuffer);

        ushort checksum = CalcChecksum(checkSumBuffer.ToArray());
        string swapped = Swap16String(checksum.ToString("x4"));
        return Enumerable.Range(0, swapped.Length / 2)
            .Select(i => byte.Parse(swapped.Substring(i * 2, 2), NumberStyles.HexNumber))
            .ToArray();
    }

    // Calculate checksum from string array (hex)
    public string CalculateChecksumString(List<string> command)
    {
        var bytes = command.Select(s => byte.Parse(s, NumberStyles.HexNumber)).ToArray();
        ushort checksum = CalcChecksum(bytes);
        string swapped = Swap16String(checksum.ToString("x4"));
        return BitConverter.ToString(Enumerable.Range(0, swapped.Length / 2)
            .Select(i => byte.Parse(swapped.Substring(i * 2, 2), NumberStyles.HexNumber))
            .ToArray()).Replace("-", "").ToLower();
    }

    // Create Enter BootLoader Packet (returns hex string)
    public string CreateEnterBootLoaderPacket()
    {
        var dataBuffer = SecurityId;
        var length = CalculateDataLength(dataBuffer.Length);
        var checksum = CalculateChecksum(EnterBootLoaderCommand, length, dataBuffer);

        var buffer = new List<byte> { StartOfPacket, EnterBootLoaderCommand };
        buffer.AddRange(length);
        buffer.AddRange(dataBuffer);
        buffer.AddRange(checksum);
        buffer.Add(EndOfPacket);

        return BitConverter.ToString(buffer.ToArray()).Replace("-", "").ToLower();
    }

    // Create Send Data Packet (returns hex string)
    public string CreateSendDataPacket(string dataHex)
    {
        byte command = 0x37;
        var dataBuffer = Enumerable.Range(0, dataHex.Length / 2)
            .Select(i => byte.Parse(dataHex.Substring(i * 2, 2), NumberStyles.HexNumber))
            .ToArray();
        var length = CalculateDataLength(dataBuffer.Length);
        var checksum = CalculateChecksum(command, length, dataBuffer);

        var buffer = new List<byte> { StartOfPacket, command };
        buffer.AddRange(length);
        buffer.AddRange(dataBuffer);
        buffer.AddRange(checksum);
        buffer.Add(EndOfPacket);

        return BitConverter.ToString(buffer.ToArray()).Replace("-", "").ToLower();
    }

    // Create Program Row Packet (returns hex string)
    public string CreateProgramRowPacket(string rowHex, byte arrayId, string dataHex)
    {
        byte command = 0x39;
        var dataBuffer = Enumerable.Range(0, dataHex.Length / 2)
            .Select(i => byte.Parse(dataHex.Substring(i * 2, 2), NumberStyles.HexNumber))
            .ToArray();
        var length = CalculateDataLength(dataBuffer.Length);
        var checksum = CalculateChecksum(command, length, dataBuffer);

        var rowBuffer = Enumerable.Range(0, rowHex.Length / 2)
            .Select(i => byte.Parse(rowHex.Substring(i * 2, 2), NumberStyles.HexNumber))
            .ToArray();

        var buffer = new List<byte> { StartOfPacket, command, arrayId };
        buffer.AddRange(rowBuffer);
        buffer.AddRange(length);
        buffer.AddRange(dataBuffer);
        buffer.AddRange(checksum);
        buffer.Add(EndOfPacket);

        return BitConverter.ToString(buffer.ToArray()).Replace("-", "").ToLower();
    }

    // Combine array (combine every two elements)
    public List<string> CombineArray(List<string> originalArray)
    {
        var combinedArray = new List<string>();
        for (int i = 0; i < originalArray.Count; i += 2)
        {
            if (i + 1 < originalArray.Count)
                combinedArray.Add(originalArray[i] + originalArray[i + 1]);
            else
                combinedArray.Add(originalArray[i]);
        }
        return combinedArray;
    }

    // Enter BootLoader (returns hex string)
    public string EnterBootLoader(string securityIdHex = null)
    {
        var packet = new List<string>();
        if (string.IsNullOrEmpty(securityIdHex))
        {
            packet.Add("01");
            packet.Add("38");
            packet.Add("00");
            packet.Add("00");
            packet.Add("C7");
            packet.Add("FF");
            packet.Add("17");
        }
        else
        {
            var securityIdArr = Enumerable.Range(0, securityIdHex.Length / 2)
                .Select(i => securityIdHex.Substring(i * 2, 2)).ToList();

            packet.Add("01");
            packet.Add("38");
            string dataLength = securityIdArr.Count.ToString("x4");
            dataLength = Swap16String(dataLength);
            packet.Add(dataLength.Substring(0, 2));
            packet.Add(dataLength.Substring(2, 2));
            packet.AddRange(securityIdArr);
            string checksum = CalculateChecksumString(packet);
            packet.Add(checksum.Substring(0, 2));
            packet.Add(checksum.Substring(2, 2));
            packet.Add("17");
        }
        return string.Join("", packet).ToLower();
    }

    // Additional packet methods (getFlashSize, sendDataPacket, writeRowPacket, etc.) can be implemented similarly as needed.
}

var bootGen = new BootLoaderPacketGen();
string packet = bootGen.CreateEnterBootLoaderPacket();