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

    public class BLEDevice
    {
        public string Name
        {
            get;

            set;
        }

        public IDevice BaseDevice { get; set; }

        public bool IsConnected { get; set; } = false;


    }
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        public ObservableCollection<BLEDevice> Devices { get; set; } = new();


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

        public IBluetoothLE ble { get; set; }
        public IAdapter adapter { get; set; }





        //private TaskCompletionSource<byte[]> _notificationTcs;

        private object _lock = new();

        TaskCompletionSource<byte[]> tcs = new TaskCompletionSource<byte[]>();



        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
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

        //private void OnNotificationReceived(object sender, CharacteristicUpdatedEventArgs e)
        //{
        //    var data = e.Characteristic.Value;

        //    lock (_lock)
        //    {
        //        _notificationTcs?.TrySetResult(data);
        //    }
        //}
        //public async Task PrepareNotificationsAsync(ICharacteristic notifyCharacteristic)
        //{
        //    if (notifyCharacteristic == null || !notifyCharacteristic.CanUpdate)
        //        throw new InvalidOperationException("Characteristic does not support notifications.");

        //    notifyCharacteristic.ValueUpdated += OnNotificationReceived;
        //    await notifyCharacteristic.StartUpdatesAsync();
        //}


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


        //public async Task<bool> CheckBluetoothAccess()
        //{
        //    try
        //    {
        //        var requestStatus = await Permissions.CheckStatusAsync<BluetoothPermissions>();
        //        return requestStatus == PermissionStatus.Granted;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Oops  {ex}");
        //        return false;
        //    }
        //}

        //public async Task<bool> RequestBluetoothAccess()
        //{
        //    try
        //    {
        //        var requestStatus = await Permissions.RequestAsync<BluetoothPermissions>();
        //        return requestStatus == PermissionStatus.Granted;
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Oops  {ex}");
        //        return false;
        //    }
        //}


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

        // Replace the usage of BluetoothConnectPermission with the correct type from Microsoft.Maui.ApplicationModel.Permissions
        // There is no BluetoothConnectPermission in Microsoft.Maui.ApplicationModel.Permissions by default.
        // For Android 12+ Bluetooth permissions, you should use Permissions.Bluetooth or Permissions.BluetoothScan, Permissions.BluetoothAdvertise, and Permissions.BluetoothConnect if available.
        // However, in .NET MAUI, only Permissions.Bluetooth is available by default.

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
            Console.WriteLine(device.Name + "XXXXXXXXXXXXXXXXX");

            try
            {

                await adapter.ConnectToDeviceAsync(device.BaseDevice);

                await ConnectAndGetMtuAsync(device.BaseDevice);

                bool isInSensorBoot = await IsDeviceInSensorBootMode(adapter.ConnectedDevices[0]);

                if (isInSensorBoot)
                {

                    return;

                }


                var service = await adapter.ConnectedDevices[0].GetServiceAsync(Guid.Parse("0003cdd0-0000-1000-8000-00805f9b0131"));

                var characteristics = await service.GetCharacteristicsAsync();

                var writeCharacteristic = await service.GetCharacteristicAsync(Guid.Parse("0003cdd2-0000-1000-8000-00805f9b0131"));



                byte[] loginBytes = new byte[]
                {
                    0x01,       // Protocol Version
                    0x10, 0x00, // Telegram Type (0x0010)
                    0x09, 0x00, // Total Length (0x0009)
                    0xFB, 0x95, // CRC16 (0x95FB)
                    0x1D, 0x01  // Login Value (0x011D = 285)
                };

                await writeCharacteristic.WriteAsync(loginBytes);

                //DFUController dfuController = new DFUController("",);



                // 0003cdd2-0000-1000-8000-00805f9b0131 Write charcchteristic

                foreach (var c in characteristics)
                {
                    Console.WriteLine($"CHARS: " +
                        $"Id:{c.Id} " +
                        $"\n\n UUID: {c.Uuid} " +
                        $"\n\n Can read?: {c.CanRead} " +
                        $"\n\n Can write?: {c.CanWrite} " +
                        $"\n\n Can update?: {c.CanUpdate}"
                        );
                }

                // Assuming 'service' is your IDevice's service instance
                var notifyCharacteristic = await service.GetCharacteristicAsync(Guid.Parse("0003cdd1-0000-1000-8000-00805f9b0131"));

                if (notifyCharacteristic != null && notifyCharacteristic.CanUpdate)
                {
                    notifyCharacteristic.ValueUpdated += (s, e) =>
                    {
                        var data = e.Characteristic.Value; // byte []
                        // Handle the notification data here
                        Console.WriteLine("Notification received: " + BitConverter.ToString(data));
                    };

                    await notifyCharacteristic.StartUpdatesAsync();
                }

            }
            catch (DeviceConnectionException e)
            {
                // ... could not connect to device
            }
        }

        public async Task<bool> IsDeviceInSensorBootMode(IDevice connectedDevice)
        {
            var service = await connectedDevice.GetServiceAsync(Guid.Parse("00060000-f8ce-11e4-abf4-0002a5d5c51b"));

            var allCharachterstics = await service.GetCharacteristicsAsync();

            foreach (var c in allCharachterstics)
            {
                Console.WriteLine($"CHARS: " +
                    $"Id:{c.Id} " +
                    $"\n\n UUID: {c.Uuid} " +
                    $"\n\n Can read?: {c.CanRead} " +
                    $"\n\n Can write?: {c.CanWrite} " +
                    $"\n\n Can update?: {c.CanUpdate}"
                    );
            }

            if (service == null)
            {
                Console.WriteLine("SERVICE IS NULL");
                return false;
            }

            if (service != null)
            {
                Console.WriteLine("SERVICE IS NOT NULL");
                Console.WriteLine($"SERVICE: {service.Id}");

                var writeCharacteristic = await service.GetCharacteristicAsync(Guid.Parse("00060001-f8ce-11e4-abf4-0002a5d5c51b"));

                if (writeCharacteristic == null)
                {
                    return false;
                }


                if (writeCharacteristic != null)
                {
                    Console.WriteLine($"BOOOT: {writeCharacteristic.Uuid} - {writeCharacteristic.CanWrite}");
                    await StartDFU(writeCharacteristic);
                    return true;
                }

            }

            return false;

        }



        // Write a packet and wait for the notification response for that single write.
        // Uses per-call event handler and unsubscribes afterwards.
        // Assumes the device sends its response as a notification on the same characteristic.
        public async Task WriteBootPackets(ICharacteristic writeCharacteristic, byte[] bytes, int timeoutMs = 5000)
        {
            if (writeCharacteristic == null) throw new ArgumentNullException(nameof(writeCharacteristic));

            await writeCharacteristic.WriteAsync(bytes);

        }

        // Use the same characteristic you pass into StartDFU:
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

        // Send a single DFU packet using the provided characteristic.
        // IMPORTANT: this no longer increments RowReachedCount.
        public async Task SendBootLoaderPacket(ICharacteristic writeCharacteristic, byte[] packet)
        {
            await WriteBootPackets(writeCharacteristic, packet);

        }


        public async Task ConnectAndGetMtuAsync(IDevice device)
        {
            try
            {

            #if ANDROID
            // Request a specific MTU size (Android only)
            int requestedMtu = 270;
            int negotiatedMtu = await device.RequestMtuAsync(requestedMtu);
            Console.WriteLine($"Negotiated MTU: {negotiatedMtu}");

            // Use `negotiatedMtu` as the current MTU
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

        public async Task StartDFU(ICharacteristic characteristic)
        {
            await EnterBootLoader(characteristic);

            await GetFlashSize(characteristic);


            string relativePath = "firmwares/P48/0227/353AP30227.cyacd";
            PayloadProcessor payloadProcessor = new PayloadProcessor(relativePath, "49A134B6C779");

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
