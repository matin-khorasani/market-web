using Microsoft.AspNetCore.Identity;

// IdentityUser<int> already has Id, Email, UserName, PasswordHash, ...
public class AppUser : IdentityUser<int>
{
    public string FullName { get; set; } = string.Empty;
    public Cart? Cart { get; set; }
}
