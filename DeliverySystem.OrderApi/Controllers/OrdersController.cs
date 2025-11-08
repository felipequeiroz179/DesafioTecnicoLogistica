using System.Text.Json;
using DeliverySystem.Core;
using DeliverySystem.Core.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeliverySystem.OrderApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly DeliveryDbContext _dbContext;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(DeliveryDbContext dbContext, ILogger<OrdersController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // 1. Endpoint: Criar um novo pedido
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        _logger.LogInformation("Recebida requisição para criar pedido para {Customer}", request.CustomerName);

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    CustomerName = request.CustomerName,
                    Status = "PedidoRecebido",
                    CreatedAt = DateTime.UtcNow,
                    LastUpdatedAt = DateTime.UtcNow
                };

                
                var initialHistory = new OrderHistoryEvent
                {
                    
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Status = "PedidoRecebido",
                    Timestamp = order.CreatedAt
                };

                var orderEvent = new OrderReceivedEvent(order.Id, order.CustomerName, order.CreatedAt);
                var outboxEvent = new OutboxEvent
                {
                    EventType = orderEvent.EventType,
                    Payload = JsonSerializer.Serialize(orderEvent, orderEvent.GetType()),
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Orders.Add(order);
                _dbContext.OrderHistoryEvents.Add(initialHistory); 
                _dbContext.OutboxEvents.Add(outboxEvent);

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Pedido {OrderId} criado com sucesso.", order.Id);
                return Accepted(new { orderId = order.Id, status = order.Status });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Falha ao criar pedido.");
                return StatusCode(500, "Erro interno ao criar pedido.");
            }
        });
    }

    // 2. Endpoint: Consultar o status atual de um pedido
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetOrderStatus(Guid id)
    {
        var order = await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound($"Pedido {id} não encontrado.");
        }

        return Ok(new { order.Id, order.Status, order.LastUpdatedAt });
    }

    // 3. Endpoint: Consultar o histórico completo de eventos de um pedido
    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetOrderHistory(Guid id)
    {
        var history = await _dbContext.OrderHistoryEvents
            .AsNoTracking()
            .Where(e => e.OrderId == id)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

        if (!history.Any())
        {
            
            var exists = await _dbContext.Orders.AnyAsync(o => o.Id == id);
            if (!exists) return NotFound($"Pedido {id} não encontrado.");

            return Ok(new List<OrderHistoryEvent>()); 
        }

        return Ok(history);
    }
}