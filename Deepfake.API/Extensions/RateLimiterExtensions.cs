using System;
using System.Linq;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Deepfake.API.Extensions
{
    public static class RateLimiterExtensions
    {
        public static IServiceCollection AddCustomRateLimiter(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";

                    // Varsayılan hata mesajı
                    string errorMessage = "Çok fazla istek attınız. Lütfen daha sonra tekrar deneyin.";

                    // 🚨 AKILLI KONTROL: Sistemin verdiği bekleme süresini (RetryAfter) okuyoruz
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    {
                        if (retryAfter.TotalMinutes > 1)
                        {
                            // Eğer beklemesi gereken süre 1 dakikadan fazlaysa, Saatlik limite takılmıştır
                            errorMessage = $"Saatlik yükleme sınırınızı (20 resim) aştınız. Lütfen {Math.Ceiling(retryAfter.TotalMinutes)} dakika sonra tekrar deneyin.";
                        }
                        else
                        {
                            // Eğer beklemesi gereken süre 1 dakikadan azsa, Dakikalık limite takılmıştır
                            errorMessage = $"Dakikalık yükleme sınırınızı (5 resim) aştınız. Lütfen {Math.Ceiling(retryAfter.TotalSeconds)} saniye sonra tekrar deneyin.";
                        }
                    }

                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = errorMessage
                    }, cancellationToken);
                };

                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    // Dakikalık Limit (1 dakikada 5 istek)
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        bool isUploadRequest = httpContext.Request.Path.StartsWithSegments("/api/analyses") && 
                                               HttpMethods.IsPost(httpContext.Request.Method);

                        if (isUploadRequest)
                        {
                            var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').First().Trim() 
                                     ?? httpContext.Connection.RemoteIpAddress?.ToString() 
                                     ?? "unknown";

                            return RateLimitPartition.GetFixedWindowLimiter(
                                $"{ip}-min",
                                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
                        }
                        return RateLimitPartition.GetNoLimiter("unlimited");
                    }),

                    // Saatlik Limit (1 Saatte 20 istek)
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        bool isUploadRequest = httpContext.Request.Path.StartsWithSegments("/api/analyses") && 
                                               HttpMethods.IsPost(httpContext.Request.Method);

                        if (isUploadRequest)
                        {
                            var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').First().Trim() 
                                     ?? httpContext.Connection.RemoteIpAddress?.ToString() 
                                     ?? "unknown";

                            return RateLimitPartition.GetFixedWindowLimiter(
                                $"{ip}-hour",
                                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromHours(1) });
                        }
                        return RateLimitPartition.GetNoLimiter("unlimited");
                    })
                );
            });

            return services;
        }
    }
}