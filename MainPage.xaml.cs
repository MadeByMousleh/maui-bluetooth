using Microsoft.Maui.ApplicationModel;
using Plugin.BLE;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.Exceptions;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
    public partial class MainPage : ContentPage
    {

        public ObservableCollection<BLEDevice> Devices { get; set; } = new();

        public ICommand connectCommand;
        public ICommand ConnectCommand => new Command<BLEDevice>(OnConnectClicked);

        public IBluetoothLE ble { get; set; }
        public IAdapter adapter { get; set; }
        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            OnConnectionClicked();
            await CheckAndRequestBluetoothPermissions();

            ble = CrossBluetoothLE.Current;
            adapter = CrossBluetoothLE.Current.Adapter;

            BluetoothState state = ble.State;




            ble.StateChanged += (s, e) =>
            {
                Debug.WriteLine($"The bluetooth state changed to {e.NewState}");
            };

            adapter.DeviceDiscovered += (s, a) =>
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
                    Devices.Add(device); 
                }
                Console.WriteLine("DEVICE:" + a.Device.Id);
            };


            var scanFilterOptions = new ScanFilterOptions();
            //scanFilterOptions.ServiceUuids = new[] { guid1, guid2, etc }; // cross platform filter
            //scanFilterOptions.ManufacturerDataFilters = new[] { new ManufacturerDataFilter(1), new ManufacturerDataFilter(2) }; // android only filter
            scanFilterOptions.DeviceAddresses = new[] { "10:B9:F7", }; // android only filter
            await adapter.StartScanningForDevicesAsync(scanFilterOptions);

            await adapter.StartScanningForDevicesAsync();


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

               bool isInSensorBoot = await IsDeviceInSensorBootMode(adapter.ConnectedDevices[0]);

                if(isInSensorBoot)
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

                if(writeCharacteristic == null)
                {
                    return false;
                }


                if (writeCharacteristic != null)
                {
                    Console.WriteLine($"BOOOT: {writeCharacteristic.Uuid} - {writeCharacteristic.CanWrite}");
                    await EnterBootLoader(writeCharacteristic);
                    return true;
                }

            }

            return false;

        }


        public async Task<bool> WriteBootPackets(byte[] bytes)
        {
            IDevice connectedDevice = Devices[0].BaseDevice;

            var service = await connectedDevice.GetServiceAsync(Guid.Parse("00060000-f8ce-11e4-abf4-0002a5d5c51b"));


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
                    Console.WriteLine("writeCharacteristic IS NULL");

                    return false;
                }


                if (writeCharacteristic != null)
                {
                    Console.WriteLine($"BOOOT: {writeCharacteristic.Uuid} - {writeCharacteristic.CanWrite}");


                    if (writeCharacteristic != null && writeCharacteristic.CanUpdate)
                    {
                        writeCharacteristic.ValueUpdated += (s, e) =>
                        {
                            var data = e.Characteristic.Value; // byte []
                                                               // Handle the notification data here
                            Console.WriteLine("Notification received: " + BitConverter.ToString(data));
                        };

                        await writeCharacteristic.StartUpdatesAsync();
                    }


                    Console.WriteLine("BYTES: " + BitConverter.ToString(bytes));
                    await writeCharacteristic.WriteAsync(bytes);


                    return true;
                }

            }

            return false;
        }

        public async Task<bool> EnterBootLoader(ICharacteristic characteristic)
        {
            BootLoaderPacketGen bootGen = new BootLoaderPacketGen();

            byte[] enterBooladerBytes = new byte[]
              {
                    0x01,       // Start of packet
                    0x38,       // Enter bootloader command
                    0x06, 0x00, // length
                    0x49,0xA1, // security ID
                    0x34,0xB6,
                    0xC7,0x79,
                    0xAD,0xFC, // checksum
                    0x17,      // End of packet
              };


           await WriteBootPackets(enterBooladerBytes);


            return true;

        }


    }
}
