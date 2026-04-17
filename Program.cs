using InventoryManagement.API.Data;
using InventoryManagement.API.Services;
using InventoryManagement.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // frontend urls
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// DbContext
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories / Services
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IDistributionService, DistributionService>();

// JWT Auth
var keyStr = builder.Configuration["Jwt:Key"] ?? "MySuperSecretKeyForInventoryApp!@#123";
var key = Encoding.ASCII.GetBytes(keyStr);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

// Auto-migrate and seed Admin
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    // Automatically drops DB on startup so new column `IsApproved` schema is applied
    context.Database.EnsureDeleted();
    context.Database.EnsureCreated(); // Creates DB if not exists
    
    if (!context.Users.Any(u => u.Email == "admin@inventory.com"))
    {
        context.Users.Add(new User
        {
            Name = "System Admin",
            Email = "admin@inventory.com",
            Role = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"), // new easy credentials
            IsApproved = true // Admin must be approved
        });
        context.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
