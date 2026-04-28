using System.Security.Claims;

namespace Server.Auth;

// Единственное место получения userId из JWT-claim'а в контроллерах
public static class UserContext
{
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(raw!);
    }
}