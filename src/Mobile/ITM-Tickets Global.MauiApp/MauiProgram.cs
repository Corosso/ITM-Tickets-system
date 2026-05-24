using ITM_Tickets_Global.MauiApp.Pages;
using ITM_Tickets_Global.MauiApp.Services;
using Microsoft.Extensions.Logging;

namespace ITM_Tickets_Global.MauiApp;

public static class MauiProgram
{
    public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
    {
        var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Configuración del gateway. En emulador Android usar 10.0.2.2;
        // en Windows / Mac usar localhost.
        var gatewayUrl =
#if ANDROID
            "http://10.0.2.2:8080";
#else
            "http://localhost:8080";
#endif

        builder.Services.AddSingleton(sp => new HttpClient
        {
            BaseAddress = new Uri(gatewayUrl),
            Timeout = TimeSpan.FromSeconds(30)
        });

        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<NotificationClient>();

        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
