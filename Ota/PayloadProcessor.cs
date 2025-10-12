using firmware_upgrade.Helpers;
using Microsoft.Maui.Controls;
using System.Globalization;


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
    private bool _secureFlash;
    private int _packetSize;

    private bool _isActor;

    public PayloadProcessor(string cypressPayload, bool secureFlash, byte[] securityId, int packetSize = 256, bool isActor = false)
    {
        _payload = cypressPayload;
        _relativePath = _payload.Trim(); // Remove surrounding whitespace
        _header = GetHeader();
        _siliconId = GetSiliconId();
        _siliconRev = GetSiliconRev();
        _checkSumType = GetChecksumType();
        _secureFlash = secureFlash;
        _packetSize = packetSize;
        _securityId = securityId;
        _isActor = isActor;
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

        byte[] finalPacket = packetBeforeChecksum
            .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endCommand })
            .ToArray();

        if (_isActor)
        {
            return ConvertToActorPacket(finalPacket);
        }
        return finalPacket;
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

        byte[] finalPacket = packetBeforeChecksum
           .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endCommand })
           .ToArray();

        if (_isActor)
        {
            return ConvertToActorPacket(finalPacket);
        }
        return finalPacket;
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


    public async Task<List<byte[]>> GetFirmwareFlashPackets()
    {
        List<FlashRow> flashRows = await ReadFlashRows();
        List<byte[]> allPackets = new List<byte[]>();

        allPackets.Add(CreateEnterBootLoader());
        allPackets.Add(GetFlashSizePacket());

        foreach (var row in flashRows)
        {
            byte[] data = row.Data;
            int total = row.DataLength;
            int fullChunks = total / _packetSize;
            int remaining = total % _packetSize;

            // Case 1: Entire array smaller than one packet
            if (_packetSize >= total)
            {
                allPackets.Add(CreateProgramRowPacket(row.ArrayID, row.RowNumber, data));
                continue; // Move to next row
            }

            // Case 2: Send all full packets
            int offset = 0;
            for (int i = 0; i < fullChunks; i++)
            {
                var packetData = data.Skip(offset).Take(_packetSize).ToArray();
                allPackets.Add(CreateSendDataPacket(packetData));
                offset += _packetSize;
            }

            // Case 3: Handle remaining elements (if any)
            if (remaining > 0)
            {
                var lastPacket = data.Skip(offset).Take(remaining).ToArray();
                allPackets.Add(CreateProgramRowPacket(row.ArrayID, row.RowNumber, lastPacket));
            }
            else
            {
                var finalFullRow = data.Skip(offset - _packetSize).Take(_packetSize).ToArray();
                allPackets.Add(CreateProgramRowPacket(row.ArrayID, row.RowNumber, finalFullRow));
            }

            // Verify row if secure mode is on
            if (_secureFlash)
            {
                allPackets.Add(CreateVerifyRowPacket(row.ArrayID, row.RowNumber));
            }
        }

        // Final steps: Verify checksum and exit
        allPackets.Add(CreateVerifyChecksum());
        allPackets.Add(CreateExitBootloader());

        return allPackets;
    }


    private byte[] CreateEnterBootLoader()
    {


        if ((_securityId == null || _securityId.Length == 0) && _isActor == false)
        {
            return new byte[] { 0x01, 0x38, 0x00, 0x00, 0xC7, 0xFF, 0x17 };
        }

        if((_securityId == null || _securityId.Length == 0) && _isActor == true)
        {
            return ConvertToActorPacket(new byte[] { 0x01, 0x38, 0x00, 0x00, 0xC7, 0xFF, 0x17 });
        }

        byte startCommand = 0x01;
        byte command = 0x38;
        byte endCommand = 0x17;

        ushort dataLength = (ushort)_securityId.Length;

        byte[] packetBeforeChecksum = new byte[1 + 1 + 2 + _securityId.Length];
        int offset = 0;
        packetBeforeChecksum[offset++] = startCommand;
        packetBeforeChecksum[offset++] = command;
        packetBeforeChecksum[offset++] = (byte)(dataLength & 0xFF);
        packetBeforeChecksum[offset++] = (byte)(dataLength >> 8);
        Array.Copy(_securityId, 0, packetBeforeChecksum, offset, _securityId.Length);

        ushort checksum = CalculateChecksum(packetBeforeChecksum);

        byte[] finalPacket = packetBeforeChecksum
            .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endCommand })
            .ToArray();

        if (_isActor)
        {
            return ConvertToActorPacket(finalPacket);
        }
        return finalPacket;
    }

    private byte[] CreateExitBootloader()
    {

        if (_isActor)
        {
            return ConvertToActorPacket(new byte[] { 0x01, 0x3B, 0x00, 0x00, 0xC4, 0xFF, 0x17 });
        }

        return new byte[] { 0x01, 0x3B, 0x00, 0x00, 0xC4, 0xFF, 0x17 };

    }

    // Update all static methods to call ConvertToActorPacket as static
    private byte[] CreateVerifyChecksum()
    {
        byte[] packet = new byte[7] { 0x01, 0x31, 0x00, 0x00, 0xCE, 0xFF, 0x17 };

        if (_isActor)
        {
            return ConvertToActorPacket(packet);
        }

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
        byte[] finalPacket = packetBeforeChecksum
            .Concat(new byte[] { (byte)(checksum & 0xFF), (byte)(checksum >> 8), endPacket })
            .ToArray();

        if (_isActor)
        {
            return ConvertToActorPacket(finalPacket);
        }

        return finalPacket;
    }

    private ushort CalculateChecksum(byte[] buffer)
    {
        ushort sum = 0;

        foreach (byte b in buffer)
            sum += b;

        ushort crc = (ushort)((65535 - sum) + 1);

        return crc;
    }


    private byte[] GetFlashSizePacket()
    {
        byte[] packet = new byte[] { 0x01, 0x32, 0x01, 0x00, 0x00, 0xCC, 0xFF, 0x17 };

        if (_isActor)
        {
            return ConvertToActorPacket(packet);
        }
        return packet;
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

    // Change all calls to ConvertToActorPacket from static context to use an instance reference.
    // For static methods (e.g., CreateVerifyChecksum, GetFlashSizePacket), make ConvertToActorPacket static.

    private byte[] ConvertToActorPacket(byte[] data)
    {
        byte startCommand = 0x01;
        ushort telegramType = 0x0014;   // 2 bytes

        int offset = 0;

        // Header: start(1) + type(2) + length(2) + checksum(2)
        byte[] header = new byte[1 + 2 + 2 + 2];

        ushort totalLength = (ushort)(data.Length + header.Length); // header (1+2+2+2) + data + endCommand

        // --- Build header ---
        header[offset++] = startCommand;

        // telegramType (little endian)
        header[offset++] = (byte)(telegramType & 0xFF);
        header[offset++] = (byte)(telegramType >> 8);

        // totalLength (little endian)
        header[offset++] = (byte)(totalLength & 0xFF);
        header[offset++] = (byte)(totalLength >> 8);

        // Calculate CRC for header so far (before adding checksum itself)
        ushort checksum = BLE.CalculateCRC16(header.Take(offset).ToArray());

        // append checksum (little endian)
        header[offset++] = (byte)(checksum & 0xFF);
        header[offset++] = (byte)(checksum >> 8);

        // --- Build full packet ---
        byte[] packet = new byte[header.Length + data.Length];

        // Copy header
        Array.Copy(header, 0, packet, 0, header.Length);

        // Copy data after header
        Array.Copy(data, 0, packet, header.Length, data.Length);

        return packet;
    }
}