using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Portal.Domain;

namespace Portal.Api.Auth;

public static class HttpContextExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static bool IsAdmin(this ClaimsPrincipal principal) =>
        principal.IsInRole(RoleNames.Admin);

    /// <summary>
    /// Kiểm tra CSRF cho các endpoint dùng cookie auth. Trả về IResult lỗi nếu không hợp lệ.
    /// </summary>
    public static async Task<IResult?> ValidateAntiforgeryAsync(this HttpContext context)
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return null;
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                title: "CSRF token không hợp lệ hoặc đã hết hạn.",
                statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
