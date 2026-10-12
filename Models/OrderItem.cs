using System.ComponentModel.DataAnnotations.Schema;

public class OrderItem
{
    public int Id { get; set; }

    // Snapshot: مقدارها در لحظه‌ی خرید کپی می‌شن و بعداً با تغییر محصول عوض نمی‌شن
    public string ProductName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    // nullable: اگه محصول بعداً حذف بشه، سفارش قدیمی باید سالم بمونه
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
}