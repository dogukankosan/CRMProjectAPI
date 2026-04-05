using CRMProjectUI.APIService;
using CRMProjectUI.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(); // Windows Service olarak çalışacaksa

// ── MVC ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── HttpContext ───────────────────────────────────────────────────────────────
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

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
        options.Cookie.SameSite = SameSiteMode.Strict; // Lax → Strict
        // HTTPS gelince → CookieSecurePolicy.Always yaparsın
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

// ── Global Authorize ─────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── API Base URL — tek yerden yönetim ────────────────────────────────────────
string apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"]
    ?? throw new InvalidOperationException("ApiSettings:BaseUrl ayarlanmamış!");

// ── API Services ──────────────────────────────────────────────────────────────
builder.Services.AddHttpClient<AuthApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<UserApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<CompanyApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<CustomerApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<MailSettingsApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<TicketApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<KnowledgeBaseApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<LogApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));
builder.Services.AddHttpClient<ErrorLogApiService>(c => c.BaseAddress = new Uri(apiBaseUrl));


var app = builder.Build();

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/500");
    // HTTPS gelince bu ikisini aç:
     app.UseHsts();
}

 app.UseHttpsRedirection(); // HTTPS gelince aç

app.UseStaticFiles();
app.UseRouting();
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<UserStatusMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AdminHome}/{action=Index}/{id?}");

// ── Global DTO ayarları — pipeline öncesinde set et ──────────────────────────
UserDto.ApiBaseUrl = apiBaseUrl;
UserListDto.ApiBaseUrl = apiBaseUrl;

app.Run();