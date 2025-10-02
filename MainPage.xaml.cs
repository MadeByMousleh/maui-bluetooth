using Microsoft.Maui.ApplicationModel;
using Plugin.BLE;
using Plugin.BLE.Abstractions.Contracts;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace firmware_upgrade
{
    public partial class MainPage : ContentPage
    {

        public ObservableCollection<IDevice> Devices { get; set; } = new();

        public MainPage()
        {
            InitializeComponent();
            BindingContext = this;

            //Task.Run(() => CheckBluetoothAccess());

            //Task.Run(() => RequestBluetoothAccess());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            OnConnectionClicked();
            await CheckAndRequestBluetoothPermissions();

            var ble = CrossBluetoothLE.Current;
            var adapter = CrossBluetoothLE.Current.Adapter;
            var state = ble.State;

            ble.StateChanged += (s, e) =>
            {
                Debug.WriteLine($"The bluetooth state changed to {e.NewState}");
            };

            adapter.DeviceDiscovered += (s, a) =>
            {
                Devices.Add(a.Device);
                Console.WriteLine(a.Device.Name);
            };
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
    }
}
