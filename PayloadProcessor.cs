using System;
using System.Collections.Generic;
using System.Linq;

public class PayloadProcessor
{
    private string _header;
    private string _siliconId;
    private string _siliconRev;
    private string _checkSumType;
    private List<FlashRow> _flashDataLines;
    private string _securityId;
    private string _payload;
    private string _file;

    public PayloadProcessor(string cypressPayload, string securityId)
    {
        _securityId = securityId;
        _payload = cypressPayload;
        _file = _payload.Trim(); // Remove surrounding whitespace
        _header = GetHeader();
        _siliconId = GetSiliconId();
        _siliconRev = GetSiliconRev();
        _checkSumType = GetChecksumType();
        _flashDataLines = GetFlashDataLines();
    }

    // Private method to read data lines and parse them into FlashRow objects
    private List<FlashRow> ReadDataLines()
    {
        var lines = _file.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var linesArr = new List<FlashRow>();

        // For each line (except the header)
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line) || line.Length < 13) // minimal valid line length
                continue;

            // [1-byte ArrayID][2-byte RowNumber][2-byte DataLength][N-byte Data][1byte Checksum]
            byte arrayID = Convert.ToByte(line.Substring(1, 2), 16);
            ushort rowNumber = Convert.ToUInt16(line.Substring(3, 4), 16);
            ushort dataLength = Convert.ToUInt16(line.Substring(7, 4), 16);
            string dataHex = line.Substring(11, line.Length - 13);
            byte[] data = Enumerable.Range(0, dataHex.Length / 2)
                .Select(x => Convert.ToByte(dataHex.Substring(x * 2, 2), 16))
                .ToArray();
            byte checksum = Convert.ToByte(line.Substring(line.Length - 2, 2), 16);

            var model = new FlashRow(arrayID, rowNumber, dataLength, data, checksum);
            linesArr.Add(model);
        }

        return linesArr;
    }

    // Getters
    public string GetSecurityId() => _securityId;

    public List<FlashRow> GetFlashDataLines()
    {
        if (_flashDataLines == null)
            _flashDataLines = ReadDataLines();
        return _flashDataLines;
    }

    public string GetHeader()
    {
        if (_header == null)
            _header = _file.Substring(0, 12);
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