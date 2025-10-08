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

        private List<byte[]> flashRows;

        public int RowsToBeProgrammed { get; private set; }
        public int RowReachedCount { get; private set; }

        private PayloadProcessor payloadProcessor;

        /// <summary>
        /// Called when a packet is ready to be written.
        /// The callback must return true when the packet was acknowledged (ACK received).
        /// </summary>
        public event Func<byte[], Task<bool>>? OnDataToWriteRequest;

        /// <summary>
        /// Fired when a new row has been successfully acknowledged.
        /// </summary>
        public event EventHandler<int>? OnProgressChanged;


        private readonly ResponseHandler responseHandler = new();

        public event EventHandler<byte[]>? OnDataToWrite;


        private BootloaderUpgrade(string filePath, bool secureUpgrade)
        {
            FilePath = filePath;
            SecureUpgrade = secureUpgrade;
            payloadProcessor = new PayloadProcessor(FilePath);
        }

        private int CurrentRowIndex;

        public static async Task<BootloaderUpgrade> CreateAsync(string filePath, bool secureUpgrade)
        {
            var instance = new BootloaderUpgrade(filePath, secureUpgrade);
           
            List<byte[]> rows = new List<byte[]>();

            instance.flashRows = new List<byte[]>();

            rows.Add(GetEnterBootloaderPacket());
            rows.Add(GetFlashSizePacket());

            instance.flashRows = await instance.payloadProcessor.GetFirmwareFlashPackets();
            rows.AddRange(instance.flashRows);
            instance.flashRows = rows;
            instance.RowsToBeProgrammed = instance.flashRows.Count;
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

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    OnProgressChanged?.Invoke(this, RowReachedCount);
                });

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

            OnDataToWrite?.Invoke(this, rowPacket);


            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                OnProgressChanged?.Invoke(this, RowReachedCount);
            });

            Console.WriteLine($"✅ Row {RowReachedCount}/{RowsToBeProgrammed} acknowledged");

        }



        public static byte[] GetEnterBootloaderPacket() => new byte[]
        {
            0x01, 0x38, 0x06, 0x00,
            0x49, 0xA1, 0x34, 0xB6,
            0xC7, 0x79, 0xAD, 0xFC, 0x17
        };

        public static byte[] GetFlashSizePacket() => new byte[]
        {
            0x01, 0x32, 0x06, 0x01,
            0x00, 0x00, 0xCC, 0xFF,
            0x17
        };
    }
}
