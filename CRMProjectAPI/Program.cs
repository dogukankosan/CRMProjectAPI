using CRMProjectAPI.Data;
using CRMProjectAPI.Hubs;
using CRMProjectAPI.Middleware;
using CRMProjectAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace CRMProjectAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseWindowsService();

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddSwaggerGen(c =>
                {
                    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CRM Project API", Version = "v1" });
                    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                    {
                        Name = "Authorization",
                        Description = "JWT token giriniz. Örnek: Bearer {token}",
                        Type = SecuritySchemeType.Http,
                        Scheme = "Bearer",
                        BearerFormat = "JWT",
                        In = ParameterLocation.Header
                    });
                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                            },
                            Array.Empty<string>()
                        }
                    });
                    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
                    {
                        Name = "X-API-Key",
                        Description = "API anahtarınızı giriniz",
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header
                    });
                    c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                            },
                            Array.Empty<string>()
                        }
                    });
                });
            }

            builder.Services.AddSingleton<DapperContext>();
            builder.Services.AddScoped<ILogService, LogService>();
            builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
            builder.Services.AddSingleton<IJwtService, JwtService>();
            builder.Services.AddScoped<IMailService, MailService>();
            builder.Services.AddSignalR();

            string jwtSecret = builder.Configuration["JwtSettings:SecretKey"]!;
            string jwtIssuer = builder.Configuration["JwtSettings:Issuer"]!;
            string jwtAudience = builder.Configuration["JwtSettings:Audience"]!;

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtIssuer,
                        ValidAudience = jwtAudience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ClockSkew = TimeSpan.Zero
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var accessToken = context.Request.Query["access_token"];
                            var path = context.HttpContext.Request.Path;
                            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            {
                                context.Token = accessToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("CRMPolicy", policy =>
                {
                    policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });
            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CRM Project API v1");
                    c.DisplayRequestDuration();
                });
            }

            // CORS en üstte olmalı — middleware'ler preflight'ı kesmeden önce
            app.UseCors("CRMPolicy");
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseMiddleware<ApiExceptionHandlerMiddleware>();
            app.UseMiddleware<ApiKeyMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHub<TicketHub>("/hubs/ticket");
            app.MapControllers();

            app.Run();
        }
    }
}