
namespace DeliverySystem.Core;


public abstract record BaseEvent(string EventType);

// Evento 1: Pedido Recebido
public record OrderReceivedEvent(
    Guid OrderId,
    string CustomerName,
    DateTime Timestamp
) : BaseEvent("OrderReceived");

// Evento 2: Pedido em Trânsito
public record OrderInTransitEvent(
    Guid OrderId,
    DateTime Timestamp
) : BaseEvent("OrderInTransit");

// Evento 3: Pedido Entregue
public record OrderDeliveredEvent(
    Guid OrderId,
    DateTime Timestamp
) : BaseEvent("OrderDelivered");