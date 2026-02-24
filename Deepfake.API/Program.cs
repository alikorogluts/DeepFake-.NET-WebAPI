using System.Text;
using Deepfake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Deepfake.API.Workers;
using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides; // 🚨 Yeni eklendi: Proxy üzerinden gerçek IP için

#region 🔥 ENV YÜKLEME
try
{
    // TraversePath() ana dizindeki .env dosyasını bulur.
    DotNetEnv.Env.TraversePath().Load();
}
catch
{
    Console.WriteLine("Uyarı: .env dosyası bulunamadı. Sistem ortam değişkenleri kullanılacak.");
}
#endregion

var builder = WebApplication.CreateBuilder(args);

// 🚨 1. ADIM: FORWARDED HEADERS KONFİGÜRASYONU
// Railway proxy arkasında çalıştığı için gerçek IP ve HTTPS protokolünü anlamamızı sağlar.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
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
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) });
                }
                return RateLimitPartition.GetNoLimiter("unlimited");
            })
        );
});
#endregion

#region 🌐 CORS AYARLARI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) 
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); 
    });
});
#endregion

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// 🚨 2. ADIM: MIDDLEWARE SIRALAMASI (KRİTİK)

// En tepede olmalı: Gelen isteğin Railway proxy'sinden geldiğini ve asıl kullanıcı IP'sini çözmemizi sağlar.
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection(); 
}

// CORS, Authentication'dan önce çağrılmalıdır.
app.UseCors("AllowAll");

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 🚨 TODO: Eğer 403 almaya devam edersen, IP doğrulama mantığını X-Forwarded-For başlığına bakacak şekilde güncellemelisin.
//app.UseMiddleware<Deepfake.API.Middlewares.IpValidationMiddleware>();

app.MapControllers();

// TODO: Gelecekte sistem loglarını (Serilog) merkezi bir yere toplamak için yapılandırma ekle.
// TODO: Veritabanı migration'larını uygulama ayağa kalkarken otomatik çalıştırmak için kod ekle.

// Railway'in atadığı PORT'u dinle.
var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
app.Run($"http://0.0.0.0:{port}");