using firmware_upgrade.BLE;
using firmware_upgrade.BLEComamnds.Interfaces;
using firmware_upgrade.BLEComamnds.JumpToBoot;
using firmware_upgrade.Ota;
using Microsoft.Maui.ApplicationModel;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Exceptions;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace firmware_upgrade
{

    public class BLEDevice : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Name { get; set; }

        public string Id => BaseDevice?.Id.ToString() ?? "Unknown";

        public IDevice BaseDevice { get; set; }

        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                if (_isConnected != value)
                {
                    _isConnected = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _upgradeProgress = 0;
        public int UpgradeProgress
        {
            get => _upgradeProgress;
            set
            {
                if (_upgradeProgress != value)
                {
                    _upgradeProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isUpgradeInProgress = false;
        public bool IsUpgradeInProgress
        {
            get => _isUpgradeInProgress;
            set
            {
                if (_isUpgradeInProgress != value)
                {
                    _isUpgradeInProgress = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ObservableCollection<BLEDevice> Devices { get; set; } = new();

        private BLEDevice _selectedDevice;
        public BLEDevice SelectedDevice
        {
            get => _selectedDevice;
            set
            {
                if (_selectedDevice != value)
                {
                    _selectedDevice = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _upgradeProgress;
        public int UpgradeProgress
        {
            get => _upgradeProgress;
            set
            {
                if (_upgradeProgress != value)
                {
                    _upgradeProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _rowsToBeProgrammed;
        public int RowsToBeProgrammed
        {
            get => _rowsToBeProgrammed;
            set
            {
                if (_rowsToBeProgrammed != value)
                {
                    _rowsToBeProgrammed = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _rowReachedCount;
        public int RowReachedCount
        {
            get => _rowReachedCount;
            set
            {
                if (_rowReachedCount != value)
                {
                    _rowReachedCount = value;
                    OnPropertyChanged();
                    if (RowsToBeProgrammed > 0)
                        UpgradeProgress = (_rowReachedCount * 100) / RowsToBeProgrammed;
                }
            }
        }

        public ICommand connectCommand;
        public ICommand ConnectCommand => new Command<BLEDevice>(OnConnectClicked);

        public ICommand OnStartBootloaderUpradeCommand => new Command<BLEDevice>(device => OnStartBootloaderUprade(device));

        public ICommand OnStartSensorUpradeCommand => new Command<BLEDevice>(device => OnStartSensorUprade(device));


        public ICommand GetDeviceInfoCommand => new Command<BLEDevice>(OnGetDeviceInfo);

        public ICommand GetSoftwareVersionCommand => new Command<BLEDevice>(OnGetSoftwareVersion);

        public ICommand GetDeviceDetailsCommand => new Command<BLEDevice>(OnGetDeviceDetails);

        public IBluetoothLE ble { get; set; }
        public IAdapter adapter { get; set; }

        private object _lock = new();

        TaskCompletionSource<byte[]> tcs = new TaskCompletionSource<byte[]>();



        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        // Menu functionality
        private void OnMenuButtonClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is BLEDevice device)
            {
                SelectedDevice = device;
                ShowMenu();
            }
        }

        private void ShowMenu()
        {
            MenuOverlay.IsVisible = true;
            MenuPopup.IsVisible = true;
        }

        private void HideMenu()
        {
            MenuOverlay.IsVisible = false;
            MenuPopup.IsVisible = false;
        }

        private void OnOverlayTapped(object sender, EventArgs e)
        {
            HideMenu();
        }

        private async void OnGetDeviceInfo(BLEDevice device)
        {
            HideMenu();
            await DisplayAlert("Device Info", $"Device: {device.Name}\nID: {device.Id}\nConnected: {device.IsConnected}", "OK");
        }

        private async void OnGetSoftwareVersion(BLEDevice device)
        {
            HideMenu();
            // TODO: Implement get software version functionality
            await DisplayAlert("Software Version", "Getting software version... (Not implemented yet)", "OK");
        }

        private async void OnGetDeviceDetails(BLEDevice device)
        {
            HideMenu();
            var details = $"Device Name: {device.Name}\n" +
                         $"Device ID: {device.Id}\n" +
                         $"Connection State: {device.BaseDevice?.State}\n" +
                         $"Is Connected: {device.IsConnected}";
            
            await DisplayAlert("Device Details", details, "OK");
        }

        private bool _isScanningStarted = false;

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            OnConnectionClicked();
            await CheckAndRequestBluetoothPermissions();

            ble = CrossBluetoothLE.Current;
            adapter = CrossBluetoothLE.Current.Adapter;

            ble.StateChanged -= OnBleStateChanged; // remove old handler just in case
            ble.StateChanged += OnBleStateChanged;

            if (!_isScanningStarted)
            {
                adapter.DeviceDiscovered -= OnDeviceDiscovered;
                adapter.DeviceDiscovered += OnDeviceDiscovered;

                var scanFilterOptions = new ScanFilterOptions
                {
                    DeviceAddresses = new[] { "10:B9:F7" } // android only filter
                };

                await adapter.StartScanningForDevicesAsync(scanFilterOptions);
                _isScanningStarted = true;
            }
        }

        private void OnBleStateChanged(object sender, BluetoothStateChangedArgs e)
        {
            Debug.WriteLine($"The bluetooth state changed to {e.NewState}");
        }

        private void OnDeviceDiscovered(object sender, DeviceEventArgs a)
        {
            var name = a.Device.Id.ToString().ToUpper();
            var last12 = name.Substring(Math.Max(0, name.Length - 12));

            BLEDevice device = new BLEDevice
            {
                Name = last12,
                BaseDevice = a.Device
            };

            if (device.Name.ToUpper().Contains("10B9F7"))
            {
                // avoid duplicates
                if (!Devices.Any(d => d.BaseDevice.Id == device.BaseDevice.Id))
                    Devices.Add(device);
            }

            Console.WriteLine("DEVICE:" + a.Device.Id);
        }

        private void OnConnectionClicked()
        {
#if ANDROID
            var enable = new Android.Content.Intent(Android.Bluetooth.BluetoothAdapter.ActionRequestEnable);
            enable.SetFlags(Android.Content.ActivityFlags.NewTask);

            var disable = new Android.Content.Intent(Android.Bluetooth.BluetoothAdapter.ActionRequestDiscoverable);
            disable.SetFlags(Android.Content.ActivityFlags.NewTask);

            var bluetoothManager = (Android.Bluetooth.BluetoothManager)Android.App.Application.Context.GetSystemService(Android.Content.Context.BluetoothService);
            var bluetoothAdapter = bluetoothManager.Adapter;

            if (bluetoothAdapter.IsEnabled == true)
            {
                Android.App.Application.Context.StartActivity(disable);
                // Disable the Bluetooth;
            }
            else
            {
                // Enable the Bluetooth
                Android.App.Application.Context.StartActivity(enable);
            }
#endif
        }

        private async Task<bool> CheckAndRequestBluetoothPermissions()
        {
            if (DeviceInfo.Platform != DevicePlatform.Android)
                return true;

            try
            {
                // For Android 12+ (API 31+), use Permissions.Bluetooth
                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    var bluetoothStatus = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
                    if (bluetoothStatus != PermissionStatus.Granted)
                    {
                        bluetoothStatus = await Permissions.RequestAsync<Permissions.Bluetooth>();
                        if (bluetoothStatus != PermissionStatus.Granted)
                            return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Permission Error", ex.Message, "OK");
                return false;
            }
        }

        private async void OnConnectClicked(BLEDevice device)
        {
            if (device.IsConnected)
            {
                await DisconnectFromDevice(device);
            }
            else
            {
                await ConnectToDevice(device);
            }
        }

        private async Task DisconnectFromDevice(BLEDevice device)
        {
            try
            {
                if (device.BaseDevice.State == DeviceState.Connected)
                {
                    await adapter.DisconnectDeviceAsync(device.BaseDevice);
                }
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    device.IsConnected = false;
                    device.IsUpgradeInProgress = false;
                    device.UpgradeProgress = 0;
                });

                Console.WriteLine($"✅ Disconnected from device: {device.Name}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"❌ Error disconnecting from device: {e.Message}");
                await DisplayAlert("Error", $"Failed to disconnect: {e.Message}", "OK");
            }
        }

        private async Task<bool> ConnectToDevice(BLEDevice device)
        {
            try
            {
                bool isInSensorBoot = await IsDeviceInSensorBootMode(device);

                if (isInSensorBoot && device.BaseDevice.State == DeviceState.Connected)
                {
                    await RequestMTU(device.BaseDevice);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        device.IsConnected = true;
                    });
                    return true;
                }

                await adapter.ConnectToDeviceAsync(device.BaseDevice);

                bool loginSuccess = await WriteToBootApplication(device, new LoginRequest(), true);
                
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    device.IsConnected = loginSuccess;
                });

                return loginSuccess;
            }
            catch (Exception e)
            {
                Console.WriteLine("ERROR" + e.Message.ToString());
                await DisplayAlert("Connection Error", $"Failed to connect to {device.Name}: {e.Message}", "OK");
                return false;
            }
        }

        
        
        
        private async void OnStartBootloaderUprade(BLEDevice device, int retry = 0)
        {
            HideMenu(); // Hide menu when starting upgrade

            if (device.BaseDevice.State != DeviceState.Connected)
            {
                bool connected = await ConnectToDevice(device);
                if (!connected)
                {
                    await DisplayAlert("Error", "Failed to connect to device before upgrade", "OK");
                    return;
                }
            }

            // Set upgrade in progress
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                device.IsUpgradeInProgress = true;
                device.UpgradeProgress = 0;
            });

            bool isInBootMode = await IsDeviceInSensorBootMode(device);

            if(!isInBootMode)
            {
                await WriteToBootApplication(device, new JumpToBootRequest(), false);
            }

            if (isInBootMode && device.BaseDevice.State == DeviceState.Connected)
            {
                await UpgradeBootloader(device, "firmwares/P46/M2.22/353BL10604.cyacd");
            }
        }



        private async void OnStartSensorUprade(BLEDevice device, int retry = 0)
        {
            HideMenu(); // Hide menu when starting upgrade

            if (device.BaseDevice.State != DeviceState.Connected)
            {
                bool connected = await ConnectToDevice(device);
                if (!connected)
                {
                    await DisplayAlert("Error", "Failed to connect to device before upgrade", "OK");
                    return;
                }
            }

            // Set upgrade in progress
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                device.IsUpgradeInProgress = true;
                device.UpgradeProgress = 0;
            });

            bool isInBootMode = await IsDeviceInSensorBootMode(device);

            if (!isInBootMode)
            {
                await WriteToBootApplication(device, new JumpToBootRequest(), false);
            }

            if (isInBootMode && device.BaseDevice.State == DeviceState.Connected)
            {
                await UpgradeBootloader(device, "firmwares/P46/M2.22/353AP3M222.cyacd");
            }
        }




        public async Task<bool> IsDeviceInSensorBootMode(BLEDevice device)
        {
            await adapter.ConnectToDeviceAsync(device.BaseDevice);

            var service = await device.BaseDevice.GetServiceAsync(Guid.Parse("00060000-f8ce-11e4-abf4-0002a5d5c51b"));

            if (service == null)
            {
                Console.WriteLine("SERVICE IS NULL");
                return false;
            }

            return true;
        }

        private async Task<bool> WriteToBootApplication(BLEDevice device, IRequest request, bool hasReply)
        {

            var ackReceivedTcs = new TaskCompletionSource<bool>();

            var service = await device.BaseDevice
                   .GetServiceAsync(Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131"));

            var writeCharacteristic = await service
                .GetCharacteristicAsync(Guid.Parse("0003cdd2-0000-1000-8000-00805f9b0131"));

            var notifyCharacteristic = await service
                .GetCharacteristicAsync(Guid.Parse("0003cdd1-0000-1000-8000-00805f9b0131"));

            if (notifyCharacteristic == null || !notifyCharacteristic.CanUpdate)
            {
                Console.WriteLine("⚠️ Notification characteristic not available.");
                ackReceivedTcs.SetResult(false);
                return false;
            }

            notifyCharacteristic.ValueUpdated += (s, e) =>
            {
                try
                {
                    var data = e.Characteristic.Value;
                    var reply = BLEParser.Parse(data);

                    if (reply.IsAck() && device.BaseDevice.State == DeviceState.Connected)
                    {
                        ackReceivedTcs.SetResult(true);

                        Console.WriteLine("✅ ACK received from device!");
                    }
                    else
                    {
                        ackReceivedTcs.SetResult(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error parsing response: {ex.Message}");
                }
            };

            if (hasReply)
            {

                await notifyCharacteristic.StartUpdatesAsync();
            }

            byte[] command = request.Create();

            await writeCharacteristic.WriteAsync(command);

            var completedTask = await Task.WhenAny(ackReceivedTcs.Task, Task.Delay(10000));

            if (!hasReply)
            {
                return true;
            }

            if (completedTask == ackReceivedTcs.Task)
            {
                return ackReceivedTcs.Task.Result;
            }
            else
            {
                Console.WriteLine("❌ Timeout waiting for ACK");
                return false;
            }

        }
 
        public async Task WriteBootPackets(ICharacteristic writeCharacteristic, byte[] bytes)
        {
            if (writeCharacteristic == null) throw new ArgumentNullException(nameof(writeCharacteristic));

            await writeCharacteristic.WriteAsync(bytes);

        }

        public async Task EnterBootLoader(ICharacteristic characteristic)
        {
            byte[] enterBootloaderBytes = new byte[]
            {
        0x01, 0x38, 0x06, 0x00,
        0x49, 0xA1, 0x34, 0xB6,
        0xC7, 0x79, 0xAD, 0xFC, 0x17
            };

            await WriteBootPackets(characteristic, enterBootloaderBytes);

        }

        public async Task GetFlashSize(ICharacteristic characteristic)
        {
            byte[] getSizeBytes = new byte[]
            {
        0x01, 0x32, 0x06, 0x01,
        0x00, 0x00, 0xCC, 0xFF,
        0x17
            };

            await WriteBootPackets(characteristic, getSizeBytes);


        }

        public async Task SendBootLoaderPacket(ICharacteristic writeCharacteristic, byte[] packet)
        {
            await WriteBootPackets(writeCharacteristic, packet);

        }

        public async Task RequestMTU(IDevice device)
        {
            try
            {

#if ANDROID
                int requestedMtu = 270;
                int negotiatedMtu = await device.RequestMtuAsync(requestedMtu);
                Console.WriteLine($"Negotiated MTU: {negotiatedMtu}");
#else
                Console.WriteLine("MTU negotiation is not supported on this platform (likely iOS)");
#endif

            }
            catch (DeviceConnectionException ex)
            {
                Console.WriteLine($"Connection failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex}");
            }
        }

        public async Task UpgradeBootloader(BLEDevice device, string cyacdFilePath)
        {
            var service = await device.BaseDevice.GetServiceAsync(Guid.Parse("00060000-f8ce-11e4-abf4-0002a5d5c51b"));
            var writeCharacteristic = await service.GetCharacteristicAsync(Guid.Parse("00060001-f8ce-11e4-abf4-0002a5d5c51b"));

            if (writeCharacteristic == null || !writeCharacteristic.CanWrite)
            {
                Console.WriteLine("⚠️ Write characteristic not available.");
                throw new Exception("WRITE CHAR CANNOT WRITE");
            }

            string relativePath = cyacdFilePath;
            var bootloader = await BootloaderUpgrade.CreateAsync(relativePath, [0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79]);

            // 🔹 Notification handler (async-safe)
            writeCharacteristic.ValueUpdated += (s, e) =>
            {
                var data = e.Characteristic.Value;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await bootloader.HandleResponse(data);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error parsing response: {ex.Message}");
                    }
                });
            };

            await writeCharacteristic.StartUpdatesAsync();

            // 🔹 BLE write handler
            bootloader.OnDataToWrite += async (sender, data) =>
            {
                await WriteBootPackets(writeCharacteristic, data);
            };

            // 🔹 Progress UI - Update individual device progress
            bootloader.OnProgressChanged += (sender, rowCount) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    device.UpgradeProgress = rowCount;
                    
                    // If upgrade is complete
                    if (rowCount >= 100)
                    {
                        device.IsUpgradeInProgress = false;
                    }
                });

                

                Console.WriteLine($"[DOTNET] Progress for {device.Name}: {rowCount}%");

            };

            await bootloader.StartDFU();
        }

        public async Task<bool> UpgradeSensor(ICharacteristic characteristic)
        {
            string relativePath = "firmwares/P46/0225/353AP30225.cyacd";

            await GetFlashSize(characteristic);

            PayloadProcessor payloadProcessor = new PayloadProcessor(relativePath, false,  [0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79]);
            List<byte[]> flashRows = await payloadProcessor.GetFirmwareFlashPackets();

            RowsToBeProgrammed = flashRows.Count;
            RowReachedCount = 0;

            for (int i = 0; i < flashRows.Count; i++)
            {
                var rowPacket = flashRows[i];
                await SendBootLoaderPacket(characteristic, rowPacket);

                // UI update on main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    RowReachedCount++;
                });

                Console.WriteLine($"ROW {i + 1}/{flashRows.Count} SENT AND ACKNOWLEDGED");
            }

            Console.WriteLine("✅ DFU completed successfully.");
            return true;
        }

        public async Task StartDFU(ICharacteristic characteristic, int firmwareType = 0)
        {

            string relativePath = "firmwares/P48/0227/353AP30227.cyacd";

            await EnterBootLoader(characteristic);

            await GetFlashSize(characteristic);

            relativePath = "firmwares/P48/0227/353AP30227.cyacd";

            PayloadProcessor payloadProcessor = new PayloadProcessor(relativePath, false, [0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79]);

            List<byte[]> flashRows = await payloadProcessor.GetFirmwareFlashPackets();

            //foreach(byte[] r in flashRows)
            //{
            //    Console.WriteLine("Row packet: " + BitConverter.ToString(r));

            //}
            RowsToBeProgrammed = flashRows.Count;
            RowReachedCount = 0; // reset progress

            foreach (var rowPacket in flashRows)
            {
                await SendBootLoaderPacket(characteristic, rowPacket);

                // update progress ONCE per successful row on UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RowReachedCount++; // property setter will update UpgradeProgress


                });

                Console.WriteLine("ROW SENT AND ACKNOWLEDGED");
            }

            Console.WriteLine("DFU completed!");
        }

    }
}
