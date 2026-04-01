using Deepfake.API.Extensions;
using Deepfake.API.Middlewares; 
using Deepfake.API.Workers;
using Deepfake.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

#region ENV YÜKLEME
try   { DotNetEnv.Env.TraversePath().Load(); }
catch { Console.WriteLine("Uyarı: .env bulunamadı. Sistem ortam değişkenleri kullanılacak."); }
#endregion

var builder = WebApplication.CreateBuilder(args);

// ✅ Kestrel Body Sınırı (DRY Prensibi - Magic Number'dan kurtulduk)
long maxUploadSize = 10 * 1024 * 1024; // 10 MB
builder.WebHost.ConfigureKestrel(k =>
{
    k.Limits.MaxRequestBodySize = maxUploadSize; 
});

// ── ForwardedHeaders ─────────────────────────────────────────────────────────
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null; 
});

#region DATABASE
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION")));
#endregion

#region SERVICES & REPOSITORIES
builder.Services.AddScoped<Deepfake.Application.Interfaces.IImageProcessingService, Deepfake.Infrastructure.Services.ImageProcessingService>();
builder.Services.AddScoped<Deepfake.Application.Interfaces.IStorageService, Deepfake.Infrastructure.Services.SupabaseStorageService>();
builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisRepository, Deepfake.Infrastructure.Repositories.AnalysisRepository>();
#endregion

#region SUPABASE & RABBITMQ
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL")!;
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY")!;

builder.Services.AddSingleton(new Supabase.Client(supabaseUrl, supabaseKey,
    new Supabase.SupabaseOptions { AutoConnectRealtime = false }));

builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisJobPublisher, Deepfake.Infrastructure.Services.RabbitMqPublisherService>();
builder.Services.AddHostedService<RabbitMqResultListener>();
#endregion

#region SECURITY — JWT & RATE LIMIT
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new InvalidOperationException("JWT_SECRET ortam değişkeni eksik.");

builder.Services.AddCustomJwtAuth(jwtSecret);
builder.Services.AddCustomRateLimiter();

// 🚀 CORS: Canlı ortam (Client) adresleri eklendi
var allowedOriginsStr = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") ?? "https://truvalens.com,https://www.truvalens.com";
var allowedOrigins = allowedOriginsStr.Split(",",StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o=> o.AddPolicy("AllowedOriginsPolicy", p=>
    p.WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
    ));
#endregion

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

// ════════════════════════════════════════════════════════════════
// MİDDLEWARE SIRALAMASI — (Kusursuz Akış)
// ════════════════════════════════════════════════════════════════

// 1. Gerçek IP'yi al
app.UseForwardedHeaders();

// 2. ✅ FIX (503): Early Rejection Middleware (Temiz Sınıf Çağrısı)
app.UseMiddleware<EarlyPayloadRejectionMiddleware>();



if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.MapOpenApi();
}

// 3. CORS
app.UseCors("AllowedOriginsPolicy");

// 4. Rate Limit 
app.UseRateLimiter();

// 5. Kimlik doğrulama & yetkilendirme
app.UseAuthentication();
app.UseAuthorization();

// 6. IP uyuşmazlığı kontrolü 
app.UseMiddleware<IpValidationMiddleware>();

// ── Endpoint'ler ─────────────────────────────────────────────────────────────
app.MapControllers();
app.MapHealthChecks("/health");

// 🚀 PORT AYARI (Varsayılan 5000)
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");