using System.Text;
using Deepfake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Deepfake.API.Workers;
using DotNetEnv;

#region 🔥 ENV YÜKLEME
// .env dosyasını local ve docker ortamında otomatik yükler
Env.Load();
#endregion

var builder = WebApplication.CreateBuilder(args);

#region 🟢 DATABASE
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        Environment.GetEnvironmentVariable("DB_CONNECTION")
        ?? throw new Exception("DB_CONNECTION not found")
    )
);
#endregion

#region 🔵 SUPABASE
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY");

builder.Services.AddSingleton(
    new Supabase.Client(
        supabaseUrl ?? throw new Exception("SUPABASE_URL missing"),
        supabaseKey ?? throw new Exception("SUPABASE_KEY missing"),
        new Supabase.SupabaseOptions { AutoConnectRealtime = false }
    )
);
#endregion

#region 🟡 STORAGE SERVICE
builder.Services.AddScoped<
    Deepfake.Application.Interfaces.IStorageService,
    Deepfake.Infrastructure.Services.SupabaseStorageService
>();
#endregion

#region 🟠 RABBITMQ
builder.Services.AddScoped<
    Deepfake.Application.Interfaces.IAnalysisJobPublisher,
    Deepfake.Infrastructure.Services.RabbitMqPublisherService
>();

builder.Services.AddHostedService<RabbitMqResultListener>();
#endregion

#region 🔴 REPOSITORY
builder.Services.AddScoped<
    Deepfake.Application.Interfaces.IAnalysisRepository,
    Deepfake.Infrastructure.Repositories.AnalysisRepository
>();
#endregion

#region 🔐 JWT AUTHENTICATION
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
    ?? throw new Exception("JWT_SECRET not found");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret)
            ),
            ClockSkew = TimeSpan.Zero
        };

        // Cookie'den JWT okuma
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("jwt_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });
#endregion

#region ⚡ RATE LIMITER
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter =
        PartitionedRateLimiter.CreateChained(

            // Dakikada 5 istek (upload için)
            PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/api/Analysis/upload"))
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"{ip}-min",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 5,
                            Window = TimeSpan.FromMinutes(1)
                        });
                }

                return RateLimitPartition.GetNoLimiter("unlimited");
            }),

            // Saatlik limit
            PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (httpContext.Request.Path.StartsWithSegments("/api/Analysis/upload"))
                {
                    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"{ip}-hour",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromHours(1)
                        });
                }

                return RateLimitPartition.GetNoLimiter("unlimited");
            })
        );
});
#endregion

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<Deepfake.API.Middlewares.IpValidationMiddleware>();

app.MapControllers();

app.Run();