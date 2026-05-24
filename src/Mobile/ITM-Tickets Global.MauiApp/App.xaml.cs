using ITM_Tickets_Global.MauiApp.Pages;

namespace ITM_Tickets_Global.MauiApp;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    // En MAUI .NET 10 el patrón recomendado es override CreateWindow en lugar
    // de asignar MainPage directamente (esa propiedad quedó obsoleta).
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var login = _services.GetRequiredService<LoginPage>();
        return new Window(new NavigationPage(login));
    }
}
