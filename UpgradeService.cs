using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

public class UpgradeService
{
    public string IP { get; set; } = "192.168.40.1";
    public Dictionary<string, DFUController> DFUControllers { get; } = new();
    public string PAYLOAD_PATH { get; set; } = "../../../firmwares/P42/0217/353AP10217.cyacd";

    private NextGenService nextGenService;

    public UpgradeService()
    {
        nextGenService = new NextGenService(IP);
    }

    // Placeholder for sending Cypress data over BLE (implement as needed)
    public void SendCypressData(string data, object mac)
    {
        // Implement BLE write logic here
        Console.WriteLine("-----------------------------------------------------------------------------------");
        Console.WriteLine("Write Data: " + data);
        Console.WriteLine("-----------------------------------------------------------------------------------");
    }

    // Placeholder for checking if handle is present (implement as needed)
    public async Task<bool> CheckIfHandleIsThere(string mac)
    {
        // Implement BLE handle check logic here
        return await Task.FromResult(true);
    }

    // Placeholder for opening notification (implement as needed)
    public async Task<bool> OpenNotification(string nodeMac)
    {
        // Implement BLE notification logic here
        return await Task.FromResult(true);
    }

    // Placeholder for sending jump to boot telegram (implement as needed)
    public async Task<bool> SendJumpToBootTelegram(string nodeMac)
    {
        // Implement BLE jump to boot logic here
        return await Task.FromResult(true);
    }

    // Placeholder for sending data to subscribers (implement as needed)
    public void SendData(object data)
    {
        Console.WriteLine(data);
    }

    public async Task UpgradeSensor(string mac)
    {
        Console.WriteLine("\n\n ################ UPGRADE SENSOR ################ \n\n");
        Console.WriteLine("Trying to connect and login to detector...\n\n");

        var isConnected = await nextGenService.Login(mac);

        if (isConnected)
        {
            Console.WriteLine("Connection and login to detector successfully executed...\n\n");
            Console.WriteLine("Jumping to boot... \n\n");

            var jumpToBootResponse = await nextGenService.JumpToBoot(mac);

            if (jumpToBootResponse)
            {
                Console.WriteLine("Jumping to boot successfully executed... \n\n");
            }
        }
    }

    public async Task OpenAndStartDFU(string mac)
    {
        try
        {
            if (!DFUControllers.ContainsKey(mac))
            {
                // bool deviceInBoot = await CheckIfHandleIsThere(mac);
                bool deviceInBoot = true; // Placeholder

                if (!deviceInBoot)
                {
                    Console.WriteLine("Device is not in boot");

                    var jumpToBootResult = await SendJumpToBootTelegram(mac);

                    if (jumpToBootResult)
                    {
                        Console.WriteLine("Jumped to boot");
                        Console.WriteLine("Disconnected...");

                        // await CassiaEndpoints.ConnectToBleDevice(mac);
                        // await ConnectToDevice(mac);

                        // Re-attempt DFU after reconnect
                        await OpenAndStartDFU(mac);
                        return;
                    }
                }
            }

            Console.WriteLine("Device is in boot");

            var notificationOpen = await OpenNotification(mac);

            if (notificationOpen)
            {
                Console.WriteLine("Notification opened");

                var payload = File.ReadAllText(PAYLOAD_PATH);

                var controller = new DFUController(payload, "49A134B6C779", SendCypressData, mac);
                DFUControllers[mac] = controller;

                controller.OnProgress((name, data) => SendData(data));
                controller.StartDFU();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Failed here: " + e);
        }
    }

    public void Start(string mac)
    {
        _ = UpgradeSensor(mac);
    }
}