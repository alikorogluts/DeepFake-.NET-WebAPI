using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Deepfake.API.Middlewares
{
    public class EarlyPayloadRejectionMiddleware
    {
        private readonly RequestDelegate _next;
        private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

        public EarlyPayloadRejectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }
//todo : Varnis hatası devam ederse burada boşa atmayı unutma gelen isteği 
        public async Task Invoke(HttpContext context)
        {
            var isUpload = context.Request.Path.StartsWithSegments("/api/v1/analyses")
                           && HttpMethods.IsPost(context.Request.Method);

            if (isUpload)
            {
                var length = context.Request.ContentLength;

                if (length is null)
                {
                    context.Response.StatusCode = StatusCodes.Status411LengthRequired;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = "Content-Length header'ı zorunludur."
                    });
                    return; // İsteği burada kes (Varnish kurtulur)
                }

                if (length > MaxFileSize)
                {
                    context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                    context.Response.Headers.Connection = "close"; // Varnish stream'i kapatsın
                    await context.Response.WriteAsJsonAsync(new
                    {
                        success = false,
                        message = "Dosya boyutu 10 MB'ı geçemez."
                    });
                    return; // İsteği burada kes
                }
            }

            await _next(context);
        }
    }
}