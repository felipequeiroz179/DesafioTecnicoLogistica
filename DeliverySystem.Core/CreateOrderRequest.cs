using System.ComponentModel.DataAnnotations;

namespace DeliverySystem.Core;

public record CreateOrderRequest(
    [Required] string CustomerName
);