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
            // 1. Kullanıcı giriş yapmış mı?
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // 2. Token içindeki IP'yi al (Claim tipini "ip" olarak belirlemiştik)
                var tokenIp = context.User.Claims.FirstOrDefault(c => c.Type == "ip")?.Value;

                // 3. Kullanıcının ŞU ANKİ GERÇEK IP'sini al
                // NOT: Program.cs'deki UseForwardedHeaders sayesinde bu değer artık Proxy IP'si değil, 
                // doğrudan kullanıcının GERÇEK IP'sidir.
                var currentIp = context.Connection.RemoteIpAddress?.ToString();

                // 4. Localhost Uyumluluğu (IPv6 - IPv4 eşitleme)
                if (currentIp == "::1") currentIp = "127.0.0.1";
                if (tokenIp == "::1") tokenIp = "127.0.0.1";

                // 5. Karşılaştırma
                if (!string.IsNullOrEmpty(tokenIp) && tokenIp != currentIp)
                {
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