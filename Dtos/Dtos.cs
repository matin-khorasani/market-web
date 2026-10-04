using System.ComponentModel.DataAnnotations;

// ---------- Category ----------
public record CategoryDto(int Id, string Name, string? Description);

public class CategorySaveDto
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
}

// ---------- Product ----------
public record ProductDto(int Id, string Name, string? Description, decimal Price,
                         int StockQuantity, string? ImageUrl, int CategoryId, string CategoryName);

public class ProductSaveDto
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Range(0.01, 1_000_000)] public decimal Price { get; set; }
    [Range(0, int.MaxValue)] public int StockQuantity { get; set; }
    public string? ImageUrl { get; set; }
    [Required] public int CategoryId { get; set; }
}

// ---------- Auth ----------
public class RegisterDto
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken,
                           int UserId, string Email, IList<string> Roles);

public class RefreshRequestDto
{
    [Required] public string RefreshToken { get; set; } = string.Empty;
}

// ---------- Admin: users ----------
public record UserDto(int Id, string FullName, string Email, List<string> Roles);

public class ChangeRoleDto
{
    public UserRole Role { get; set; }
}

// ---------- Cart ----------
public record CartItemDto(int Id, int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);
public record CartDto(int Id, List<CartItemDto> Items, decimal Total);

public class CartItemAddDto
{
    [Required] public int ProductId { get; set; }
    [Range(1, 1000)] public int Quantity { get; set; } = 1;
}

public class CartItemUpdateDto
{
    [Range(1, 1000)] public int Quantity { get; set; }
}
