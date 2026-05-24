using ITM_Tickets_Global.MauiApp.Services;

namespace ITM_Tickets_Global.MauiApp.Pages;

public partial class LoginPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;

    public LoginPage(AuthService auth, IServiceProvider services)
    {
        InitializeComponent();
        _auth = auth;
        _services = services;
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        LoginButton.IsEnabled = false;
        StatusLabel.Text = "Autenticando...";

        try
        {
            var ok = await _auth.LoginAsync(UsernameEntry.Text ?? "", PasswordEntry.Text ?? "");
            if (ok)
            {
                var mainPage = _services.GetRequiredService<MainPage>();
                await Navigation.PushAsync(mainPage);
            }
            else
            {
                StatusLabel.Text = "Credenciales inválidas";
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
}
