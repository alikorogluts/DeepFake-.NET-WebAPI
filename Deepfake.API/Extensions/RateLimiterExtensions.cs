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

                    string errorMessage = "Çok fazla istek attınız. Lütfen daha sonra tekrar deneyin.";

                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    {
                        if (retryAfter.TotalMinutes > 1)
                        {
                            errorMessage = $"Saatlik yükleme sınırınızı (20 resim) aştınız. Lütfen {Math.Ceiling(retryAfter.TotalMinutes)} dakika sonra tekrar deneyin.";
                        }
                        else
                        {
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
                    
                    // 🛡️ 1. ZİNCİR: AUTH KORUMASI (Saf REST Uyumlu)
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        var path = httpContext.Request.Path;
                        var method = httpContext.Request.Method;

                        // 🟢 GET /api/v1/auth -> Status kontrolü (Sınırsız)
                        if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsGet(method))
                        {
                            return RateLimitPartition.GetNoLimiter("unlimited");
                        }

                        // 🔴 POST /api/v1/auth -> Token alma (5 dakikada 3 kez ile sınırlı)
                        if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsPost(method))
                        {
                            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                            return RateLimitPartition.GetFixedWindowLimiter($"{ip}-auth", _ => 
                                new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(5) });
                        }

                        return RateLimitPartition.GetNoLimiter("unlimited");
                    }),

                    // 🛡️ 2. ZİNCİR: ANALİZ (UPLOAD) KORUMASI (Dakikalık)
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        // 🚨 ROTA GÜNCELLEDİ: /api/v1/analyses ve POST kontrolü
                        bool isUploadRequest = httpContext.Request.Path.StartsWithSegments("/api/v1/analyses") && 
                                               HttpMethods.IsPost(httpContext.Request.Method);

                        if (isUploadRequest)
                        {
                            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                            return RateLimitPartition.GetFixedWindowLimiter(
                                $"{ip}-upload-min",
                                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) });
                        }
                        return RateLimitPartition.GetNoLimiter("unlimited");
                    }),

                    // 🛡️ 3. ZİNCİR: ANALİZ (UPLOAD) KORUMASI (Saatlik)
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        bool isUploadRequest = httpContext.Request.Path.StartsWithSegments("/api/v1/analyses") && 
                                               HttpMethods.IsPost(httpContext.Request.Method);

                        if (isUploadRequest)
                        {
                            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                            return RateLimitPartition.GetFixedWindowLimiter(
                                $"{ip}-upload-hour",
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