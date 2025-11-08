using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DeliverySystem.Core;

public class Order
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] 
    public Guid Id { get; set; }

    [Required]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}