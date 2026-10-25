var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Swagger UI для агрегации
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Каждый SwaggerEndpoint указывает на проксированный swagger.json
        options.SwaggerEndpoint("/swagger-json/orders/swagger/v1/swagger.json", "Orders API");
        options.SwaggerEndpoint("/swagger-json/products/swagger/v1/swagger.json", "Products API");
        options.SwaggerEndpoint("/swagger-json/auth/swagger/v1/swagger.json", "Auth API");
    });
}

app.MapReverseProxy();

app.Run();