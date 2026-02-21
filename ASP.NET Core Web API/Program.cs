using System.Text;
using Deepfake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using ASP.NET_Core_Web_API.Workers;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


var supabaseUrl = builder.Configuration["Supabase:Url"];
var supabaseKey = builder.Configuration["Supabase:Key"];
var supabaseOptions = new Supabase.SupabaseOptions { AutoConnectRealtime = false };

// Supabase Client'ı Singleton olarak (tek bir örnek) sisteme kaydediyoruz
builder.Services.AddSingleton(new Supabase.Client(supabaseUrl!, supabaseKey, supabaseOptions));

// Kendi yazdığımız IStorageService'i Infrastructure'daki SupabaseStorageService ile eşleştiriyoruz
builder.Services.AddScoped<Deepfake.Application.Interfaces.IStorageService, Deepfake.Infrastructure.Services.SupabaseStorageService>();


// RABBITMQ PUBLISHER SERVİSİ
builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisJobPublisher, Deepfake.Infrastructure.Services.RabbitMqPublisherService>();

// Arka Plan Dinleyicisini (Worker) Kaydet
builder.Services.AddHostedService<RabbitMqResultListener>();   

// REPOSITORY PATTERN KAYDI
builder.Services.AddScoped<Deepfake.Application.Interfaces.IAnalysisRepository, Deepfake.Infrastructure.Repositories.AnalysisRepository>();


// 2. JWT Authentication Ayarları
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret!)),
            ClockSkew = TimeSpan.Zero 
        };
        
        // YENİ: JWT Middleware'ine "Token'ı Cookie'de de ara" diyoruz!
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // İstekte 'jwt_token' adında bir çerez varsa, onu kimlik olarak kabul et
                if (context.Request.Cookies.TryGetValue("jwt_token", out var cookieToken))
                {
                    context.Token = cookieToken;
                }
                return Task.CompletedTask;
            }
        };
    });


// 3. IP Tabanlı Akıllı Rate Limiter Ayarları
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.CreateChained( 
        // 1. Kural: Dakikada 5 İstek [cite: 305]
    System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        // SADECE Upload endpoint'ini sınırla!
        if (httpContext.Request.Path.StartsWithSegments("/api/Analysis/upload", StringComparison.OrdinalIgnoreCase))
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter($"{ip}-min", _ =>
                new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
        }
        // Diğer tüm istekler (history, result) Sınırsız!
        return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("unlimited");
    }),
        
    
    System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (httpContext.Request.Path.StartsWithSegments("/api/Analysis/upload", StringComparison.OrdinalIgnoreCase))
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter($"{ip}-hour", _ =>
                new System.Threading.RateLimiting.FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) });
        }
        return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("unlimited");
    })
    );
});


builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter(); // Kimlik doğrulamadan önce hızı kes!
app.UseAuthentication(); // Önce kimlik doğrula (JWT)
app.UseAuthorization();  // Sonra yetki ver

// KENDİ YAZDIĞIMIZ IP KALKANI BURAYA GELECEK:
app.UseMiddleware<ASP.NET_Core_Web_API.Middlewares.IpValidationMiddleware>();

app.MapControllers();

app.Run();