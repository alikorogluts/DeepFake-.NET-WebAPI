using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Deepfake.API.Extensions;

public static class RateLimiterExtensions
{
    // ── Sabitler — tek yerden yönet ──────────────────────────────────────────
    private const int    AuthPermitLimit      = 3;
    private static readonly TimeSpan AuthWindow = TimeSpan.FromMinutes(5);

    private const int    UploadMinuteLimit    = 5;
    private static readonly TimeSpan UploadMinuteWindow = TimeSpan.FromMinutes(1);

    private const int    UploadHourLimit      = 20;
    private static readonly TimeSpan UploadHourWindow = TimeSpan.FromHours(1);

    // Saatlik ile dakikalık pencereyi ayırt etmek için eşik (saniye)
    // Dakikalık kalan ≤ 120sn, saatlik kalan > 120sn
    private const double MinuteHourThresholdSeconds = 120;

    public static IServiceCollection AddCustomRateLimiter(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // ✅ FIX: OnRejected artık path+method'a bakarak doğru mesajı üretiyor.
            // Eski kod sadece retryAfter.TotalMinutes > 1 koşuluna bakıyordu;
            // Auth limiti (5 dk pencere) bu koşulu sağladığından
            // "Saatlik yükleme sınırı" mesajı çıkıyordu — YANLIŞ.
            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode  = StatusCodes.Status429TooManyRequests;
                response.ContentType = "application/json";
                
                // ✅ 2. ÇÖZÜM: Varnish 503 Hatasını Önlemek İçin Body'yi Boşa Akıt (Draining)
                // Varnish'in stream'i başarıyla tamamlayabilmesi için gelen veriyi okuyup "Stream.Null" (hiçlik) içine atıyoruz.
                if (context.HttpContext.Request.ContentLength > 0 && context.HttpContext.Request.Body.CanRead)
                {
                    await context.HttpContext.Request.Body.CopyToAsync(Stream.Null, ct);
                }

                var path   = context.HttpContext.Request.Path;
                var method = context.HttpContext.Request.Method;
                context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter);

                var message = BuildRejectionMessage(path, method, retryAfter);

                Console.WriteLine(
                    $"🚫 [RATE LIMIT REJECTED] {method} {path} | " +
                    $"RetryAfter: {retryAfter.TotalSeconds:F0}s | Mesaj: {message}");

                await response.WriteAsJsonAsync(new { success = false, message }, ct);
            };

            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(

                // ── 1. ZİNCİR: Auth POST koruması (5 dk / 3 istek) ──────────
                PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                {
                    var path   = ctx.Request.Path;
                    var method = ctx.Request.Method;

                    // GET /api/v1/auth → sınırsız (SPA her açılışta kontrol eder)
                    if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsGet(method))
                        return RateLimitPartition.GetNoLimiter("unlimited");

                    // POST /api/v1/auth → 5 dakikada 3 istek
                    if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsPost(method))
                    {
                        var ip = GetCleanIp(ctx);
                        Console.WriteLine($"🚦 [AUTH] IP: {ip}");
                        return RateLimitPartition.GetFixedWindowLimiter($"{ip}-auth", _ =>
                            new FixedWindowRateLimiterOptions
                            {
                                PermitLimit = AuthPermitLimit,
                                Window      = AuthWindow,
                            });
                    }

                    return RateLimitPartition.GetNoLimiter("unlimited");
                }),

                // ── 2. ZİNCİR: Upload dakikalık koruma (1 dk / 5 istek) ─────
                PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                {
                    if (!IsUploadRequest(ctx))
                        return RateLimitPartition.GetNoLimiter("unlimited");

                    var ip = GetCleanIp(ctx);
                    Console.WriteLine($"🚦 [UPLOAD/MIN] IP: {ip}");
                    return RateLimitPartition.GetFixedWindowLimiter($"{ip}-upload-min", _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = UploadMinuteLimit,
                            Window      = UploadMinuteWindow,
                        });
                }),

                // ── 3. ZİNCİR: Upload saatlik koruma (1 saat / 20 istek) ────
                PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                {
                    if (!IsUploadRequest(ctx))
                        return RateLimitPartition.GetNoLimiter("unlimited");

                    var ip = GetCleanIp(ctx);
                    return RateLimitPartition.GetFixedWindowLimiter($"{ip}-upload-hour", _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = UploadHourLimit,
                            Window      = UploadHourWindow,
                        });
                })
            );
        });

        return services;
    }

    // ── Yardımcı metotlar ────────────────────────────────────────────────────

    private static bool IsUploadRequest(HttpContext ctx) =>
        ctx.Request.Path.StartsWithSegments("/api/v1/analyses") &&
        HttpMethods.IsPost(ctx.Request.Method);

    // Railway/Docker proxy'sinin eklediği ::ffff: ön ekini temizler
    private static string GetCleanIp(HttpContext ctx) =>
        (ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown")
        .Replace("::ffff:", "");

    private static string BuildRejectionMessage(PathString path, string method, TimeSpan retryAfter)
    {
        // Auth POST reddedildi
        if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsPost(method))
        {
            var waitSec = (int)Math.Ceiling(retryAfter.TotalSeconds);
            return $"Çok fazla giriş denemesi. Lütfen {waitSec} saniye sonra tekrar deneyin.";
        }

        // Upload reddedildi — hangi pencere?
        if (path.StartsWithSegments("/api/v1/analyses") && HttpMethods.IsPost(method))
        {
            if (retryAfter.TotalSeconds > MinuteHourThresholdSeconds)
            {
                var waitMin = (int)Math.Ceiling(retryAfter.TotalMinutes);
                return $"Saatlik yükleme sınırınızı ({UploadHourLimit} resim) aştınız. " +
                       $"Lütfen {waitMin} dakika sonra tekrar deneyin.";
            }
            else
            {
                var waitSec = (int)Math.Ceiling(retryAfter.TotalSeconds);
                return $"Dakikalık yükleme sınırınızı ({UploadMinuteLimit} resim) aştınız. " +
                       $"Lütfen {waitSec} saniye sonra tekrar deneyin.";
            }
        }

        return "Çok fazla istek attınız. Lütfen daha sonra tekrar deneyin.";
    }
}