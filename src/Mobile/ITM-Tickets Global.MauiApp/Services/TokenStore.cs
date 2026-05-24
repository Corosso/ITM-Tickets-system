namespace ITM_Tickets_Global.MauiApp.Services;

/// <summary>
/// Almacenamiento simple del JWT en memoria (en producción, usar SecureStorage de MAUI).
/// </summary>
public class TokenStore
{
    public string? Token { get; set; }
    public string? Username { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(Token) && ExpiresAt > DateTime.UtcNow;
}
