using CRMProjectUI.APIService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;

    public UserStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserApiService userService,
        CustomerApiService customerService)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            if (!context.Request.Path.StartsWithSegments("/adminThema") &&
                !context.Request.Path.StartsWithSegments("/Auth") &&
                !context.Request.Path.StartsWithSegments("/Error"))
            {
                string? userIdStr = context.User.FindFirst(
                    System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                string? companyIdStr = context.User.FindFirst("CompanyId")?.Value;
                string? token = context.User.FindFirst("JwtToken")?.Value;

                if (int.TryParse(userIdStr, out int userId))
                {
                    // Kullanıcı aktif mi?
                    bool userActive = await userService.IsUserActiveAsync(userId, token);
                    if (!userActive)
                    {
                        await context.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Auth/Login?reason=passive");
                        return;
                    }
                }

                // Firma aktif mi? (SuperAdmin için firma kontrolü yapma)
                string? isAdminStr = context.User.FindFirst("IsAdmin")?.Value;
                int isAdminVal = int.TryParse(isAdminStr, out int av) ? av : 0;
                bool isSuperAdmin = isAdminVal == 2;

                if (!isSuperAdmin && int.TryParse(companyIdStr, out int companyId) && companyId > 0)
                {
                    bool companyActive = await customerService.IsCustomerActiveAsync(companyId, token);
                    if (!companyActive)
                    {
                        await context.SignOutAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme);
                        context.Response.Redirect("/Auth/Login?reason=company_passive");
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}