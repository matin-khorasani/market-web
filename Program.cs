using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

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

        // قفل موقت حساب بعد از چند بار رمز اشتباه (جلوی حدس زدن رمز رو می‌گیره)
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>();

// ---- Rate limiting: حداکثر ۱۰ درخواست در دقیقه برای هر IP روی endpointهای /api/auth ----
// نکته: پشت reverse proxy باید forwarded headers تنظیم بشه تا IP واقعی کاربر دیده بشه.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

// ---- JWT authentication ----
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
          ?? throw new InvalidOperationException("Missing 'Jwt' section in appsettings.json");
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");

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
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
// Singleton: اتصال به Redis گرونه و باید بین همه‌ی درخواست‌ها مشترک باشه
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Missing ConnectionStrings:Redis")));
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services
    .AddGraphQLServer()
    .AddAuthorization()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .AddErrorFilter<GraphQlErrorFilter>();

var app = builder.Build();

// ---- Seed roles, admin user and sample data ----
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// ---- Middleware pipeline (ORDER MATTERS) ----
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();   // 1) who are you?  (reads & validates the JWT)
app.UseAuthorization();    // 2) are you allowed? ([Authorize], roles)
app.MapControllers();
app.MapGraphQL();

app.Run();
