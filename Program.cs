using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using studentsData.Data;
using studentsData.Entities;
using studentsData.Helpers;
using studentsData.Services;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Serilog;
using studentsData.Middlewares;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.OpenApi.Models;
using System.Security.Claims; // <-- added for Swagger

namespace studentsData
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            // Configuration
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

            // DB
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseMySql(
                    builder.Configuration.GetConnectionString("DefaultConnection"),
                    new MySqlServerVersion(new Version(8, 0, 36)) // Replace with your actual MySQL version
                )
            // ? Enables automatic retries on transient failures
            );

            // Identity
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 6;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Add JWT Auth
            var jwtKey = builder.Configuration["Jwt:Key"];
            var key = Encoding.UTF8.GetBytes(jwtKey);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30), // small grace period to avoid tiny clock mismatches
                    RoleClaimType = ClaimTypes.Role
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        // optional: support token passed as query param, cookie, etc.
                        // var token = ctx.Request.Query["access_token"];
                        // if (!string.IsNullOrEmpty(token)) ctx.Token = token;
                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = ctx =>
                    {
                        // token invalid or expired
                        ctx.NoResult();
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        var pd = new ProblemDetails
                        {
                            Title = "Authentication failed",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = ctx.Exception?.Message ?? "Token validation failed."
                        };

                        var json = JsonSerializer.Serialize(pd, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                        return ctx.Response.WriteAsync(json);
                    },

                    OnChallenge = async ctx =>
                    {
                        // called when token missing or challenge required
                        ctx.HandleResponse(); // suppress default WWW-Authenticate-only response
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        var pd = new ProblemDetails
                        {
                            Title = "Unauthorized",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = "Missing or invalid Authorization header. Please provide a valid Bearer token."
                        };

                        var json = JsonSerializer.Serialize(pd, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                        await ctx.Response.WriteAsync(json);
                    }
                };
            });

            // Add services
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddControllers();

            // Swagger with JWT support
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "LeaveManagement API", Version = "v1" });

                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter the JWT token only (without 'Bearer ' prefix). Example: eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
                };
                c.AddSecurityDefinition("Bearer", securityScheme);

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        new string[] {}
                    }
                });
            });

            Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration) // supports appsettings "Serilog" section
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

            builder.Host.UseSerilog();

            var app = builder.Build();

            // Use middlewares early so they catch everything
            app.UseSerilogRequestLogging();          // Serilog request logging (optional)
            app.UseSimpleRequestLogging();           // your simple logging (optional)
            app.UseGlobalExceptionHandler();         // global exception handler

            // DEV helper: log whether Authorization header arrived (optional; remove in production)
            //app.UseMiddleware<DebugAuthHeaderMiddleware>();

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Database.Migrate();

                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                string[] roles = new[] { "Admin", "Employee" };
                foreach (var role in roles)
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));
                }
                // optionally seed an admin user (example)
                var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                var adminEmail = builder.Configuration["AdminUser:Email"];
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    var admin = await userManager.FindByEmailAsync(adminEmail);
                    if (admin == null)
                    {
                        var a = new ApplicationUser
                        {
                            UserName = adminEmail,
                            Email = adminEmail,
                            Name = "Administrator",
                            Department = "IT",
                            Designation = "Software Developer"
                        };
                        var res = await userManager.CreateAsync(a, builder.Configuration["AdminUser:Password"]);
                        if (res.Succeeded)
                        {
                            await userManager.AddToRoleAsync(a, "Admin");
                        }
                    }
                }
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
