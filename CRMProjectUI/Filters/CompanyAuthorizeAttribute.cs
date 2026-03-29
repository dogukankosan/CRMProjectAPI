using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace CRMProjectUI.Filters
{
    public class CompanyAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;

            // Kullanıcı giriş yapmamışsa
            if (user.Identity == null || !user.Identity.IsAuthenticated)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Admin ve SuperAdmin her firmaya erişebilir — erken çık
            if (user.IsInRole("Admin") || user.IsInRole("SuperAdmin"))
            {
                base.OnActionExecuting(context);
                return;
            }

            // Claim yoksa sistem çökmemeli
            string? companyClaim = user.FindFirst("CompanyId")?.Value; // ← küçük d
            if (string.IsNullOrEmpty(companyClaim) || !int.TryParse(companyClaim, out int userCompanyId))
            {
                context.Result = new ForbidResult();
                return;
            }

            int? targetCompanyId = null;

            // 1. "customerId" route/query parametresinden al
            if (context.ActionArguments.TryGetValue("customerId", out var routeVal) && routeVal != null)
            {
                targetCompanyId = Convert.ToInt32(routeVal);
            }

            // 2. DTO içinden CompanyID prop'u ara (POST işlemleri)
            if (!targetCompanyId.HasValue)
            {
                foreach (var arg in context.ActionArguments.Values)
                {
                    if (arg == null) continue;
                    var prop = arg.GetType().GetProperty("CompanyID");
                    if (prop != null)
                    {
                        var value = prop.GetValue(arg);
                        if (value != null)
                        {
                            targetCompanyId = Convert.ToInt32(value);
                            break;
                        }
                    }
                }
            }

            // 3. targetCompanyId bulunabildiyse kontrol et
            if (targetCompanyId.HasValue)
            {
                if (userCompanyId != targetCompanyId.Value)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            // Not: "id" parametresi (Duzenle/{id}, Sil/{id} gibi) burada
            // kontrol edilemiyor çünkü kullanıcının hangi firmaya ait olduğunu
            // bilmek için API çağrısı gerekir. Bu action'larda controller
            // içinde manuel kontrol yapılmalı.

            base.OnActionExecuting(context);
        }
    }
}