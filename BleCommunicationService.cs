using System;
using System.Threading.Tasks;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE;
using System.Diagnostics;

namespace firmware_upgrade.Services
{
    public class BleCommunicationService
    {
        private readonly IBluetoothLE _ble;
        private readonly IAdapter _adapter;

        public event EventHandler<byte[]> ResponseReceived;

        public BleCommunicationService()
        {
            _ble = CrossBluetoothLE.Current;
            _adapter = CrossBluetoothLE.Current.Adapter;
        }

        public async Task<bool> ConnectAsync(IDevice device)
        {
            try
            {
                if (!_adapter.ConnectedDevices.Contains(device))
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
            Guid serviceGuid,
            Guid writeCharacteristicGuid,
            Guid notifyCharacteristicGuid,
            byte[] message)
        {
            try
            {
                var service = await device.GetServiceAsync(serviceGuid);
                if (service == null) return false;

                var writeChar = await service.GetCharacteristicAsync(writeCharacteristicGuid);
                var notifyChar = await service.GetCharacteristicAsync(notifyCharacteristicGuid);

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

        public async Task StopListeningAsync(IDevice device, Guid serviceGuid, Guid notifyCharacteristicGuid)
        {
            var service = await device.GetServiceAsync(serviceGuid);
            var notifyChar = await service.GetCharacteristicAsync(notifyCharacteristicGuid);
            if (notifyChar != null && notifyChar.CanUpdate)
            {
                await notifyChar.StopUpdatesAsync();
            }
        }
    }
}