using firmware_upgrade.BLE;
using firmware_upgrade.BLEComamnds.ActorBootPacket;
using firmware_upgrade.BLEComamnds.ActorBootState;
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

        // Track notification subscriptions to avoid redundant operations
        public bool IsBootApplicationNotificationActive { get; set; } = false;
        public bool IsSensorBootNotificationActive { get; set; } = false;

        // Store characteristics references for reuse
        public ICharacteristic BootApplicationNotifyCharacteristic { get; set; }
        public ICharacteristic SensorBootNotifyCharacteristic { get; set; }

        public IService CurrentService { get; set; }
        public ICharacteristic CurrentWriteCharachteristic { get; set; }
        public ICharacteristic CurrentNotifyCharachteristic { get; set; }

        public EventHandler<byte[]>? onNotify;



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

        public bool IsApplication { get; internal set; }


        public async Task Write(byte[] data)
        {
            if (CurrentWriteCharachteristic != null)
            {
                await Task.Run(async () =>
                {
                    await CurrentWriteCharachteristic.WriteAsync(data);
                    Console.WriteLine("Writing - " + BitConverter.ToString(data));
                    await Task.Delay(5000);
                });
             
            }
        }

        public async Task WriteTwo(ReadOnlyMemory<byte> bytes)
        {
            if (CurrentWriteCharachteristic != null)
            {
                await Task.Run(async () =>
                {
                    await CurrentWriteCharachteristic.WriteAsync(bytes.ToArray());
                    Console.WriteLine("Writing - " + BitConverter.ToString(bytes.ToArray()));
                    await Task.Delay(5000);
                });

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

        public ICommand OnStartActorUpradeCommand => new Command<BLEDevice>(device => OnStartActorUprade(device));

        public ICommand GetDeviceInfoCommand => new Command<BLEDevice>(OnGetDeviceInfo);

        public ICommand GetSoftwareVersionCommand => new Command<BLEDevice>(OnGetSoftwareVersion);

        public ICommand GetDeviceDetailsCommand => new Command<BLEDevice>(OnGetDeviceDetails);

        public bool IsNotifyInit = false;

        public IBluetoothLE ble { get; set; }
        public IAdapter adapter { get; set; }

        private object _lock = new();

        TaskCompletionSource<byte[]> tcs = new TaskCompletionSource<byte[]>();

        // Fields for managing persistent notifications
        private TaskCompletionSource<IGeneralReply> _bootApplicationResponseReceived;
        private IGeneralReply _lastBootApplicationResponse;
        private BootloaderUpgrade _currentBootloaderUpgrade;



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
            adapter.ScanMode = ScanMode.LowLatency;



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


            await ConnectToDeviceNew(device);

        }

        private async Task DisconnectFromDevice(BLEDevice device)
        {
            try
            {
                // Clean up notifications before disconnecting
                await CleanupNotifications(device);

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
                    // Setup notifications for sensor boot mode
                    await SetupSensorBootNotifications(device);
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        device.IsConnected = true;
                    });
                    return true;
                }

                await adapter.ConnectToDeviceAsync(device.BaseDevice);

                // Setup notifications for boot application mode
                await SetupBootApplicationNotifications(device);

                IGeneralReply loginSuccess = await WriteToBootApplicationWithNotifications(device, new LoginRequest(), true);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    device.IsConnected = loginSuccess.IsAck;
                });

                return loginSuccess.IsAck;
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

            if (!isInBootMode)
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


        private async void OnStartActorUprade(BLEDevice device, int retry = 0)
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

            //bool isInBootMode = await IsDeviceInActorBootMode(device);
            //await Task.Delay(10000);


            //if (!isInBootMode)
            //{
            //    await WriteToBootApplicationNoCleanup(device, new LoginRequest(), true);
            //    // Wait a bit for the device to switch to boot mode
            //    await Task.Delay(10000);
            //}

            // Re-check if we're in boot mode after the jump command
            //isInBootMode = await IsDeviceInActorBootMode(device);

            //if (isInBootMode && device.BaseDevice.State == DeviceState.Connected)
            //{
            await UpgradeActor(device, "firmwares/P48/0227/353AP50227.cyacd");
            //}
            //else
            //{
            //    await MainThread.InvokeOnMainThreadAsync(() =>
            //    {
            //        device.IsUpgradeInProgress = false;
            //    });
            //    await DisplayAlert("Error", "Device did not enter actor boot mode", "OK");
            //}
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


        public async Task<bool> IsDeviceInActorBootMode(BLEDevice device)
        {
            try
            {
                IGeneralReply response = await WriteToBootApplicationWithNotifications(device, new ActorBootStateRequest(), true);

                if (response?.Data is ActorBootStateReply bleReply)
                {
                    if (bleReply.BootState == 1)
                    {
                        return response.IsAck;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error checking actor boot mode: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sets up notifications for boot application mode (once per connection)
        /// </summary>
        private async Task SetupBootApplicationNotifications(BLEDevice device)
        {
            if (device.IsBootApplicationNotificationActive)
            {
                Console.WriteLine("✅ Boot application notifications already active");
                return;
            }

            try
            {
                var service = await device.BaseDevice
                    .GetServiceAsync(Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131"));

                var notifyCharacteristic = await service
                    .GetCharacteristicAsync(Guid.Parse("0003cdd1-0000-1000-8000-00805f9b0131"));

                if (notifyCharacteristic == null || !notifyCharacteristic.CanUpdate)
                {
                    Console.WriteLine("⚠️ Boot application notification characteristic not available.");
                    return;
                }

                // Store reference for reuse
                device.BootApplicationNotifyCharacteristic = notifyCharacteristic;

                // Set up the notification handler
                notifyCharacteristic.ValueUpdated += (s, e) => OnBootApplicationNotificationReceived(device, e);

                await notifyCharacteristic.StartUpdatesAsync();
                device.IsBootApplicationNotificationActive = true;

                Console.WriteLine("✅ Boot application notifications setup complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error setting up boot application notifications: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up notifications for sensor boot mode (once per connection)
        /// </summary>
        private async Task SetupSensorBootNotifications(BLEDevice device)
        {
            if (device.IsSensorBootNotificationActive)
            {
                Console.WriteLine("✅ Sensor boot notifications already active");
                return;
            }

            try
            {
                var service = await device.BaseDevice.GetServiceAsync(Guid.Parse("00060000-f8ce-11e4-abf4-0002a5d5c51b"));
                var notifyCharacteristic = await service.GetCharacteristicAsync(Guid.Parse("00060001-f8ce-11e4-abf4-0002a5d5c51b"));

                if (notifyCharacteristic == null || !notifyCharacteristic.CanUpdate)
                {
                    Console.WriteLine("⚠️ Sensor boot notification characteristic not available.");
                    return;
                }

                // Store reference for reuse
                device.SensorBootNotifyCharacteristic = notifyCharacteristic;

                // Set up the notification handler
                notifyCharacteristic.ValueUpdated += (s, e) => OnSensorBootNotificationReceived(device, e);

                await notifyCharacteristic.StartUpdatesAsync();
                device.IsSensorBootNotificationActive = true;

                Console.WriteLine("✅ Sensor boot notifications setup complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error setting up sensor boot notifications: {ex.Message}");
            }
        }

        /// <summary>
        /// Centralized handler for boot application notifications
        /// </summary>
        private void OnBootApplicationNotificationReceived(BLEDevice device, CharacteristicUpdatedEventArgs e)
        {
            try
            {
                var data = e.Characteristic.Value;

                // Check if this is for a bootloader upgrade (actor upgrade)
                if (_currentBootloaderUpgrade != null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Console.WriteLine("I am getting stufff" + BitConverter.ToString(data));
                            await _currentBootloaderUpgrade.HandleResponse(data);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Error handling bootloader response: {ex.Message}");
                        }
                    });
                }
                else
                {
                    // Regular boot application command responses
                    var dataReply = BLEParser.Parse(data);

                    Console.WriteLine("I AM RECIEBING STUFF" + BitConverter.ToString(data));

                    // Store the last received response for any waiting operations
                    lock (_lock)
                    {
                        _lastBootApplicationResponse = new IGeneralReply
                        {
                            IsAck = dataReply.IsAck() && device.BaseDevice.State == DeviceState.Connected,
                            Data = dataReply
                        };

                        // Signal any waiting operations
                        _bootApplicationResponseReceived?.TrySetResult(_lastBootApplicationResponse);
                    }
                }

                Console.WriteLine($"✅ Boot application notification received from {device.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error processing boot application notification: {ex.Message}");
                _bootApplicationResponseReceived?.TrySetException(ex);
            }
        }

        /// <summary>
        /// Centralized handler for sensor boot notifications  
        /// </summary>
        private void OnSensorBootNotificationReceived(BLEDevice device, CharacteristicUpdatedEventArgs e)
        {
            try
            {
                var data = e.Characteristic.Value;

                // Handle sensor boot responses (for bootloader upgrades)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // This will be used by the bootloader upgrade process
                        if (_currentBootloaderUpgrade != null)
                        {
                            await _currentBootloaderUpgrade.HandleResponse(data);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Error handling sensor boot response: {ex.Message}");
                    }
                });

                Console.WriteLine($"✅ Sensor boot notification received from {device.Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error processing sensor boot notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans up all notifications when disconnecting
        /// </summary>
        private async Task CleanupNotifications(BLEDevice device)
        {
            try
            {
                if (device.IsBootApplicationNotificationActive && device.BootApplicationNotifyCharacteristic != null)
                {
                    await device.BootApplicationNotifyCharacteristic.StopUpdatesAsync();
                    device.IsBootApplicationNotificationActive = false;
                    Console.WriteLine("✅ Boot application notifications stopped");
                }

                if (device.IsSensorBootNotificationActive && device.SensorBootNotifyCharacteristic != null)
                {
                    await device.SensorBootNotifyCharacteristic.StopUpdatesAsync();
                    device.IsSensorBootNotificationActive = false;
                    Console.WriteLine("✅ Sensor boot notifications stopped");
                }

                // Clear references
                device.BootApplicationNotifyCharacteristic = null;
                device.SensorBootNotifyCharacteristic = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error cleaning up notifications: {ex.Message}");
            }
        }


        /// <summary>
        /// Writes to boot application using persistent notifications (no cleanup needed)
        /// </summary>
        private async Task<IGeneralReply> WriteToBootApplicationWithNotifications(BLEDevice device, IRequest request, bool hasReply = true)
        {
            var reply = new IGeneralReply { IsAck = false, Data = null };

            try
            {
                var service = await device.BaseDevice
                       .GetServiceAsync(Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131"));

                var writeCharacteristic = await service
                    .GetCharacteristicAsync(Guid.Parse("0003cdd2-0000-1000-8000-00805f9b0131"));

                if (!hasReply)
                {
                    byte[] requestData = request.Create();
                    await writeCharacteristic.WriteAsync(requestData);
                    return reply;
                }

                // Ensure notifications are setup
                await SetupBootApplicationNotifications(device);

                if (!device.IsBootApplicationNotificationActive)
                {
                    Console.WriteLine("⚠️ Boot application notifications not available.");
                    return reply;
                }

                // Create a new TaskCompletionSource for this specific request
                lock (_lock)
                {
                    _bootApplicationResponseReceived = new TaskCompletionSource<IGeneralReply>();
                }

                // Send the command
                byte[] command = request.Create();
                await writeCharacteristic.WriteAsync(command);

                // Wait for response with timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);

                var completedTask = await Task.WhenAny(_bootApplicationResponseReceived.Task, timeoutTask);

                if (completedTask == _bootApplicationResponseReceived.Task)
                {
                    return await _bootApplicationResponseReceived.Task;
                }
                else
                {
                    Console.WriteLine("❌ Timeout waiting for response");
                    return reply;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error in WriteToBootApplicationWithNotifications: {ex.Message}");
                return reply;
            }
        }

        private async Task<IGeneralReply> WriteToBootApplicationNoCleanup(BLEDevice device, IRequest request, bool hasReply = true)
        {
            // Use the new persistent notification method
            return await WriteToBootApplicationNoCleanup(device, request, hasReply);
        }

        private async Task<IGeneralReply> WriteToBootApplication(BLEDevice device, IRequest request, bool hasReply = true)
        {
            // Use the new persistent notification method
            return await WriteToBootApplicationWithNotifications(device, request, hasReply);
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

            // Ensure sensor boot notifications are setup (reuse existing if already active)
            await SetupSensorBootNotifications(device);

            string relativePath = cyacdFilePath;
            var bootloader = await BootloaderUpgrade.CreateAsync(relativePath, [0x49, 0xA1, 0x34, 0xB6, 0xC7, 0x79]);

            // Store reference for the notification handler to use
            _currentBootloaderUpgrade = bootloader;

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
                        _currentBootloaderUpgrade = null; // Clear reference
                    }
                });

                Console.WriteLine($"[DOTNET] Progress for {device.Name}: {rowCount}%");
            };

            await bootloader.StartDFU();
        }

        public async Task UpgradeActor(BLEDevice device, string cyacdFilePath)
        {
            //IService service = null;
            //ICharacteristic writeCharacteristic = null;

            InitCharachteristics(device);


            //string relativePath = cyacdFilePath;
            //var bootloader = await BootloaderUpgrade.CreateAsync(relativePath, [], 71, false, false);

            //// Store reference for the notification handler to process bootloader responses
            //_currentBootloaderUpgrade = bootloader;

            //// Setup notifications for boot application mode
            //await SetupBootApplicationNotifications(device);

            //IGeneralReply loginSuccess = await WriteToBootApplicationWithNotifications(device, new LoginRequest(), true);

            //Console.WriteLine(loginSuccess.Data + "YOYOYOYOOYOY");

        }



        //public async Task WriteBleMessage(byte[] bytes)
        //{


        //    characteristic.ValueUpdated += (o, args) =>
        //    {
        //        var bytes = args.Characteristic.Value;
        //    };

        //    await characteristic.StartUpdatesAsync();
        //}

        private async Task<bool> ConnectToDeviceNew(BLEDevice device)
        {
            try
            {
                await adapter.ConnectToDeviceAsync(device.BaseDevice);
                await InitCharachteristics(device);
                Console.WriteLine(device.CurrentService.Id);
                Console.WriteLine(device.CurrentWriteCharachteristic.Id);

                if (device.IsApplication)
                {
                    await device.Write(new byte[] { 0x01, 0x10, 0x00, 0x09, 0x00, 0xFB, 0x95, 0x1D, 0x01 });
                    await Task.Delay(1000);

                    await device.Write(new byte[] { 0x01, 0x01, 0x00, 0x08, 0x00, 0xd9, 0xcb, 0x02});
                    await Task.Delay(5000);

                    await device.Write(new byte[] { 0x01, 0x17, 0x00, 0x07, 0x00, 0xd9, 0xe7});
                    await Task.Delay(1000);

                    //await device.WriteTwo(new ReadOnlyMemory<byte>(new byte[] { 0x01, 0x14, 0x00, 0x0E, 0x00, 0x9D, 0xC6, 0x01, 0x38, 0x00, 0x00, 0xC7, 0xFF, 0x17 }));

                    //byte one = 0x01;
                    //byte two = 0x14;
                    //byte three = 0x00;
                    //byte one = 0x0E;
                    //byte one = 0x00;
                    //byte one = 0x9D;
                    //byte one = 0xC6;
                    //byte one = 0x01;
                    //byte one = 0x38;
                    //byte one = 0x00;
                    //byte one = 0x00;
                    //byte one = 0xC7;
                    //byte one = 0xFF;
                    //byte one = 0x17;

                    await device.Write(new byte[] { 0x01, 0x17, 0x00, 0x07, 0x00, 0xd9, 0xe7 });
                    await Task.Delay(1000);

                    await device.Write(new byte[] { 0x01, 0x17, 0x00, 0x07, 0x00, 0xd9, 0xe7 });
                    await Task.Delay(1000);


                    await device.Write(new byte[] { 0x01, 0x14, 0x00, 0x0E, 0x00, 0x9D, 0xC6, 0x01, 0x38, 0x00, 0x00, 0xC7, 0xFF, 0x17 });



                }

                return true;


            }
            catch (DeviceConnectionException e)
            {
                Console.WriteLine("ERROR: Could not connect to device");
                return false;
            }
            return false;
        }

        public async Task<bool> InitCharachteristics(BLEDevice device)
        {
            Console.WriteLine(device.BaseDevice.Id);
            try
            {
                if (device != null)
                {

                    if (device.BaseDevice != null)
                    {
                        var service = await device.BaseDevice.GetServiceAsync(Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131"));
                        device.CurrentService = service;


                        foreach (ICharacteristic ch in await service.GetCharacteristicsAsync())
                        {
                            
                            if(ch.CanUpdate)
                            {
                                device.CurrentNotifyCharachteristic = ch;
                            }
                            if(ch.CanWrite)
                            {
                                device.CurrentWriteCharachteristic = ch;
                            }

                        }

                        device.IsApplication = true;

                        if (IsNotifyInit == false)
                        {
                            device.CurrentNotifyCharachteristic.ValueUpdated += (o, args) =>
                            {
                                var bytes = args.Characteristic.Value;
                                device.onNotify?.Invoke(this, bytes);

                                Console.WriteLine("Message recieved - " + BitConverter.ToString(bytes));
                            };
                            await device.CurrentNotifyCharachteristic.StartUpdatesAsync();

                            IsNotifyInit = true;
                        }
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not find this service");
            }

            try
            {
                var service = await device.BaseDevice.GetServiceAsync(Guid.Parse("00060000-f8ce-11e4-abf4-0002a5d5c51b"));
                device.CurrentService = service;
                ICharacteristic writeAndNotify = await device.CurrentService.GetCharacteristicAsync(Guid.Parse("00060001-f8ce-11e4-abf4-0002a5d5c51b"));
                device.CurrentWriteCharachteristic = writeAndNotify;
                device.CurrentNotifyCharachteristic = writeAndNotify;

                return true;


            }
            catch (Exception e)
            {
                Console.WriteLine("Could not find this service");
            }
            return false;


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
