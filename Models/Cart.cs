public class Cart
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
