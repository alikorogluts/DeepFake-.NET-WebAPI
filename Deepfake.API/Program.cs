using Deepfake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Deepfake.API.Workers;
using Microsoft.AspNetCore.HttpOverrides; // 🚨 Yeni eklendi: Proxy üzerinden gerçek IP için
using Deepfake.API.Extensions;
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

#region 🟣 İMAGE PROCESSING SERVICES

builder.Services.AddScoped<Deepfake.Application.Interfaces.IImageProcessingService,Deepfake.Infrastructure.Services.ImageProcessingService>();

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
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("JWT_SECRET not found");
builder.Services.AddCustomJwtAuth(jwtSecret);
#endregion
#region ⚡ RATE LIMITER
builder.Services.AddCustomRateLimiter();
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
builder.Services.AddHealthChecks();
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


app.UseMiddleware<Deepfake.API.Middlewares.IpValidationMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");


// TODO: Gelecekte sistem loglarını (Serilog) merkezi bir yere toplamak için yapılandırma ekle.
// TODO: Veritabanı migration'larını uygulama ayağa kalkarken otomatik çalıştırmak için kod ekle.

// Railway'in atadığı PORT'u dinle.
var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
app.Run($"http://0.0.0.0:{port}");