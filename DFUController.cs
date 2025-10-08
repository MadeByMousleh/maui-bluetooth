using System;
using System.Collections.Generic;

public class DFUController
{
    public event Action<string, object> Progress;
    private readonly Action<string, object> _writeMethod;
    private readonly string _payload;
    private readonly string _securityId;
    private readonly object _bleDevice;
    private readonly PayloadProcessor _processor;
    private readonly List<FlashRow> _rows;

    private int _currentStep = 1;
    private int _rowIndex = 0;
    private int _commandIndex = 0;

    public DFUController(string payload, string securityId, Action<string, object> writeMethod, object bleDevice)
    {
        _securityId = securityId;
        _payload = payload;
        _writeMethod = writeMethod;
        _bleDevice = bleDevice;
        _processor = new PayloadProcessor(_payload);
        //_rows = _processor.GetFlashDataLines();
    }

    private List<string> SplitRowIntoChunks(string row)
    {
        var chunks = new List<string>();
        if (row.Length > 0)
            chunks.Add(row.Substring(0, Math.Min(256, row.Length)));
        if (row.Length > 256)
            chunks.Add(row.Substring(256, Math.Min(256, row.Length - 256)));
        return chunks;
    }

    private string ProcessRow(FlashRow row)
    {
        var packetGen = new BootLoaderPacketGen();
        var chunks = SplitRowIntoChunks(BitConverter.ToString(row.Data).Replace("-", ""));
        int currentCommand = _commandIndex;

        if (_rowIndex == _rows.Count && _commandIndex == 3)
        {
            _commandIndex = 0;
            _rowIndex++;
            _currentStep = 4;
            return packetGen.CreateProgramRowPacket(
                row.RowNumber.ToString("x4"),
                row.ArrayID,
                chunks[currentCommand]
            );
        }

        if (currentCommand == 1)
        {
            _commandIndex = 0;
            _rowIndex++;
            return packetGen.CreateProgramRowPacket(
                row.RowNumber.ToString("x4"),
                row.ArrayID,
                chunks[currentCommand]
            );
        }

        _commandIndex++;
        return packetGen.CreateSendDataPacket(chunks[currentCommand]);
    }

    private void EnterBootLoader()
    {
        var packetGen = new BootLoaderPacketGen();
        var packet = packetGen.EnterBootLoader();
        _writeMethod(packet, _bleDevice);
        _currentStep = 2;
    }

    private string GetFlashSizePacket()
    {
        var packetGen = new BootLoaderPacketGen();
        var packet = packetGen.CreateEnterBootLoaderPacket(); // Or .GetFlashSize() if implemented
        _currentStep = 3;
        return packet;
    }

    private string GetVerifyChecksum()
    {
        var packetGen = new BootLoaderPacketGen();
        // Implement GetVerifyChecksum in BootLoaderPacketGen if needed
        var packet = ""; // packetGen.GetVerifyChecksum();
        _currentStep = 5;
        return packet;
    }

    private string ExitBootLoader()
    {
        var packetGen = new BootLoaderPacketGen();
        // Implement GetExitBootLoader in BootLoaderPacketGen if needed
        var packet = ""; // packetGen.GetExitBootLoader();
        _currentStep = 6;
        return packet;
    }

    public string GetPacketToSend()
    {
        int step = _currentStep;

        if (step == 2)
        {
            return GetFlashSizePacket();
        }

        if (step == 3)
        {
            if (_rowIndex < _rows.Count)
            {
                Progress?.Invoke("progress", new { mac = _bleDevice, progress = (int)((_rowIndex / (double)_rows.Count) * 100) });
                var row = _rows[_rowIndex];
                if (row != null)
                {
                    var packet = ProcessRow(row);
                    if (packet != null)
                        return packet;
                }
                return GetVerifyChecksum();
            }
        }

        if (step == 4)
        {
            return GetVerifyChecksum();
        }

        if (step == 5)
        {
            return ExitBootLoader();
        }

        if (step == 6)
        {
            // DFU COMPLETE
        }

        return null;
    }

    public void StartDFU()
    {
        EnterBootLoader();
    }

    public void OnResponse(byte[] response)
    {
        var responseHandler = new ResponseHandler();
        var isAccepted = responseHandler.HandleResponse(response);

        if (isAccepted)
        {
            var packet = GetPacketToSend()?.ToUpperInvariant();
            if (packet != null)
                _writeMethod(packet, _bleDevice);
        }
    }

    public void OnProgress(Action<string, object> callback)
    {
        Progress += callback;
    }
}