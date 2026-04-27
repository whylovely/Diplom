using System.Security.Claims;

namespace Server.Auth;

/// <summary>
/// Единственное место получения userId из JWT-claim'а в контроллерах.
/// Контроллеры вызывают <see cref="GetUserId"/> и фильтруют запросы по полученному Id —
/// это гарантирует изоляцию данных между пользователями.
/// </summary>
public static class UserContext
{
    /// <summary>
    /// Достаёт userId из claim'а NameIdentifier. Бросает исключение, если claim отсутствует —
    /// но при правильно настроенной [Authorize] это невозможно.
    /// </summary>
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(raw!);
    }
}