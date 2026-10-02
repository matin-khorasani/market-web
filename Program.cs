using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Database (EF Core) ----
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Identity (users, roles, password hashing) - "Core" version = no cookies/UI, perfect for APIs ----
builder.Services.AddIdentityCore<AppUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>();

// ---- JWT authentication ----
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
          ?? throw new InvalidOperationException("Missing 'Jwt' section in appsettings.json");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep claim names as we wrote them ("sub", "role", ...)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = "role",
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization();

// ---- Dependency Injection: register our services ----
// Scoped = one instance per HTTP request (required for anything that uses DbContext)
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// ---- Seed roles, admin user and sample data ----
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// ---- Middleware pipeline (ORDER MATTERS) ----
app.UseHttpsRedirection();
app.UseAuthentication();   // 1) who are you?  (reads & validates the JWT)
app.UseAuthorization();    // 2) are you allowed? ([Authorize], roles)
app.MapControllers();

app.Run();
