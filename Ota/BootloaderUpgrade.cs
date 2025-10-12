using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using firmware_upgrade.BLE;
using Microsoft.Maui.Dispatching;

namespace firmware_upgrade.Ota
{
    public class BootloaderUpgrade
    {
        public string FilePath { get; private set; }
        public bool SecureUpgrade { get; private set; }

        private List<byte[]>? flashRows;

        public int RowsToBeProgrammed { get; private set; }
        public int RowReachedCount { get; private set; }

        private PayloadProcessor payloadProcessor;

        /// <summary>
        /// Called when a packet is ready to be written.
        /// The callback must return true when the packet was acknowledged (ACK received).
        /// </summary>
        public event Func<byte[], Task<bool>>? OnDataToWriteRequest;

        
        public event EventHandler<int>? OnProgressChanged;


        private readonly ResponseHandler responseHandler = new();

        public event EventHandler<byte[]>? OnDataToWrite;

        private byte[] SecurityId = [0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79];

        public int MTUSize;
        private BootloaderUpgrade(string filePath, bool secureUpgrade, byte[] securityId, int mtuSize)
        {
            FilePath = filePath;
            SecureUpgrade = secureUpgrade;
            payloadProcessor = new PayloadProcessor(FilePath, false, SecurityId, mtuSize);
            SecurityId = securityId;
            MTUSize = mtuSize;
        }

        private int CurrentRowIndex;

        public static async Task<BootloaderUpgrade> CreateAsync(string filePath, byte[] securityId, int mtuSize = 256, bool secureUpgrade = false)
        {
            var instance = new BootloaderUpgrade(filePath, secureUpgrade, securityId, mtuSize);

            instance.flashRows = await instance.InitRows();

            instance.RowsToBeProgrammed = instance.flashRows.Count -1;
            instance.RowReachedCount = 0;
            instance.CurrentRowIndex = 0;
            return instance;
        }


        public async Task HandleResponse(byte[] response)
        {
            bool isAccepted = responseHandler.HandleResponse(response);

            if (isAccepted)
            {
                RowReachedCount++;
                CurrentRowIndex++;

                await SendNextRow();
            }
            else
            {
                Console.WriteLine("⚠️ DFU halted due to error response.");
            }
        }


        private async Task<List<byte[]>> InitRows()
        {

           return  await payloadProcessor.GetFirmwareFlashPackets();

        }
        public async Task StartDFU()
        {

            Console.WriteLine($"🔧 Starting DFU. Total rows: {RowsToBeProgrammed}");

            await SendNextRow();
        }


        private async Task SendNextRow()
        {
            if (CurrentRowIndex >= flashRows.Count)
            {
                Console.WriteLine("✅ DFU completed successfully.");
                return;
            }

            var rowPacket = flashRows[CurrentRowIndex];

            Console.WriteLine($"📦 Sending Row {CurrentRowIndex}: {BitConverter.ToString(rowPacket)}");

            OnDataToWrite?.Invoke(this, rowPacket);

            int percent = (int)(((double)RowReachedCount / RowsToBeProgrammed) * 100);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {

                OnProgressChanged?.Invoke(this, percent);
            });

            Console.WriteLine($"✅ Row {RowReachedCount}/{RowsToBeProgrammed} acknowledged");

        }

        public byte[] GetEnterBootloaderPacket()
        {
            if (SecurityId == null || SecurityId.Length == 0)
            {
                return new byte[] { 0x01, 0x38, 0x00, 0x00, 0xC7, 0xFF, 0x17 };
            }

            ushort length = (ushort)SecurityId.Length;
            byte lengthLow = (byte)(length & 0xFF);
            byte lengthHigh = (byte)((length >> 8) & 0xFF);
            var header = new byte[] { 0x01, 0x38, lengthLow, lengthHigh };
            var footer = new byte[] { 0x17 };
            var result = new byte[header.Length + SecurityId.Length + footer.Length];

            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(SecurityId, 0, result, header.Length, SecurityId.Length);
            Buffer.BlockCopy(footer, 0, result, header.Length + SecurityId.Length, footer.Length);

            return result;


        }


    }
}
