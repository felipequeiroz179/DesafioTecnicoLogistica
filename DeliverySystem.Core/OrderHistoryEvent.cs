using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeliverySystem.Core;

public class OrderHistoryEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] 
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}