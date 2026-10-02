using System.Security.Claims;

public static class UserClaimsExtensions
{
    // Reads the user id we put in the token ("sub" claim)
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirst("sub")?.Value
                  ?? throw new InvalidOperationException("Token has no 'sub' claim."));
}
