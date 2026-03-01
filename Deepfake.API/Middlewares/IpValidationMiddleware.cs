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
            // 1. Kullanıcı giriş yapmış mı? (Token'ı var mı?)
            if (context.User.Identity?.IsAuthenticated == true)
            {
                // 2. Token'ın içine mühürlediğimiz Orijinal IP'yi al
                var tokenIp = context.User.Claims.FirstOrDefault(c => c.Type == "ip")?.Value;

                // 3. Kullanıcının ŞU ANKİ GERÇEK IP'sini bul
                // Railway gibi proxy arkasında olduğumuz için önce 'X-Forwarded-For' başlığına bakıyoruz
                var currentIp = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

                if (!string.IsNullOrEmpty(currentIp))
                {
                    // Bazen proxy zinciri olur (ip1, ip2, ip3), bize her zaman en baştaki (asıl kullanıcı) lazım
                    currentIp = currentIp.Split(',').First().Trim();
                }
                else
                {
                    // Eğer proxy yoksa (Localhost'ta test ediyorsan) normal IP'yi al
                    currentIp = context.Connection.RemoteIpAddress?.ToString();
                }

                // IPv4/IPv6 uyumsuzluklarını gidermek için ::1 (localhost) kontrolü
                if (currentIp == "::1") currentIp = "127.0.0.1";
                if (tokenIp == "::1") tokenIp = "127.0.0.1";

                // 4. Karşılaştırma: Token'daki IP ile gelen IP eşleşmiyor mu?
                if (!string.IsNullOrEmpty(tokenIp) && !string.IsNullOrEmpty(currentIp) && tokenIp != currentIp)
                {
                    // 🚨 Eşleşmedi! Token çalınmış olabilir veya kullanıcı ağ değiştirmiş olabilir. İsteği reddet!
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new 
                    { 
                        success = false, 
                        message = "Güvenlik İhlali: Ağ bağlantınız değiştiği için güvenlik sebebiyle işlem reddedildi. Lütfen sayfayı yenileyin." 
                    });
                    return; // Akışı kes (Controller'a gitmesine izin verme)
                }
            }

            // Sorun yoksa bir sonraki adıma (Controller'a) geç
            await _next(context);
        }
    }
}