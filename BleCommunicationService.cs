using System;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions;
using System.Diagnostics;

namespace firmware_upgrade.Services
{
    public class BleCommunicationService
    {
        private readonly IBluetoothLE _ble;
        private readonly IAdapter _adapter;
        public event EventHandler<byte[]>? ResponseReceived;

        public BleCommunicationService(IBluetoothLE ble, IAdapter adapter)
        {
            _ble = ble;
            _adapter = adapter;
        }

        public async Task<bool> ConnectAsync(IDevice device)
        {
            try
            {
                if (device.State != DeviceState.Connected)
                    await _adapter.ConnectToDeviceAsync(device);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BLE Connect failed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SendMessageAndListenAsync(
            IDevice device,
            Guid serviceUuid,
            Guid writeCharacteristicUuid,
            Guid notifyCharacteristicUuid,
            byte[] message)
        {
            try
            {
                var service = await device.GetServiceAsync(serviceUuid);
                if (service == null) return false;

                var writeChar = await service.GetCharacteristicAsync(writeCharacteristicUuid);
                var notifyChar = await service.GetCharacteristicAsync(notifyCharacteristicUuid);

                if (notifyChar != null && notifyChar.CanUpdate)
                {
                    notifyChar.ValueUpdated += (s, e) =>
                    {
                        ResponseReceived?.Invoke(this, e.Characteristic.Value);
                    };
                    await notifyChar.StartUpdatesAsync();
                }

                if (writeChar != null && writeChar.CanWrite)
                {
                    await writeChar.WriteAsync(message);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BLE Send/Listen failed: {ex.Message}");
                return false;
            }
        }

        public async Task StopListeningAsync(IDevice device, Guid serviceUuid, Guid notifyCharacteristicUuid)
        {
            var service = await device.GetServiceAsync(serviceUuid);
            var notifyChar = await service.GetCharacteristicAsync(notifyCharacteristicUuid);
            if (notifyChar != null && notifyChar.CanUpdate)
            {
                await notifyChar.StopUpdatesAsync();
            }
        }
    }
}