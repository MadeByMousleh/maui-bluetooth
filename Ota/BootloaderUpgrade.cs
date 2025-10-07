
using Plugin.BLE.Abstractions.Contracts;

namespace firmware_upgrade.Ota
{
    // This class expects the detecit to be in device mode before doing the upgrade
    // This class also will not finish the upgrade of the device. The sensor needs tobe upgraded right after. 
    public class BootloaderUpgrade
    {

        string FilePath { get; set; }
        bool SecureUpgrade { get; set; }


        public int RowsToBeProgrammed { get; private set; }
        public List<byte[]>? Rows { get; private set; }
        public int RowReachedCount { get; private set; }


        public event EventHandler<int>? RowReachedChanged;

        PayloadProcessor payloadProcessor;

        private readonly Func<ICharacteristic, byte[], Task> writeMethod;

        private ICharacteristic Characteristic { get; set; }
        private BootloaderUpgrade(string filePath, bool secureUpgrade, Func<ICharacteristic, byte[], Task> writeMethod, ICharacteristic characteristic)
        {
            FilePath = filePath;
            SecureUpgrade = secureUpgrade;
            this.writeMethod = writeMethod;
            payloadProcessor = new PayloadProcessor(FilePath);
            RowReachedCount = 0;
            Characteristic = characteristic;

        }


        public static async Task<BootloaderUpgrade> Init(string filePath, bool secureUpgrade, Func<ICharacteristic, byte[], Task> writeMethod, ICharacteristic characteristic)
        {
            var instance = new BootloaderUpgrade(filePath, secureUpgrade, writeMethod, characteristic);

            // Read firmware and set RowsToBeProgrammed
            List<byte[]> flashRows = await instance.payloadProcessor.GetFirmwareFlashPackets();
            instance.RowsToBeProgrammed = flashRows.Count;
            instance.Rows = flashRows;

            return instance;
        }

        public async Task<bool> Upgrade()
        {

            List<byte[]> flashRows = await payloadProcessor.GetFirmwareFlashPackets();

            flashRows.Add(EnterBootLoader());
            flashRows.Add(GetFlashSize());

            for (int i = 0; i < flashRows.Count - 1; i++)
            {
                var rowPacket = flashRows[i];
                await writeMethod(Characteristic, rowPacket);

                // UI update on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    RowReachedCount++;
                    RowReachedChanged?.Invoke(this, RowReachedCount);

                });

                Console.WriteLine($"ROW {i + 1}/{flashRows.Count} SENT AND ACKNOWLEDGED");
            }

            Console.WriteLine("✅ DFU completed successfully.");
            return false;
        }

        private byte[] GetFlashSize()
        {
            return new byte[]
            {
                0x01, 0x32, 0x06, 0x01,
                0x00, 0x00, 0xCC, 0xFF,
                0x17
            };
        }

        private byte[] EnterBootLoader()
        {
            return new byte[]
            {
                0x01, 0x38, 0x06, 0x00,
                0x49, 0xA1, 0x34, 0xB6,
                0xC7, 0x79, 0xAD, 0xFC, 0x17
            };

        }


    }
}
