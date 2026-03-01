using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Deepfake.API.Extensions
{
    public static class JwtExtensions
    {
        public static IServiceCollection AddCustomJwtAuth(this IServiceCollection services, string jwtSecret)
        {
            // Ortam değişkenlerini alıyoruz
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER");
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE");

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true, // Feedback: Artık doğrula
                        ValidateAudience = true, // Feedback: Artık doğrula
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = issuer,
                        ValidAudience = audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                        ClockSkew = TimeSpan.FromSeconds(30) // Feedback 5: 5 dk bekleme, 30 sn'de bitir
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            if (context.Request.Cookies.TryGetValue("jwt_token", out var cookieToken))
                            {
                                context.Token = cookieToken;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });

            return services;
        }    }
}