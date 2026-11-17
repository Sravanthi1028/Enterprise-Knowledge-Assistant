
using EnterpriseKnowledge.API.Services;
using EnterpriseKnowledge.API.Data;
using EnterpriseKnowledge.API.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. JWT Authentication Configuration
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is missing.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
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

        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtKey)
        ),

        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 2. OpenAPI and Controllers
builder.Services.AddOpenApi();
builder.Services.AddControllers();

// 3. Application Services
builder.Services.AddScoped<DocumentTextExtractor>();
builder.Services.AddScoped<DocumentSearchService>();
builder.Services.AddScoped<DocumentChunkingService>();
builder.Services.AddScoped<RAGService>();

// 4. Gemini AI Service
builder.Services.AddHttpClient<GeminiAIService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

// 5. Embedding Service
builder.Services.AddHttpClient<EmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
});

// 6. Password Hashing
builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>
>();

// 7. MySQL Database Connection
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseMySQL(
        builder.Configuration.GetConnectionString(
            "DefaultConnection"
        )!
    );
});

// 8. Build Application
var app = builder.Build();

// 9. OpenAPI Configuration
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 10. Health Check
app.MapGet("/health", () =>
{
    return Results.Ok(new
    {
        Status = "Healthy",
        Application = "Enterprise Knowledge Assistant",
        Message = "Backend is running successfully"
    });
});

// 11. Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// 12. Map Controllers
app.MapControllers();

// 13. Start Application
app.Run();
