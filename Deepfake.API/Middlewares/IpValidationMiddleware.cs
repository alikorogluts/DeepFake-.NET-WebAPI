using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Deepfake.API.Middlewares
{
    public class IpValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public IpValidationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // 1. Token içindeki IP'yi al ve temizle
                var rawTokenIp = context.User.Claims.FirstOrDefault(c => c.Type == "ip")?.Value;
                var cleanTokenIp = rawTokenIp?.Replace("::ffff:", "");

                // 2. Güncel bağlantı IP'sini al ve temizle
                var rawCurrentIp = context.Connection.RemoteIpAddress?.ToString();
                var cleanCurrentIp = rawCurrentIp?.Replace("::ffff:", "");

                // 3. Localhost (IPv6 to IPv4) Normalizasyonu
                if (cleanCurrentIp == "::1") cleanCurrentIp = "127.0.0.1";
                if (cleanTokenIp == "::1") cleanTokenIp = "127.0.0.1";

                // 📝 DEBUG LOG: Railway Dashboard'da bu iki değeri yan yana görelim
                Console.WriteLine($"🔍 [IP VALIDATION] Token IP: {cleanTokenIp} | Connection IP: {cleanCurrentIp}");

                // 4. Karşılaştırma
                if (!string.IsNullOrEmpty(cleanTokenIp) && cleanTokenIp != cleanCurrentIp)
                {
                    // ❌ Hata Logu
                    Console.WriteLine($"❌ [IP MISMATCH] Güvenlik Engeli: {cleanTokenIp} != {cleanCurrentIp}");

                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        success = false, 
                        message = "Güvenlik İhlali: IP uyuşmazlığı tespit edildi. Lütfen tekrar giriş yapın." 
                    });
                    return; 
                }
            }

            await _next(context);
        }
    }
}