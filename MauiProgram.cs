using firmware_upgrade.BLE;
using firmware_upgrade.BLEComamnds;
using firmware_upgrade.BLEComamnds.GetSensorSwVersion;
using firmware_upgrade.BLEComamnds.GetActorSwVersion;
using firmware_upgrade.BLEComamnds.GetSensorSwVersionExtended;
using Microsoft.Extensions.Logging;
using Shiny;
using firmware_upgrade.BLEComamnds.ActorBootPacket;
using firmware_upgrade.BLEComamnds.ActorBootState;
using firmware_upgrade.BLEComamnds.CheckPinCode;

namespace firmware_upgrade
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp
           .CreateBuilder()
           .UseMauiApp<App>()
           .UseShiny() // <-- add this line (this is important) this wires shiny lifecycle through maui
           .ConfigureFonts(fonts =>
           {
               fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
               fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
           });
#if DEBUG
            builder.Logging.AddDebug();
#endif


            BLEReplyRegistry.Register(0x0011, bytes => new LoginReply(bytes));
            BLEReplyRegistry.Register(0x012A, bytes => new GetSensorSwVersionReply(bytes)); // Register GetSensorSwVersionReply
            BLEReplyRegistry.Register(0x012C, bytes => new GetActorSwVersionReply(bytes)); // Register GetActorSwVersionReply
            BLEReplyRegistry.Register(0x0130, bytes => new GetSensorSwVersionExtendedReply(bytes)); // Register GetSensorSwVersionExtendedReply
            BLEReplyRegistry.Register(0x0015, bytes => new ActorBootPacketReply(bytes)); // Register ActorBootPacketReply
            BLEReplyRegistry.Register(0x0018, bytes => new ActorBootStateReply(bytes)); // Register ActorBootStateReply
            BLEReplyRegistry.Register(0x0132, bytes => new CheckPinCodeReply(bytes)); // Register CheckPinCodeReply

            return builder.Build();
        }
    }
}
