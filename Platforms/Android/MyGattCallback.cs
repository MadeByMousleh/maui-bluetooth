using Android.Bluetooth;
using System;

namespace firmware_upgrade
{
    public class MyGattCallback : BluetoothGattCallback
    {
        public override void OnMtuChanged(BluetoothGatt gatt, int mtu, GattStatus status)
        {
            base.OnMtuChanged(gatt, mtu, status);
            System.Diagnostics.Debug.WriteLine($"MTU changed to {mtu}, status: {status}");
        }
    }
}
