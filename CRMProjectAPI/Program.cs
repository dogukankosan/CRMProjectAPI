using CRMProjectAPI.Data;
using CRMProjectAPI.Middleware;
using CRMProjectAPI.Services;
using Microsoft.AspNetCore.Diagnostics;

namespace CRMProjectAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            // Data
            builder.Services.AddSingleton<DapperContext>();
            // Services
            builder.Services.AddScoped<ILogService, LogService>();
            builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
            // CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            WebApplication app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            // Middleware sýrasý önemli!
            app.UseMiddleware<ApiExceptionHandlerMiddleware>(); 
            app.UseMiddleware<ApiKeyMiddleware>();       
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}