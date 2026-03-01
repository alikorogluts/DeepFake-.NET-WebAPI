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
                    
                    // 🛡️ 1. ZİNCİR: AUTH KORUMASI
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        var path = httpContext.Request.Path;
                        var method = httpContext.Request.Method;

                        if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsGet(method))
                        {
                            return RateLimitPartition.GetNoLimiter("unlimited");
                        }

                        if (path.StartsWithSegments("/api/v1/auth") && HttpMethods.IsPost(method))
                        {
                            // IP'yi al ve temizle (::ffff: gibi ön ekleri sil)
                            var rawIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                            var cleanIp = rawIp.Replace("::ffff:", "");
                            
                            // 📝 DEBUG LOG
                            Console.WriteLine($"🚦 [RATE LIMIT - AUTH] Path: {path} | Method: {method} | IP: {cleanIp}");

                            return RateLimitPartition.GetFixedWindowLimiter($"{cleanIp}-auth", _ => 
                                new FixedWindowRateLimiterOptions { PermitLimit = 3, Window = TimeSpan.FromMinutes(5) });
                        }

                        return RateLimitPartition.GetNoLimiter("unlimited");
                    }),

                    // 🛡️ 2. ZİNCİR: ANALİZ (UPLOAD) KORUMASI (Dakikalık & Saatlik birleşik log yapısı)
                    PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                    {
                        bool isUploadRequest = httpContext.Request.Path.StartsWithSegments("/api/v1/analyses") && 
                                               HttpMethods.IsPost(httpContext.Request.Method);

                        if (isUploadRequest)
                        {
                            var rawIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                            var cleanIp = rawIp.Replace("::ffff:", "");

                            // 📝 DEBUG LOG
                            Console.WriteLine($"🚦 [RATE LIMIT - UPLOAD] IP: {cleanIp} | Target: Analyses POST");

                            return RateLimitPartition.GetFixedWindowLimiter(
                                $"{cleanIp}-upload-min",
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
                            var cleanIp = httpContext.Connection.RemoteIpAddress?.ToString()?.Replace("::ffff:", "") ?? "unknown";

                            return RateLimitPartition.GetFixedWindowLimiter(
                                $"{cleanIp}-upload-hour",
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