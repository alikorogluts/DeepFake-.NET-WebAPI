using System.Net;

namespace ASP.NET_Core_Web_API.Middlewares;

public class IpValidationMiddleware
{
    private readonly RequestDelegate _next;

    public IpValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        // Eğer istek yetkilendirilmişse (yani geçerli bir JWT varsa) IP kontrolü yap
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tokenIp = context.User.FindFirst("ip")?.Value;
            var requestIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            // Token içindeki IP ile isteği atan IP eşleşmiyor! (Olası token hırsızlığı)
            if (tokenIp != requestIp)
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new 
                { 
                    success = false, 
                    message = "IP adresi eşleşmedi. Güvenlik ihlali." 
                });
                return;
            }
        }

        // Sorun yoksa bir sonraki adıma (Controller'a) geç
        await _next(context);
    }
}