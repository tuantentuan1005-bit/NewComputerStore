using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NewComputerStore.Data;
using NewComputerStore.Hubs;
using NewComputerStore.Models;
using NewComputerStore.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "NewComputerStore API",
        Version = "v1",
        Description = "REST API cho ứng dụng Android (NewComputerStore)"
    });

    options.DocInclusionPredicate((docName, apiDesc) =>
        apiDesc.RelativePath != null &&
        apiDesc.RelativePath.StartsWith("api/", StringComparison.OrdinalIgnoreCase));

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Nhập JWT Bearer token (chỉ cần nhập chuỗi token hoặc 'Bearer {token}')",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = JwtBearerDefaults.AuthenticationScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, Array.Empty<string>() }
    });
});

builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    jwtKey = "__FALLBACK_PLACEHOLDER_KEY_REPLACE_WITH_USER_SECRETS__";
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "NewComputerStore";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "NewComputerStore.MobileClient";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);

        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            if (context.Request.Path.StartsWithSegments("/chatHub") &&
                !string.IsNullOrEmpty(context.Request.Query["access_token"]))
            {
                return JwtBearerDefaults.AuthenticationScheme;
            }

            return null;
        };

        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Path.StartsWithSegments("/chatHub"))
                {
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    }
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                if (context.Request.Path.StartsWithSegments("/api") ||
                    context.Request.Path.StartsWithSegments("/chatHub"))
                {
                    if (!context.Response.HasStarted)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    }
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        options.CallbackPath = "/signin-google";
        options.SaveTokens = true;
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status401Unauthorized,
                        Title = "Unauthorized",
                        Detail = "Token xác thực không hợp lệ, đã hết hạn hoặc bị thiếu.",
                        Instance = context.Request.Path
                    };
                    problem.Extensions["code"] = "UNAUTHORIZED";
                    await context.Response.WriteAsJsonAsync(problem);
                }
            },
            OnForbidden = async context =>
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new ProblemDetails
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Forbidden",
                        Detail = "Bạn không có quyền truy cập tài nguyên này.",
                        Instance = context.Request.Path
                    };
                    problem.Extensions["code"] = "FORBIDDEN";
                    await context.Response.WriteAsJsonAsync(problem);
                }
            }
        };
    });

// Add PasswordHasher
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// For accessing session in views and controllers if needed
builder.Services.AddHttpContextAccessor();

// Chat, Notifications, Email, Auth, Orders & JWT
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAccountAuthService, AccountAuthService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddDataProtection();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost;
    if (builder.Environment.IsDevelopment())
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

var app = builder.Build();

app.UseForwardedHeaders();

// Unified ProblemDetails error handling for /api routes
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/api"),
    apiApp =>
    {
        apiApp.UseExceptionHandler();
        apiApp.UseStatusCodePages();
    });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "NewComputerStore API v1");
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

// Add Session before Authentication and Authorization
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/chatHub");

// Initialize Database & Seed Admin
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();

    if (!context.Categories.Any())
    {
        context.Categories.AddRange(
            new Category { CategoryName = "Laptop" },
            new Category { CategoryName = "Máy tính" },
            new Category { CategoryName = "Linh kiện" }
        );
        context.SaveChanges();
    }

    var adminEmail = "admin@store.com";
    var adminUser = context.Users.FirstOrDefault(u => u.Email == adminEmail);
    if (adminUser == null)
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        adminUser = new User
        {
            FullName = "System Administrator",
            Email = adminEmail,
            Role = "Admin",
            IsEmailVerified = true,
            IsActive = true
        };
        adminUser.Password = passwordHasher.HashPassword(adminUser, "admin123");
        
        context.Users.Add(adminUser);
        context.SaveChanges();
    }
    else
    {
        bool changed = false;
        if (!adminUser.IsEmailVerified)
        {
            adminUser.IsEmailVerified = true;
            changed = true;
        }
        if (!adminUser.IsActive)
        {
            adminUser.IsActive = true;
            changed = true;
        }
        if (changed)
        {
            context.SaveChanges();
        }
    }
}

app.Run();
