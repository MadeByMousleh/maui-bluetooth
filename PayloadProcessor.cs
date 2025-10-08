using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class PayloadProcessor
{
    private string _header;
    private string _siliconId;
    private string _siliconRev;
    private string _checkSumType;
    private List<FlashRow> _flashDataLines;
    private byte[] _securityId = [0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79];
    private string _payload;
    private string _relativePath;

    public PayloadProcessor(string cypressPayload)
    {
        _payload = cypressPayload;
        _relativePath = _payload.Trim(); // Remove surrounding whitespace
        _header = GetHeader();
        _siliconId = GetSiliconId();
        _siliconRev = GetSiliconRev();
        _checkSumType = GetChecksumType();
    }


    private static byte[] ConvertHexStringToBytes(string hex)
    {
        int len = hex.Length / 2;
        var bytes = new byte[len];
        for (int i = 0; i < len; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber);
        }
        return bytes;
    }
    // Private method to read data lines and parse them into FlashRow objects



    // SEND DATA PACKET (0x37)
    private byte[] CreateSendDataPacket(byte[] data)
    {
        byte startCommand = 0x01;
        byte command = 0x37;
        byte endCommand = 0x17;

        ushort dataLength = (ushort)data.Length;

        byte[] packetBeforeChecksum = new byte[1 + 1 + 2 + data.Length];
        int offset = 0;
        packetBeforeChecksum[offset++] = startCommand;
        packetBeforeChecksum[offset++] = command;
        packetBeforeChecksum[offset++] = (byte)(dataLength & 0xFF);
        packetBeforeChecksum[offset++] = (byte)(dataLength >> 8);
        Array.Copy(data, 0, packetBeforeChecksum, offset, data.Length);

        ushort checksum = CalculateChecksum(packetBeforeChecksum);

        return packetBeforeChecksum
            .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endCommand })
            .ToArray();
    }

    // PROGRAM ROW PACKET (0x39)
    private byte[] CreateProgramRowPacket(byte arrayId, ushort rowNumber, byte[] data)
    {
        byte startCommand = 0x01;
        byte command = 0x39;
        byte endCommand = 0x17;

        ushort dataLength = (ushort)(data.Length + 3); // arrayID(1) + rowNumber(2)

        byte[] packetBeforeChecksum = new byte[1 + 1 + 2 + 1 + 2 + data.Length];
        int offset = 0;
        packetBeforeChecksum[offset++] = startCommand;
        packetBeforeChecksum[offset++] = command;
        packetBeforeChecksum[offset++] = (byte)(dataLength & 0xFF);
        packetBeforeChecksum[offset++] = (byte)(dataLength >> 8);
        packetBeforeChecksum[offset++] = arrayId;
        packetBeforeChecksum[offset++] = (byte)(rowNumber & 0xFF);
        packetBeforeChecksum[offset++] = (byte)(rowNumber >> 8);
        Array.Copy(data, 0, packetBeforeChecksum, offset, data.Length);

        ushort checksum = CalculateChecksum(packetBeforeChecksum);

        return packetBeforeChecksum
            .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endCommand })
            .ToArray();
    }


    public async Task<List<FlashRow>> ReadFlashRows()
    {
        
            // Use the relative path under Raw/
            using var stream = await FileSystem.OpenAppPackageFileAsync(_relativePath);
            using var reader = new StreamReader(stream);

            var rows = new List<FlashRow>();

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || line.Length < 13 || !line.StartsWith(":"))
                    continue;

                try
                {
                    byte arrayID = Convert.ToByte(line.Substring(1, 2), 16);
                    ushort rowNumber = Convert.ToUInt16(line.Substring(3, 4), 16);
                    ushort dataLength = Convert.ToUInt16(line.Substring(7, 4), 16);
                    string dataHex = line.Substring(11, line.Length - 13);
                    byte[] data = Enumerable.Range(0, dataHex.Length / 2)
                        .Select(x => Convert.ToByte(dataHex.Substring(x * 2, 2), 16))
                        .ToArray();
                    byte checksum = Convert.ToByte(line.Substring(line.Length - 2, 2), 16);

                BootLoaderPacketGen packetGen = new BootLoaderPacketGen();

                rows.Add(new FlashRow(arrayID, rowNumber, dataLength, data, checksum));
                }
                catch
                {
                    continue;
                }
            }

            return rows;
       
    }


    // MAIN COMBINER
    public async Task<List<byte[]>> GetFirmwareFlashPackets()
    {
        List<FlashRow> flashRows = await ReadFlashRows();
        List<byte[]> allPackets = new List<byte[]>();

        foreach (var row in flashRows)
        {
            byte[] data = row.Data;
            int mid = data.Length;
            //byte[] firstHalf = data.Take(mid).ToArray();
            //byte[] secondHalf = data.Skip(mid).ToArray();


            // SEND DATA
            //allPackets.Add(CreateSendDataPacket(firstHalf));

            // PROGRAM ROW
            allPackets.Add(CreateProgramRowPacket(row.ArrayID, row.RowNumber, data));

            // VERIFY ROW
            //allPackets.Add(CreateVerifyRowPacket(row.ArrayID, row.RowNumber));
        }

        // EXIT BOOTLOADER
        allPackets.Add(CreateExitBootloader());

        return allPackets;
    }

    private byte[] CreateExitBootloader()
    {
        byte[] packet = new byte[6];
        packet[0] = 0x01;
        packet[1] = 0x3B;
        packet[2] = 0x00;
        packet[3] = 0x00;
        packet[4] = 0xC4;
        packet[5] = 0x17;
        return packet;
    }

    private byte[] CreateVerifyRowPacket(byte arrayId, ushort rowNumber)
    {
        byte startPacket = 0x01;
        byte command = 0x3A;
        byte endPacket = 0x17;

        // Length is 3 (arrayId + rowNumber(2)), so data length = 3
        byte[] packetBeforeChecksum = new byte[1 + 1 + 2 + 1 + 2];
        int offset = 0;
        packetBeforeChecksum[offset++] = startPacket;
        packetBeforeChecksum[offset++] = command;
        packetBeforeChecksum[offset++] = 0x03; // length LSB
        packetBeforeChecksum[offset++] = 0x00; // length MSB
        packetBeforeChecksum[offset++] = arrayId;
        packetBeforeChecksum[offset++] = (byte)(rowNumber & 0xFF);
        packetBeforeChecksum[offset++] = (byte)(rowNumber >> 8);

        ushort checksum = CalculateChecksum(packetBeforeChecksum);

        // Final packet: [packetBeforeChecksum][checksum LSB][checksum MSB][endPacket]
        return packetBeforeChecksum
            .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endPacket })
            .ToArray();
    }

    private ushort CalculateChecksum(byte[] buffer)
    {
        ushort sum = 0;

        foreach (byte b in buffer)
            sum += b;

        ushort crc = (ushort)((65535 - sum) + 1);

        return crc;
    }


    // Getters
    public byte[] GetSecurityId() => _securityId;

    public async Task<List<FlashRow>> GetFlashDataLines()
    {
        if (_flashDataLines == null)
            _flashDataLines = await ReadFlashRows();
        return _flashDataLines;
    }

    public string GetHeader()
    {
        if (_header == null)
            _header = _relativePath.Substring(0, 12);
        return _header;
    }

    public string GetSiliconId()
    {
        if (_siliconId == null)
            _siliconId = GetHeader().Substring(0, 8);
        return _siliconId;
    }

    public string GetSiliconRev()
    {
        if (_siliconRev == null)
            _siliconRev = GetHeader().Substring(8, 2);
        return _siliconRev;
    }

    public string GetChecksumType()
    {
        if (_checkSumType == null)
            _checkSumType = GetHeader().Substring(10, 2);
        return _checkSumType;
    }
}