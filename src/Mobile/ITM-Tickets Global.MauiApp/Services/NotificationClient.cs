using Microsoft.AspNetCore.SignalR.Client;

namespace ITM_Tickets_Global.MauiApp.Services;

/// <summary>
/// Cliente SignalR que se conecta al NotificationHub del Notification.Api y
/// recibe TicketReadyEvent en tiempo real. Es el componente que cierra el
/// flujo de compra: la MAUI envía la orden por HTTP y, una vez emitido el
/// ticket, lo recibe push sin polling.
/// </summary>
public class NotificationClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;
    private HubConnection? _connection;

    public event Action<TicketNotification>? TicketReceived;
    public event Action<OrderConfirmedNotification>? OrderConfirmed;

    public NotificationClient(HttpClient http, TokenStore tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task ConnectAsync()
    {
        if (_connection is not null && _connection.State == HubConnectionState.Connected) return;

        var hubUrl = new Uri(_http.BaseAddress!, "/api/notifications/hubs/notifications").ToString();

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, opts =>
            {
                opts.AccessTokenProvider = () => Task.FromResult<string?>(_tokens.Token);
                // Para desarrollo local: aceptar el certificado dev de ASP.NET.
                opts.HttpMessageHandlerFactory = inner =>
                {
                    if (inner is HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    }
                    return inner;
                };
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<TicketNotification>("TicketReady", n => TicketReceived?.Invoke(n));
        _connection.On<OrderConfirmedNotification>("OrderConfirmed", n => OrderConfirmed?.Invoke(n));

        await _connection.StartAsync();
    }

    public async Task SubscribeToOrderAsync(Guid orderId)
    {
        if (_connection is null) throw new InvalidOperationException("Hub no conectado");
        await _connection.InvokeAsync("SubscribeToOrder", orderId.ToString());
    }

    public async Task DisconnectAsync()
    {
        if (_connection is not null)
        {
            await _connection.StopAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

public record TicketNotification(Guid TicketId, Guid OrderId, Guid EventId, string EventName,
    string Section, int Row, int SeatNumber, string QrCode);

public record OrderConfirmedNotification(Guid OrderId, DateTime ConfirmedAt, List<TicketNotification> Tickets);
