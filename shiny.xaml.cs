using Shiny;
using Shiny.BluetoothLE;
using System.Reactive.Linq;
using Microsoft.Maui.Devices;
using Microsoft.Maui.ApplicationModel;
namespace firmware_upgrade;

public partial class shiny : ContentPage
{
    IBleManager bleManager;
    public shiny()
    {
        InitializeComponent();


    }

    protected async override void OnAppearing()
    {
        base.OnAppearing();

        var access = await bleManager.RequestAccess();
        if (access != AccessState.Available)
        {
            await CheckAndRequestBluetoothPermissions();
        }

        if (access == AccessState.Available)
        {
            var scanner = bleManager.Scan().Subscribe(scanResult =>
            {
                Console.WriteLine(scanResult.Peripheral);
             });
        }
    }

    private async Task<bool> CheckAndRequestBluetoothPermissions()
    {
        if (Microsoft.Maui.Devices.DeviceInfo.Platform != DevicePlatform.Android)
            return true;

        try
        {
            // For Android 12+ (API 31+), use Permissions.Bluetooth
            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                var bluetoothStatus = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.Bluetooth>();
                if (bluetoothStatus != PermissionStatus.Granted)
                {
                    bluetoothStatus = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Bluetooth>();
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