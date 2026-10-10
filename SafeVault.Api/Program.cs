using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "SuperSecretSafeVaultKeyThatIsLongEnough123!";
var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<SafeVaultContext>(options =>
    options.UseSqlite("Data Source=safevault.db"));

builder.Services.AddValidatorsFromAssemblyContaining<VaultItemValidator>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SafeVaultContext>();
    db.Database.EnsureCreated();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/api/auth/login", (LoginModel model) =>
{
    if (model.Username == "admin" && model.Password == "SecurePassword123!")
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, model.Username),
                new Claim(ClaimTypes.Role, "Admin")
            }),
            Expires = DateTime.UtcNow.AddHours(2),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Results.Ok(new { Token = tokenHandler.WriteToken(token) });
    }

    return Results.Unauthorized();
});

app.MapGet("/api/vault/search", async (string query, SafeVaultContext db) =>
{
    var sanitizedQuery = System.Net.WebUtility.HtmlEncode(query ?? string.Empty);

    var results = await db.VaultItems
        .Where(v => v.Content.Contains(sanitizedQuery))
        .ToListAsync();

    return Results.Ok(results);
});

app.MapPost("/api/vault/item", [Authorize(Roles = "Admin")] async (VaultItem item, IValidator<VaultItem> validator, SafeVaultContext db) =>
{
    var validationResult = await validator.ValidateAsync(item);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }

    item.Content = System.Net.WebUtility.HtmlEncode(item.Content);

    db.VaultItems.Add(item);
    await db.SaveChangesAsync();

    return Results.Created($"/api/vault/item/{item.Id}", item);
});

app.Run();
