using firmware_upgrade.BLE;
using firmware_upgrade.BLEComamnds;
using Microsoft.Extensions.Logging;
using Shiny;

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


            return builder.Build();
        }
    }
}
