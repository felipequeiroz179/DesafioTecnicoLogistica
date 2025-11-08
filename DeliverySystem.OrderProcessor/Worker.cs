using DeliverySystem.Core;
using DeliverySystem.Core.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace DeliverySystem.OrderProcessor;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _rabbitConnection;
    private readonly ILogger<Worker> _logger;
    private readonly string _queueName;
    private IChannel _channel = null!;

    public Worker(
        IServiceProvider serviceProvider,
        IConnection rabbitConnection,
        IConfiguration configuration,
        ILogger<Worker> logger)
    {
        _serviceProvider = serviceProvider;
        _rabbitConnection = rabbitConnection;
        _logger = logger;
        _queueName = configuration["RabbitMQ:QueueName"]!;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        
        _channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: cancellationToken);

        
        await _channel.QueueDeclareAsync(queue: _queueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: cancellationToken);

        
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

        _logger.LogInformation("Worker está rodando e escutando a fila {QueueName}.", _queueName);

        
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            string eventType = "unknown";

            try
            {
                
                using (JsonDocument doc = JsonDocument.Parse(message))
                {
                    
                    if (doc.RootElement.TryGetProperty("EventType", out JsonElement typeElement))
                    {
                        eventType = typeElement.GetString() ?? "unknown";
                    }
                }

                _logger.LogInformation("Recebido evento {EventType}.", eventType);

                await HandleEvent(eventType, message);

                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar evento {EventType}.", eventType);

                if (_channel.IsOpen)
                {
                    
                    await _channel.BasicNackAsync(ea.DeliveryTag, false, true, cancellationToken: stoppingToken);
                }
            }
        };

        await _channel.BasicConsumeAsync(queue: _queueName,
                                         autoAck: false,
                                         consumer: consumer,
                                         cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleEvent(string eventType, string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();

        switch (eventType)
        {
            case "OrderReceived":
                await ProcessOrderReceived(dbContext, JsonSerializer.Deserialize<OrderReceivedEvent>(message)!);
                break;
            case "OrderInTransit":
                await ProcessOrderInTransit(dbContext, JsonSerializer.Deserialize<OrderInTransitEvent>(message)!);
                break;
            case "OrderDelivered":
                await ProcessOrderDelivered(dbContext, JsonSerializer.Deserialize<OrderDeliveredEvent>(message)!);
                break;
            default:
                _logger.LogWarning($"Tipo de evento desconhecido: {eventType}");
                break;
        }
    }

    private async Task ProcessOrderReceived(DeliveryDbContext dbContext, OrderReceivedEvent ev)
    {
        var order = await dbContext.Orders.FindAsync(ev.OrderId);
        if (order == null) return;

        if (order.Status != "PedidoRecebido")
        {
            _logger.LogWarning($"Evento duplicado {ev.OrderId}. Status atual: {order.Status}. Ignorando.");
            return;
        }

        _logger.LogInformation($"Processando [Separação] para {order.Id}...");
        await Task.Delay(2000);

        
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                order.Status = "EmTransporte";
                order.LastUpdatedAt = DateTime.UtcNow;

                var history = new OrderHistoryEvent
                {
                    OrderId = order.Id,
                    Status = "EmTransporte",
                    Timestamp = DateTime.UtcNow
                };

                var nextEventDto = new OrderInTransitEvent(order.Id, DateTime.UtcNow);
                var nextEvent = new OutboxEvent
                {
                    EventType = nextEventDto.EventType,
                    Payload = JsonSerializer.Serialize(nextEventDto, nextEventDto.GetType()),
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.OrderHistoryEvents.Add(history);
                dbContext.OutboxEvents.Add(nextEvent);

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw; 
            }
        });

        _logger.LogInformation($"[Separação] concluída para {order.Id}. Próximo evento: OrderInTransit");
    }

    private async Task ProcessOrderInTransit(DeliveryDbContext dbContext, OrderInTransitEvent ev)
    {
        var order = await dbContext.Orders.FindAsync(ev.OrderId);
        if (order == null) return;

        if (order.Status != "EmTransporte")
        {
            _logger.LogWarning($"Evento duplicado {ev.OrderId}. Status atual: {order.Status}. Ignorando.");
            return;
        }

        _logger.LogInformation($"Processando [Transporte] para {order.Id}...");
        await Task.Delay(3000);

        
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            try
            {
                order.Status = "Entregue";
                order.LastUpdatedAt = DateTime.UtcNow;

                var history = new OrderHistoryEvent
                {
                    OrderId = order.Id,
                    Status = "Entregue",
                    Timestamp = DateTime.UtcNow
                };

                var nextEventDto = new OrderDeliveredEvent(order.Id, DateTime.UtcNow);
                var nextEvent = new OutboxEvent
                {
                    EventType = nextEventDto.EventType,
                    Payload = JsonSerializer.Serialize(nextEventDto, nextEventDto.GetType()),
                    CreatedAt = DateTime.UtcNow
                };

                dbContext.OrderHistoryEvents.Add(history);
                dbContext.OutboxEvents.Add(nextEvent);

                await dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        _logger.LogInformation($"[Transporte] concluído para {order.Id}. Próximo evento: OrderDelivered");
    }

    private async Task ProcessOrderDelivered(DeliveryDbContext dbContext, OrderDeliveredEvent ev)
    {
        _logger.LogInformation($"Processamento [Entregue] finalizado para {ev.OrderId}. Fim do fluxo.");
        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null && _channel.IsOpen)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}