namespace ITM_Tickets_Global.MauiApp.WinUI;

public partial class App : Microsoft.Maui.MauiWinUIApplication
{
    public App() { this.InitializeComponent(); }

    protected override Microsoft.Maui.Hosting.MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
