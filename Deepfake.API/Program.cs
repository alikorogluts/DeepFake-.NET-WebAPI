using Deepfake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Deepfake.API.Workers;
using Microsoft.AspNetCore.HttpOverrides;
using Deepfake.API.Extensions;

#region 🔥 ENV YÜKLEME
try
{
    DotNetEnv.Env.TraversePath().Load();
}
catch
{
    Console.WriteLine("Uyarı: .env dosyası bulunamadı. Sistem ortam değişkenleri kullanılacak.");
}
#endregion

var builder = WebApplication.CreateBuilder(args);

// 🚨 1. ADIM: FORWARDED HEADERS KONFİGÜRASYONU
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

#region 🟢 DATABASE
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION"))
);
#endregion

#region 🟣 SERVICES & REPOSITORIES
builder.Services.AddScoped<Deepfake.Application.Interfaces.IImageProcessingService, Deepfake.Infrastructure.Services.ImageProcessingService>();

builder.Services.AddScoped<Deepfake.Application.Interfaces.IStorageService, Deepfake.Infrastructure.Services.SupabaseStorageService>();

builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisRepository, Deepfake.Infrastructure.Repositories.AnalysisRepository>();

//  TODO : Analiz temizleme servisi kaydı (Daha önce konuştuğumuz) 
// builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisCleanupService, Deepfake.Infrastructure.Services.AnalysisCleanupService>();
#endregion

#region 🔵 SUPABASE & RABBITMQ
var supabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL");
var supabaseKey = Environment.GetEnvironmentVariable("SUPABASE_KEY");
builder.Services.AddSingleton(new Supabase.Client(supabaseUrl!, supabaseKey!, new Supabase.SupabaseOptions { AutoConnectRealtime = false }));

builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisJobPublisher, Deepfake.Infrastructure.Services.RabbitMqPublisherService>();
builder.Services.AddHostedService<RabbitMqResultListener>();
// builder.Services.AddHostedService<AnalysisCleanupWorker>();
#endregion

#region 🔐 SECURITY (JWT & RATE LIMIT)
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? throw new Exception("JWT_SECRET missing");
builder.Services.AddCustomJwtAuth(jwtSecret);
builder.Services.AddCustomRateLimiter();

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

// 🚨 2. ADIM: MIDDLEWARE SIRALAMASI
app.UseForwardedHeaders(); // En üstte kalmalı

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseHttpsRedirection(); 
}

app.UseCors("AllowAll");
app.UseRateLimiter(); // Kimlik doğrulamadan önce hızı kesmek performanslıdır

app.UseAuthentication();
app.UseAuthorization();

// Bizim özel IP kontrolümüz Yetkilendirmeden SONRA çalışmalı
app.UseMiddleware<Deepfake.API.Middlewares.IpValidationMiddleware>();

app.MapControllers();
app.MapHealthChecks("/health");

// 🚨 OTOMATIK MIGRATION (Uygulama ayağa kalkarken tabloları basar)
//using (var scope = app.Services.CreateScope())
//{
//  var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    db.Database.Migrate();
//}

var port = Environment.GetEnvironmentVariable("PORT") ?? "80";
app.Run($"http://0.0.0.0:{port}");