using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotChocolate;

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Total { get; set; }

    public int UserId { get; set; }

    [GraphQLIgnore]   // AppUser هش پسورد و... داره؛ هرگز از GraphQL نباید دیده بشه
    public AppUser User { get; set; } = null!;

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();

    [Timestamp]
    [GraphQLIgnore]
    public byte[] RowVersion { get; set; } = null!;
}