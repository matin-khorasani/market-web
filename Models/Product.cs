using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HotChocolate;
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    [Timestamp]       // یعنی ستون rowversion توی SQL Server
    [GraphQLIgnore]   // به کلاینت GraphQL نشون داده نشه
    public byte[] RowVersion { get; set; } = null!;
}
