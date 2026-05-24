namespace ITM_Tickets_Global.MauiApp.Services;

public class AuthService
{
    private readonly ApiClient _api;
    private readonly TokenStore _tokens;

    public AuthService(ApiClient api, TokenStore tokens)
    {
        _api = api;
        _tokens = tokens;
    }

    public async Task<bool> LoginAsync(string username, string password)
    {
        var result = await _api.LoginAsync(username, password);
        if (result is null) return false;

        _tokens.Token = result.Token;
        _tokens.Username = result.Username;
        _tokens.ExpiresAt = result.ExpiresAt;
        return true;
    }

    public void Logout()
    {
        _tokens.Token = null;
        _tokens.Username = null;
        _tokens.ExpiresAt = null;
    }
}
