using CRMProjectUI.APIService;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// MVC
builder.Services.AddControllersWithViews();
// HttpContext
builder.Services.AddHttpContextAccessor();
// API Services
builder.Services.AddHttpClient<CompanyApiService>();
WebApplication app = builder.Build();

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=AdminHome}/{action=Index}/{id?}");

app.Run();