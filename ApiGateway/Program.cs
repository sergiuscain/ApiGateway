using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- JWT ---
var jwtSection = builder.Configuration.GetSection("Jwt");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["Key"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Cookies.TryGetValue("access_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

// Аутентификация (парсит токен, если он есть, но не требует его — это делает [Authorize])
app.UseAuthentication();

// Middleware: если пользователь аутентифицирован — прокидываем X-User-Id / X-User-Roles вниз
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        // Убираем заголовки, если клиент их прислал (защита от подделки).
        context.Request.Headers.Remove("X-User-Id");
        context.Request.Headers.Remove("X-User-Roles");

        var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? context.User.FindFirstValue("sub");

        if (!string.IsNullOrEmpty(sub))
            context.Request.Headers["X-User-Id"] = sub;

        var roles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        if (roles.Length > 0)
            context.Request.Headers["X-User-Roles"] = string.Join(',', roles);
    }
    else
    {
        // Анонимный запрос: клиент не должен иметь возможности подсунуть заголовки.
        context.Request.Headers.Remove("X-User-Id");
        context.Request.Headers.Remove("X-User-Roles");
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger-json/orders/swagger/v1/swagger.json", "Orders API");
        options.SwaggerEndpoint("/swagger-json/products/swagger/v1/swagger.json", "Products API");
        options.SwaggerEndpoint("/swagger-json/auth/swagger/v1/swagger.json", "Auth API");
    });
}

app.MapReverseProxy();

app.Run();