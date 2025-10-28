using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace BiketaBai.Helpers;

public static class AuthHelper
{
    public static async Task SignInUserAsync(HttpContext httpContext, int userId, string email, string fullName, bool isRenter, bool isOwner, bool isAdmin)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("UserId", userId.ToString()), // Add custom UserId claim for Profile page
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, fullName),
            new Claim("IsRenter", isRenter.ToString()),
            new Claim("IsOwner", isOwner.ToString()),
            new Claim("IsAdmin", isAdmin.ToString())
        };

        var claimsIdentity = new ClaimsIdentity(claims, "BiketaBaiAuth");
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(60) // Extended to 60 minutes
        };

        await httpContext.SignInAsync("BiketaBaiAuth", new ClaimsPrincipal(claimsIdentity), authProperties);
    }

    public static async Task SignOutUserAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync("BiketaBaiAuth");
    }

    public static int? GetCurrentUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }

    public static bool IsRenter(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("IsRenter");
        return claim != null && bool.TryParse(claim.Value, out bool isRenter) && isRenter;
    }

    public static bool IsOwner(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("IsOwner");
        return claim != null && bool.TryParse(claim.Value, out bool isOwner) && isOwner;
    }

    public static bool IsAdmin(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("IsAdmin");
        return claim != null && bool.TryParse(claim.Value, out bool isAdmin) && isAdmin;
    }
}

