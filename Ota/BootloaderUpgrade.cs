using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using firmware_upgrade.BLE;
using Microsoft.Maui.Dispatching;

namespace firmware_upgrade.Ota
{
    public class BootloaderUpgrade
    {

        public bool IsSecureUpgrade { get; private set; }

        public bool IsActor;

        public string FilePath { get; private set; }
        public int RowsToBeProgrammed { get; private set; }
        public int RowReachedCount { get; private set; }

        public int MTUSize;


        private byte[] SecurityId;

        private List<byte[]>? flashRows;

        private PayloadProcessor payloadProcessor;
        
        public event EventHandler<int>? OnProgressChanged;

        private readonly ResponseHandler responseHandler = new();

        public event EventHandler<byte[]>? OnDataToWrite;

        private BootloaderUpgrade(string filePath, bool isSecureUpgrade, byte[] securityId, int mtuSize, bool isActor)
        {
            FilePath = filePath;
            IsSecureUpgrade = isSecureUpgrade;
            payloadProcessor = new PayloadProcessor(FilePath, isSecureUpgrade, securityId, mtuSize, isActor);
            SecurityId = securityId;
            MTUSize = mtuSize;
            IsActor = isActor;
        }

        private int CurrentRowIndex;

        public static async Task<BootloaderUpgrade> CreateAsync(string filePath, byte[] securityId, int mtuSize = 256, bool isSecureUpgrade = false, bool isActor = false)
        {
            var instance = new BootloaderUpgrade(filePath, isSecureUpgrade, securityId, mtuSize, isActor);

            instance.flashRows = await instance.payloadProcessor.GetFirmwareFlashPackets();

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


    }
}
