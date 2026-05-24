using System.Collections.ObjectModel;
using ITM_Tickets_Global.MauiApp.Services;

namespace ITM_Tickets_Global.MauiApp.Pages;

public partial class MainPage : ContentPage
{
    private readonly ApiClient _api;
    private readonly NotificationClient _notifications;
    private readonly TokenStore _tokens;

    public ObservableCollection<EventDto> Events { get; } = new();
    public ObservableCollection<TicketNotification> Tickets { get; } = new();

    public MainPage(ApiClient api, NotificationClient notifications, TokenStore tokens)
    {
        InitializeComponent();
        _api = api;
        _notifications = notifications;
        _tokens = tokens;

        EventsList.ItemsSource = Events;
        TicketsList.ItemsSource = Tickets;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        WelcomeLabel.Text = $"Hola, {_tokens.Username} 👋";

        try
        {
            await _notifications.ConnectAsync();
            _notifications.TicketReceived += OnTicketReceived;
            _notifications.OrderConfirmed += OnOrderConfirmed;
            LogLabel.Text = "✅ Conectado a SignalR";
        }
        catch (Exception ex)
        {
            LogLabel.Text = $"⚠ No se pudo conectar a SignalR: {ex.Message}";
        }
    }

    private void OnTicketReceived(TicketNotification ticket)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Tickets.Insert(0, ticket);
            LogLabel.Text = $"🎟 Recibido ticket {ticket.TicketId} en tiempo real";
        });
    }

    private void OnOrderConfirmed(OrderConfirmedNotification order)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LogLabel.Text = $"✅ Orden {order.OrderId} confirmada ({order.Tickets.Count} tickets)";
        });
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        try
        {
            var results = await _api.SearchAsync(QueryEntry.Text ?? "festival", VibeEntry.Text);
            Events.Clear();
            foreach (var ev in results) Events.Add(ev);
            LogLabel.Text = $"🔍 {results.Count} resultados";
        }
        catch (Exception ex)
        {
            LogLabel.Text = $"Error buscando: {ex.Message}";
        }
    }

    private async void OnBuyClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not Guid eventId) return;

        // Generamos el Correlation ID de la compra ANTES de la llamada, así
        // queda visible en la UI y se puede rastrear en los logs de los tres
        // servicios involucrados (order-api, inventory-api, notification-api).
        var correlationId = $"maui-{DateTime.UtcNow:HHmmss}-{Guid.NewGuid().ToString()[..8]}";

        // Asiento aleatorio: la sección VIP tiene 5 filas x 10 asientos = 50 lugares.
        // Pedir un seat aleatorio evita chocar con compras previas en pruebas
        // consecutivas (los asientos quedan Reserved en la BD entre llamadas).
        var rng = Random.Shared;
        var row = rng.Next(1, 6);          // 1..5
        var seatNumber = rng.Next(1, 11);  // 1..10

        var order = new CreateOrderDto(
            UserId: Guid.NewGuid(),
            Email: $"{_tokens.Username}@itm.edu.co",
            PhoneNumber: "300-000-0000",
            Items: new List<OrderItemDto>
            {
                new(eventId, "VIP", row, seatNumber, 1, 250.00)
            }
        );

        try
        {
            // Mostrar el ID en pantalla apenas iniciamos la compra
            CorrelationEntry.Text = correlationId;
            CorrelationFrame.IsVisible = true;
            LogLabel.Text = $"🛒 Compra iniciada (Correlation ID: {correlationId})";

            var result = await _api.CreateOrderAsync(order, correlationId);
            if (result is null)
            {
                LogLabel.Text = $"❌ No se pudo crear la orden (ID: {correlationId})";
                return;
            }

            LogLabel.Text = $"🛒 Orden {result.OrderId} en proceso. Esperando ticket vía SignalR...";
            await _notifications.SubscribeToOrderAsync(result.OrderId);
        }
        catch (Exception ex)
        {
            LogLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnCopyCorrelationClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CorrelationEntry.Text)) return;
        await Clipboard.SetTextAsync(CorrelationEntry.Text);
        LogLabel.Text = $"📋 Correlation ID copiado: {CorrelationEntry.Text}";
    }
}
