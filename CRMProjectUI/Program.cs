using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// ── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── HttpContext ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
var turkeyZone = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
// ── Cookie Authentication ─────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "CRM.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

// ── Global Authorize — login olmadan hiçbir yere girilemesin ─────────────────
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── API Services ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<AuthApiService>();
builder.Services.AddHttpClient<UserApiService>();
builder.Services.AddHttpClient<CompanyApiService>();
builder.Services.AddHttpClient<CustomerApiService>();
builder.Services.AddHttpClient<MailSettingsApiService>();
builder.Services.AddHttpClient<TicketApiService>();
builder.Services.AddHttpClient<KnowledgeBaseApiService>();
builder.Services.AddHttpClient<LogApiService>();
builder.Services.AddHttpClient<ErrorLogApiService>();
// ── Pipeline ──────────────────────────────────────────────────────────────────
WebApplication app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserStatusMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AdminHome}/{action=Index}/{id?}");

// ── Global DTO ayarları ───────────────────────────────────────────────────────
UserDto.ApiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "";
UserListDto.ApiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "";

app.Run();