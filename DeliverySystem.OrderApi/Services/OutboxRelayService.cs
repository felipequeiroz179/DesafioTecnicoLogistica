using DeliverySystem.Core.Data;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;

namespace DeliverySystem.OrderApi;

public class OutboxRelayService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnection _rabbitConnection;
    private readonly ILogger<OutboxRelayService> _logger;
    private const string QueueName = "order-events";

    public OutboxRelayService(IServiceProvider serviceProvider, IConnection rabbitConnection, ILogger<OutboxRelayService> logger)
    {
        _serviceProvider = serviceProvider;
        _rabbitConnection = rabbitConnection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
       
        using var channel = await _rabbitConnection.CreateChannelAsync(cancellationToken: stoppingToken);

        
        await channel.QueueDeclareAsync(queue: QueueName,
                                        durable: true,
                                        exclusive: false,
                                        autoDelete: false,
                                        arguments: null,
                                        cancellationToken: stoppingToken);

        _logger.LogInformation("OutboxRelayService está rodando.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxEvents(channel, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar outbox.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    // V7: O tipo do parâmetro mudou de 'IModel' para 'IChannel'
    private async Task ProcessOutboxEvents(IChannel channel, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryDbContext>();

        var events = await dbContext.OutboxEvents
            .Where(e => !e.IsProcessed)
            .OrderBy(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(stoppingToken);

        if (!events.Any()) return;

        _logger.LogInformation($"Encontrados {events.Count} eventos para publicar.");

        foreach (var ev in events)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(ev.Payload);

                // V7: Use a classe concreta BasicProperties
                var properties = new BasicProperties
                {
                    Persistent = true
                };

                // V7: Use BasicPublishAsync com todos os parâmetros obrigatórios
                await channel.BasicPublishAsync(exchange: "",
                                                routingKey: QueueName,
                                                mandatory: false,
                                                basicProperties: properties,
                                                body: body,
                                                cancellationToken: stoppingToken);

                ev.IsProcessed = true;
                ev.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Falha ao publicar evento {ev.Id}.");
            }
        }
        await dbContext.SaveChangesAsync(stoppingToken);
    }
}